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
    private readonly int _dbwWatchdog = 20;
    private readonly Dictionary<int, int> _positionMap = new()
    {
        { 0, 59 }, // DBX0.0 -> pos 59 (left, furnace 1)
        { 1, 61 }, // DBX0.1 -> pos 61 (right, furnace 1)
        { 2, 62 }, // DBX0.2 -> pos 62 (left, furnace 2)
        { 3, 64 }, // DBX0.3 -> pos 64 (right, furnace 2)
        { 4, 65 }, // DBX0.4 -> pos 65 (left, furnace 3)
        { 5, 67 }  // DBX0.5 -> pos 67 (right, furnace 3)
    };

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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlcPollingService is starting.");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2)); // Poll every 2 seconds

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
        using var plc = new Plc(CpuType.S7400, _plcIp, 0, 65535);
        try
        {
            await plc.OpenAsync();

            // Read DB550.DBX0.0 - DBX0.5 (first 1 byte)
            var dbData = (byte)plc.Read($"{_dbNumber}.DBB0");

            int? activeBit = null;
            for (int i = 0; i < 6; i++)
            {
                if ((dbData & (1 << i)) != 0)
                {
                    activeBit = i;
                    break;
                }
            }

            if (activeBit.HasValue)
            {
                var pos = _positionMap[activeBit.Value];
                _logger.LogInformation("Active bit {Bit}, corresponding to position {Pos}", activeBit.Value, pos);

                var steelGrade = await _oracleService.GetSteelGradeByPositionAsync(pos);
                if (!string.IsNullOrEmpty(steelGrade))
                {
                    _logger.LogInformation("Steel grade '{Grade}' found for position {Pos}", steelGrade, pos);

                    var settings = await _context.SteelGrades
                        .FirstOrDefaultAsync(s => s.SteelGradeName == steelGrade);

                    if (settings != null)
                    {
                        _logger.LogInformation("Settings found: Pumps={Pumps}, Pressure={Pressure}", settings.NumberOfPumps, settings.PressureSetting);

                        // Send to PLC
                        var plcService = new PlcService(_configuration);
                        await plcService.WriteDescaleSettingsAsync(settings.NumberOfPumps, settings.PressureSetting);
                    }
                    else
                    {
                        _logger.LogWarning("No settings found for steel grade '{Grade}', using defaults.", steelGrade);
                        var plcService = new PlcService(_configuration);
                        await plcService.WriteDescaleSettingsAsync(2, 18.3); // defaults
                    }
                }
                else
                {
                    _logger.LogWarning("No steel grade found for position {Pos}", pos);
                }
            }
            else
            {
                _logger.LogDebug("No active bit found in DB550.DBX0.0 - DBX0.5.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with PLC at {_plcIp}", _plcIp);
        }
    }
}