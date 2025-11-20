using S7.Net;
using Microsoft.Extensions.Logging;

namespace HydroDescaleApp.Server.Services;

public interface IDiagnosticService
{
    Task<(bool success, short value, string error)> ReadFurnaceNumberAsync();
    Task<(bool success, string error)> WriteTestValuesAsync(int pumps, double pressure);
}

public class DiagnosticService : IDiagnosticService
{
    private readonly string _plcReadIp;
    private readonly string _plcWriteIp;
    private readonly ILogger<DiagnosticService> _logger;

    public DiagnosticService(IConfiguration configuration, ILogger<DiagnosticService> logger)
    {
        _plcReadIp = configuration["Plc:ReadIp"] ?? "127.0.0.1";
        _plcWriteIp = configuration["Plc:WriteIp"] ?? "127.0.0.1";
        _logger = logger;
    }

    public async Task<(bool success, short value, string error)> ReadFurnaceNumberAsync()
    {
        using var plc = new Plc(CpuType.S7400, _plcReadIp, 0, 3);
        try
        {
            await plc.OpenAsync();
            var value1 = plc.Read("DB550.DBB0"); // Пример: читаем DB855.DBW0
            var value = Convert.ToInt16(value1);
            _logger.LogInformation("Successfully read DB855.DBW0 = {Value}", value);
            return (true, value, string.Empty);
        }
        catch (Exception ex)
        {
            var error = $"Failed to read from PLC at {_plcReadIp}: {ex.Message}";
            _logger.LogError(ex, error);
            return (false, 0, error);
        }
    }

    public async Task<(bool success, string error)> WriteTestValuesAsync(int pumps, double pressure)
    {
        using var plc = new Plc(CpuType.S7400, _plcWriteIp, 0,3);
        try
        {
            await plc.OpenAsync();

            // Читаем текущий watchdog
            var currentWatchdog = (ushort)plc.Read("DB14.DBW0");
            var newWatchdog = (ushort)((currentWatchdog + 1) & 0xFFFF);

            // Записываем тестовые значения
            plc.Write("DB14.DBW0", newWatchdog); // Watchdog
            plc.Write("DB14.DBW2", true); // Link_service_OK
            plc.Write("DB14.DBW4", (short)pumps); // Count_Pump_Furnace_DSC
            plc.Write("DB14.DBW6", (float)pressure); // Reference_PS_Furnace_DSC (REAL занимает 4 байта: DBD6)

            _logger.LogInformation("Successfully wrote test values to PLC: Pumps={Pumps}, Pressure={Pressure}, Watchdog={Watchdog}", pumps, pressure, newWatchdog);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            var error = $"Failed to write to PLC at {_plcWriteIp}: {ex.Message}";
            _logger.LogError(ex, error);
            return (false, error);
        }
    }
}