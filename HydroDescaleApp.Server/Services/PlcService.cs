using Microsoft.EntityFrameworkCore.Storage;
using S7.Net;

namespace HydroDescaleApp.Server.Services
{
  public interface IPlcService
{
    Task<int> ReadFurnaceNumberAsync();
    Task WriteDescaleSettingsAsync(int pumps, double pressure);
}

public class PlcService : IPlcService
{
    private readonly string _plcIpWrite;
    private readonly IPlcConnectionStateService _connectionStateService;
    private readonly ILogger<PlcService> _logger;
    private const int DBWrite = 14; // DB14
    private const int DBRead = 14;

    public PlcService(IConfiguration configuration, IPlcConnectionStateService connectionStateService, ILogger<PlcService> logger)
    {
        _plcIpWrite = configuration["Plc:WriteIp"] ?? "127.0.0.1";
        _connectionStateService = connectionStateService;
        _logger = logger;
    }

    public async Task WriteDescaleSettingsAsync(int pumps, double pressure)
    {
        Plc? plc = null;
        bool success = false;
        string? errorMessage = null;

        try
        {
            plc = new Plc(CpuType.S7400, _plcIpWrite, 0, 2);
            await plc.OpenAsync();

            // Читаем текущее значение Watchdog (DB14.DBW0)
            var currentWatchdog = (ushort)plc.Read($"{DBWrite}.DBW0");

            // Увеличиваем watchdog на 1
            var newWatchdog = (ushort)((currentWatchdog + 1) & 0xFFFF);

            // Записываем новые значения
            plc.Write($"{DBWrite}.DBW0", newWatchdog); // Watchdog
            plc.Write($"{DBWrite}.DBW2", true); // Link_service_OK (BOOL) — устанавливаем в TRUE
            plc.Write($"{DBWrite}.DBW4", (short)pumps); // Count_Pump_Furnace_DSC (INT)
            plc.Write($"{DBWrite}.DBD6", (float)pressure); // Reference_PS_Furnace_DSC (REAL)

            _logger.LogInformation("Successfully wrote to PLC: Watchdog={Watchdog}, Pumps={Pumps}, Pressure={Pressure}", newWatchdog, pumps, pressure);
            success = true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Error writing to PLC DB{DBWrite}: {Message}", DBWrite, ex.Message);

            // Установим Link_service_OK в FALSE
            await SetLinkServiceOk(false);
        }
        finally
        {
            plc?.Close();
        }

        _connectionStateService.UpdateConnectionState(success, errorMessage);
    }

    private async Task SetLinkServiceOk(bool value)
    {
        Plc? plc = null;
        try
        {
            plc = new Plc(CpuType.S7400, _plcIpWrite, 0, 2);
            await plc.OpenAsync();
            plc.Write($"{DBWrite}.DBW2", value); // Link_service_OK
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Link_service_OK to {Value}", value);
        }
        finally
        {
            plc?.Close();
        }
    }

    public async Task<int> ReadFurnaceNumberAsync()
    {
        using var plc = new Plc(CpuType.S7400, _plcIpWrite, 0, 2);
        try
        {
            await plc.OpenAsync();
            var value = (short)plc.Read($"{DBRead}.DBW0");
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading furnace number from PLC.");
            return 0;
        }
    }
}
}
