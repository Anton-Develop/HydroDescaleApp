using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace HydroDescaleApp.Server.Services
{
  public interface IOracleService
  {
    Task<List<string>> GetSteelGradesAsync();
    Task<string?> GetSteelGradeByFurnaceAsync(int furnaceNumber);
    Task<string?> GetSteelGradeByPositionAsync(int position);
  }

  public class OracleService : IOracleService
  {
    private readonly string _connectionString;

    public OracleService(IConfiguration configuration)
    {
      _connectionString = configuration.GetConnectionString("OracleConnection") ?? throw new ArgumentNullException("Oracle connection string is not configured.");
    }

    public async Task<List<string>> GetSteelGradesAsync()
    {
      var grades = new List<string>();
      using var connection = new OracleConnection(_connectionString);
      await connection.OpenAsync();

      using var command = new OracleCommand("SELECT ps.SteelGrade FROM pdi_sl ps GROUP BY ps.SteelGrade", connection);
      using var reader = await command.ExecuteReaderAsync();

      while (await reader.ReadAsync())
      {
        grades.Add(reader.GetString("SteelGrade"));
      }

      return grades;
    }

    public async Task<string?> GetSteelGradeByFurnaceAsync(int furnaceNumber)
    {
      using var connection = new OracleConnection(_connectionString);
      await connection.OpenAsync();

      using var command = new OracleCommand("SELECT SteelGrade FROM View_Descaling_Servise WHERE furn_num = :furnNum", connection);
      command.Parameters.Add(new OracleParameter("furnNum", OracleDbType.Int32) { Value = furnaceNumber });

      var result = await command.ExecuteScalarAsync();
      return result?.ToString();
    }
    
    public async Task<string?> GetSteelGradeByPositionAsync(int position)
    {
        using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new OracleCommand("SELECT SteelGrade FROM View_Descaling_Servise WHERE pos = :pos", connection);
        command.Parameters.Add(new OracleParameter("pos", OracleDbType.Int32) { Value = position });

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }
  }
}
