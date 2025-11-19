
using HydroDescaleApp.Server.Data;
using HydroDescaleApp.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace HydroDescaleApp.Server
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      // Add services
      builder.Services.AddControllers();
      builder.Services.AddEndpointsApiExplorer();
      builder.Services.AddSwaggerGen();

      builder.Services.AddDbContext<AppDbContext>(options =>
          options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

      builder.Services.AddScoped<IOracleService, OracleService>();
      builder.Services.AddScoped<IPlcService, PlcService>();
      builder.Services.AddScoped<IDiagnosticService, DiagnosticService>();

   
builder.Services.AddSingleton<IPlcConnectionStateService, PlcConnectionStateService>();
      builder.Services.AddHostedService<PlcPollingService>();

      var app = builder.Build();

      // Initialize DB
      using (var scope = app.Services.CreateScope())
      {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
      }

      // Configure pipeline
      if (app.Environment.IsDevelopment())
      {
        app.UseSwagger();
        app.UseSwaggerUI();
      }

      app.UseHttpsRedirection();
      app.MapControllers();

      app.Run();
    }
  }
}
