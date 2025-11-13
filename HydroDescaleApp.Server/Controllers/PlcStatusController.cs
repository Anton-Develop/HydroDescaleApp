using HydroDescaleApp.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroDescaleApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlcStatusController : ControllerBase
{
    private readonly IPlcConnectionStateService _connectionStateService;

    public PlcStatusController(IPlcConnectionStateService connectionStateService)
    {
        _connectionStateService = connectionStateService;
    }

    [HttpGet]
    public IActionResult GetStatus()
    {
        var status = new
        {
            IsConnected = _connectionStateService.IsConnected,
            LastSuccessfulWrite = _connectionStateService.LastSuccessfulWrite,
            LastErrorMessage = _connectionStateService.LastErrorMessage
        };

        return Ok(status);
    }
}