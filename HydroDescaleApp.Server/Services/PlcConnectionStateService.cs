using System;

namespace HydroDescaleApp.Server.Services;

public interface IPlcConnectionStateService
{
    bool IsConnected { get; }
    DateTime LastSuccessfulWrite { get; }
    string LastErrorMessage { get; }
    void UpdateConnectionState(bool success, string? errorMessage = null);
}

public class PlcConnectionStateService : IPlcConnectionStateService
{
    private readonly ILogger<PlcConnectionStateService> _logger;
    private readonly object _lock = new();

    public bool IsConnected { get; private set; } = false;
    public DateTime LastSuccessfulWrite { get; private set; } = DateTime.MinValue;
    public string LastErrorMessage { get; private set; } = string.Empty;

    public PlcConnectionStateService(ILogger<PlcConnectionStateService> logger)
    {
        _logger = logger;
    }

    public void UpdateConnectionState(bool success, string? errorMessage = null)
    {
        lock (_lock)
        {
            if (success)
            {
                IsConnected = true;
                LastSuccessfulWrite = DateTime.UtcNow;
                LastErrorMessage = string.Empty;
                _logger.LogInformation("PLC connection is OK.");
            }
            else
            {
                IsConnected = false;
                LastErrorMessage = errorMessage ?? "Unknown error";
                _logger.LogWarning("PLC connection lost. Error: {ErrorMessage}", errorMessage);
            }
        }
    }
}
