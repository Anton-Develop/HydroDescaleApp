using HydroDescaleApp.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HydroDescaleApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticController : ControllerBase
{
    private readonly IDiagnosticService _diagnosticService;

    public DiagnosticController(IDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
    }

    [HttpGet("read-furnace")]
    public async Task<IActionResult> ReadFurnaceNumber()
    {
        var (success, value, error) = await _diagnosticService.ReadFurnaceNumberAsync();
        if (success)
        {
            return Ok(new { success = true, value = value });
        }
        return BadRequest(new { success = false, error = error });
    }

    [HttpPost("write-test")]
    public async Task<IActionResult> WriteTestValues([FromBody] WriteTestRequest request)
    {
        var (success, error) = await _diagnosticService.WriteTestValuesAsync(request.Pumps, request.Pressure);
        if (success)
        {
            return Ok(new { success = true });
        }
        return BadRequest(new { success = false, error = error });
    }
}

public class WriteTestRequest
{
    public int Pumps { get; set; } = 2;
    public double Pressure { get; set; } = 18.3;
}