using Microsoft.EntityFrameworkCore.Storage;
using S7.Net;

namespace HydroDescaleApp.Server.Services
{
  public interface IPlcService
  {
   // Task<int> ReadFurnaceNumberAsync();
    Task WriteDescaleSettingsAsync(int pumps, double pressure);
  }

  public class PlcService : IPlcService
  {
    private readonly string _plcIpRead;
    private readonly string _plcIpWrite;
    private const int DBRead = 855;
    private const int DBWrite = 900;
    

    public PlcService(IConfiguration configuration)
    {
      _plcIpRead = configuration["Plc:ReadIp"] ?? "127.0.0.1";
      _plcIpWrite = configuration["Plc:WriteIp"] ?? "127.0.0.1";
    }

   /* public async Task<int> ReadFurnaceNumberAsync()
    {
      using var plc = new Plc(CpuType.S7400, _plcIpRead, 0, 2);
      try
      {
        await plc.OpenAsync();
        var value = (short)plc.Read($"{DBRead}.DBW0");
        return value;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error reading PLC: {ex.Message}");
        return 0;
      }
    }
*/
    public async Task WriteDescaleSettingsAsync(int pumps, double pressure)
    {
      using var plc = new Plc(CpuType.S7400, _plcIpWrite, 0, 2);
      try
      {
        await plc.OpenAsync();
        plc.Write($"{DBWrite}.DBW0", (short)pumps);
        plc.Write($"{DBWrite}.DBW2", (float)pressure);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error writing to PLC: {ex.Message}");
      }
    }
  }
}
