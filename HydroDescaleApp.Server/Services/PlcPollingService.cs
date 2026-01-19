using System.Threading.Tasks.Dataflow;
using Microsoft.EntityFrameworkCore;
using S7.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HydroDescaleApp.Server.Services;
using HydroDescaleApp.Server.Data;

namespace HydroDescaleApp.Server.Services; // Убедитесь, что пространство имён правильное

public class PlcPollingService : BackgroundService
{
    private readonly ILogger<PlcPollingService> _logger;
    private readonly string _plcIp;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly int _dbNumber = 550;
    private readonly int _bitToWatch =6;
    private readonly Dictionary<int, int> _positionMap = new()
    
        { 0, 59 }, // DBX0.0 -> pos 59 (left, furnace 1)
        { 1, 61 }, // DBX0.1 -> pos 61 (right, furnace 1)
        { 2, 62 }, // DBX0.2 -> pos 62 (left, furnace 2)
        { 3, 64 }, // DBX0.3 -> pos 64 (right, furnace 2)
        { 4, 65 }, // DBX0.4 -> pos 65 (left, furnace 3)
        { 5, 67 }  // DBX0.5 -> pos 67 (right, furnace 3)
    };
    //----Состояние-----

    private bool? _lastBitValue = null;
    private string? _lastReadSteelGrade=null; //For DBX0.6
    private byte? _lastByteValue = null; //For DBX0.0-DBX0.5
    
    public PlcPollingService(
        ILogger<PlcPollingService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _plcIp = configuration["Plc:ReadIp"] ?? "127.0.0.1";
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlcPollingService is starting.");
        /// Таймер 1: DBX0.6 (new logic) -500ms
        using var timerNew = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        /// Таймер 2: DBX0.0 - DBX0.6 (old logic) -60 sec
        using var timerOld = new PeriodicTimer(TimeSpan.FromMinutes(2));

     var newTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested && await timerNew.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await PollPlcNewLogicAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in new PLC logic (DBX0.6).");
                }
            }
        });

        var oldTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested && await timerOld.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await PollPlcOldLogicAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in old PLC logic (DBX0.0 - DBX0.5).");
                }
            }
        });

        await Task.WhenAll(newTask, oldTask);

        _logger.LogInformation("PlcPollingService is stopping.");
    }


 // --- Новая логика: DBX0.6 ---
    private async Task PollPlcNewLogicAsync()
    {
        using var plc = new Plc(CpuType.S7400, _plcIp, 0, 3);
        try
        {
            await plc.OpenAsync();

            // Read DB550.DBB0
            var currentByteValue = (byte)plc.Read($"DB{_dbNumber}.DBB0");

            // Check if DBX0.6 has changed
            var currentBitValue = (currentByteValue & (1 << _bitToWatch)) != 0;

            /*
            if (_lastBitValue.HasValue && _lastBitValue.Value == currentBitValue)
            {
                return;
            }
*/
            _logger.LogDebug("DBX0.{Bit} changed from {_lastBitValue} to {currentBitValue}", _bitToWatch, _lastBitValue, currentBitValue);
  //          _lastBitValue = currentBitValue;

            if (currentBitValue)
            {
                _logger.LogInformation("Event: DBX0.{Bit} is TRUE - slab is on receiving roller table.", _bitToWatch);

                // ✅ Проверяем: если уже успешно читали (не пусто), то не читаем снова
                if (_lastReadSteelGrade != null && !string.IsNullOrEmpty(_lastReadSteelGrade))
                {
                    _logger.LogDebug("Steel grade already successfully read: {_lastReadSteelGrade}. Skipping Oracle query.", _lastReadSteelGrade);
                    return;
                }

                // ❌ Теперь читаем Oracle, даже если _lastReadSteelGrade == "" (читали, но было пусто)
                using var scope = _scopeFactory.CreateScope();
                var oracleService = scope.ServiceProvider.GetRequiredService<IOracleService>();

                var steelGrade = await oracleService.GetSteelGradeFromTrcViewAsync();
                _logger.LogInformation("SteelGrade is Oracle: ", steelGrade);
                if (!string.IsNullOrEmpty(steelGrade))
                {
                    _logger.LogInformation("Steel grade '{Grade}' read from VIEW_DESCALING_SERVISE_TRC.", steelGrade);

                    // ✅ Запоминаем УСПЕШНО прочитанную марку
                    _lastReadSteelGrade = steelGrade;

                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var settings = await context.SteelGrades
                        .FirstOrDefaultAsync(s => s.SteelGradeName == steelGrade);

                    if (settings != null)
                    {
                        _logger.LogInformation("Settings found for '{Grade}': Pumps={Pumps}, Pressure={Pressure}", steelGrade, settings.NumberOfPumps, settings.PressureSetting);

                        var plcService = scope.ServiceProvider.GetRequiredService<IPlcService>();
                        await plcService.WriteDescaleSettingsAsync(settings.NumberOfPumps, settings.PressureSetting);
                    }
                    else
                    {
                        _logger.LogWarning("No settings found for steel grade '{Grade}', using defaults.", steelGrade);
                        var plcService = scope.ServiceProvider.GetRequiredService<IPlcService>();
                        await plcService.WriteDescaleSettingsAsync(2, 18.3); // defaults
                    }
                }
                else
                {
                    // ❌ Oracle пустой. Запоминаем "", чтобы при следующем опросе (если бит true) снова читать
                    _logger.LogWarning("VIEW_DESCALING_SERVISE_TRC returned no steel grade. Will retry on next poll if bit is still true.");
                    _lastReadSteelGrade = ""; // <-- ВАЖНО: запоминаем пустую строку
                }
            }
            else
            {
                // Бит стал false — сбрасываем запомненную марку (и успешную, и пустую)
                if (_lastReadSteelGrade != null)
                {
                    _logger.LogDebug("DBX0.{Bit} is FALSE. Resetting last read steel grade.", _bitToWatch);
                    _lastReadSteelGrade = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with PLC at {_plcIp} (New Logic).");
        }
    }

    // --- Старая логика: DBX0.0 - DBX0.5 ---
    private async Task PollPlcOldLogicAsync()
    {
        using var plc = new Plc(CpuType.S7400, _plcIp, 0, 3);
        try
        {
            await plc.OpenAsync();

            // Read DB550.DBB0
            var currentByteValue = (byte)plc.Read($"DB{_dbNumber}.DBB0");

            // Check if byte value has changed
            if (_lastByteValue.HasValue && _lastByteValue.Value == currentByteValue)
            {
                return;
            }

            _logger.LogDebug("DBB0 changed from {_lastByteValue} to {currentByteValue} (Old Logic)", _lastByteValue, currentByteValue);
            _lastByteValue = currentByteValue;

            int? activeBit = null;
            for (int i = 0; i < 6; i++)
            {
                if ((currentByteValue & (1 << i)) != 0)
                {
                    activeBit = i;
                    break;
                }
            }

            if (activeBit.HasValue)
            {
                var pos = _positionMap[activeBit.Value];
                _logger.LogInformation("Event (Old Logic): Active bit {Bit} changed, corresponding to position {Pos}", activeBit.Value, pos);

                // Здесь можно, например, сохранить состояние в кэш или отправить в SignalR для UI
                // Пример: var state = new { Position = pos, Timestamp = DateTime.UtcNow };
                // await _hubContext.Clients.All.SendAsync("SlabStatusChanged", state);
            }
            else
            {
                _logger.LogDebug("DBB0 changed, but no active bit found in DBX0.0 - DBX0.5 (Old Logic). Current value: {currentByteValue}", currentByteValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with PLC at {_plcIp} (Old Logic).");
        }
    }

    
}