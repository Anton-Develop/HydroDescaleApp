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
    private readonly Dictionary<int, int> _positionMap = new()
    {
        { 0, 59 }, // DBX0.0 -> pos 59 (left, furnace 1)
        { 1, 61 }, // DBX0.1 -> pos 61 (right, furnace 1)
        { 2, 62 }, // DBX0.2 -> pos 62 (left, furnace 2)
        { 3, 64 }, // DBX0.3 -> pos 64 (right, furnace 2)
        { 4, 65 }, // DBX0.4 -> pos 65 (left, furnace 3)
        { 5, 67 }  // DBX0.5 -> pos 67 (right, furnace 3)
    };

    private byte? _lastByteValue = null;

    // ❌ УБРАТЬ IOracleService из параметров конструктора!
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

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(5000));

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
        using var plc = new Plc(CpuType.S7400, _plcIp, 0,3); 
        
        try
        {
            
            await plc.OpenAsync();
            


            // Read DB550.DBB0 (contains DBX0.0 - DBX0.5) {_dbNumber}
            var currentByteValue = (byte)plc.Read($"DB550.DBB0");
            
            // Check if byte value has changed
            if (_lastByteValue.HasValue && _lastByteValue.Value == currentByteValue)
            {
                return;
            }

            _logger.LogDebug("DBB0 changed from {_lastByteValue} to {currentByteValue}", _lastByteValue, currentByteValue);
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
                _logger.LogInformation("Event: Active bit {Bit} changed, corresponding to position {Pos}", activeBit.Value, pos);

                // Создаём scope для получения IOracleService и AppDbContext
                using var scope = _scopeFactory.CreateScope();
                var oracleService = scope.ServiceProvider.GetRequiredService<IOracleService>();

                var steelGrade = await oracleService.GetSteelGradeByPositionAsync(pos);
                if (!string.IsNullOrEmpty(steelGrade))
                {
                    _logger.LogInformation("Steel grade '{Grade}' found for position {Pos}", steelGrade, pos);

                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var settings = await context.SteelGrades
                        .FirstOrDefaultAsync(s => s.SteelGradeName == steelGrade);

                    if (settings != null)
                    {
                        _logger.LogInformation("Settings found: Pumps={Pumps}, Pressure={Pressure}", settings.NumberOfPumps, settings.PressureSetting);

                        // Send to PLC
                    //    var plcService = scope.ServiceProvider.GetRequiredService<IPlcService>();
                     //   await plcService.WriteDescaleSettingsAsync(settings.NumberOfPumps, settings.PressureSetting);
                    }
                    else
                    {
                        _logger.LogWarning("No settings found for steel grade '{Grade}', using defaults.", steelGrade);
                     //   var plcService = scope.ServiceProvider.GetRequiredService<IPlcService>();
                     //   await plcService.WriteDescaleSettingsAsync(2, 18.3); // defaults
                    }
                }
                else
                {
                    _logger.LogWarning("No steel grade found for position {Pos}", pos);
                }
            }
            else
            {
                _logger.LogDebug("DBB0 changed, but no active bit found in DBX0.0 - DBX0.5. Current value: {currentByteValue}", currentByteValue);
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка коммуникации с PLC at {_plcIp}", _plcIp);
        }
    }
}