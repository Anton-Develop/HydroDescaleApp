using System.Threading.Tasks.Dataflow;
using HydroDescaleApp.Server.Data;
using Microsoft.EntityFrameworkCore;
using S7.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace HydroDescaleApp.Server.Services;

public class PlcPollingService : BackgroundService
{
    private readonly ILogger<PlcPollingService> _logger;
    private readonly AppDbContext _context;
    private readonly IOracleService _oracleService;
    private readonly string _plcIp;
    private readonly int _dbNumber = 550;
    private readonly Dictionary<int, int> _positionMap = new()
    {
        { 0, 59 }, // DBX0.0 -> pos 59 (left, furnace 1)
        { 1, 61 }, // DBX0.1 -> pos 61 (right, furnace 1)
        { 2, 62 }, // DBX0.2 -> pos 62 (left, furnace 2)
        { 3, 64 }, // DBX0.3 -> pos 64 (right, furnace 2)
        { 4, 65 }, // DBX0.4 -> pos 65 (left, furnace 3)
        { 5, 67 }  // DBX0.5 -> pos 67 (right, furnace 3)
    };

    // <-- Теперь отслеживаем изменение байта, а не watchdog
    private byte? _lastByteValue = null;

    public PlcPollingService(
        ILogger<PlcPollingService> logger,
        AppDbContext context,
        IOracleService oracleService,
        IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _oracleService = oracleService;
        _plcIp = configuration["Plc:ReadIp"] ?? "127.0.0.1";
        _configuration = configuration;
    }

    private readonly IConfiguration _configuration;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlcPollingService is starting.");

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(2000)); // Частый опрос для отслеживания изменений

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollPlcAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while polling PLC.");
            }
        }

        _logger.LogInformation("PlcPollingService is stopping.");
    }

    private async Task PollPlcAsync()
    {
        using var plc = new Plc(CpuType.S7400, _plcIp, 0, 2);
        try
        {
            await plc.OpenAsync();

            // Read DB550.DBB0 (contains DBX0.0 - DBX0.5)
            var currentByteValue = (byte)plc.Read($"{_dbNumber}.DBB0");

            // Check if byte value has changed
            if (_lastByteValue.HasValue && _lastByteValue.Value == currentByteValue)
            {
                // Байт не изменился, выходим
                return;
            }

            _logger.LogDebug("DBB0 changed from {_lastByteValue} to {currentByteValue}", _lastByteValue, currentByteValue);
            _lastByteValue = currentByteValue;

            // Если байт изменился, ищем активный бит
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
                _logger.LogInformation("Event: Active bit {Bit} changed, corresponding to position {Pos}", activeBit.Value, pos);

                var steelGrade = await _oracleService.GetSteelGradeByPositionAsync(pos);
                if (!string.IsNullOrEmpty(steelGrade))
                {
                    _logger.LogInformation("Steel grade '{Grade}' found for position {Pos}", steelGrade, pos);

                    var settings = await _context.SteelGrades
                        .FirstOrDefaultAsync(s => s.SteelGradeName == steelGrade);

                    if (settings != null)
                    {
                        _logger.LogInformation("Settings found: Pumps={Pumps}, Pressure={Pressure}", settings.NumberOfPumps, settings.PressureSetting);

                        // Send to PLC Закомментим пока не будет создано правильной DB
                      //  var plcService = new PlcService(_configuration);
                      //  await plcService.WriteDescaleSettingsAsync(settings.NumberOfPumps, settings.PressureSetting);
                    }
                    else
                    {
                        _logger.LogWarning("No settings found for steel grade '{Grade}', using defaults.", steelGrade);
                        // Send to PLC Закомментим пока не будет создано правильной DB
                       // var plcService = new PlcService(_configuration);
                       // await plcService.WriteDescaleSettingsAsync(2, 18.3); // defaults
                    }
                }
                else
                {
                    _logger.LogWarning("No steel grade found for position {Pos}", pos);
                }
            }
            else
            {
                // Байт изменился, но ни один из интересующих битов не установлен.
                // Это может быть, например, если установился бит 6 или 7, или все сброшены.
                // Логируем, если это важно для отладки.
                _logger.LogDebug("DBB0 changed, but no active bit found in DBX0.0 - DBX0.5. Current value: {currentByteValue}", currentByteValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with PLC at {_plcIp}", _plcIp);
        }
    }
}