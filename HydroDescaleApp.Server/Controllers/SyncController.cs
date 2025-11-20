using HydroDescaleApp.Server.Data;
using HydroDescaleApp.Server.Models;
using HydroDescaleApp.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HydroDescaleApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IOracleService _oracleService;

    public SyncController(AppDbContext context, IOracleService oracleService)
    {
        _context = context;
        _oracleService = oracleService;
    }

    [HttpPost("load-steel-grades")]
    public async Task<IActionResult> LoadSteelGradesFromOracle()
    {
        try
        {
            var oracleGrades = await _oracleService.GetSteelGradesAsync();
            var existingGrades = await _context.SteelGrades
                .Select(s => s.SteelGradeName)
                .ToListAsync();

            var newGrades = oracleGrades
                .Where(g => !existingGrades.Contains(g))
                .Select(name => new SteelGrade
                {
                    SteelGradeName = name,
                    NumberOfPumps = 2,
                    PressureSetting = 18.3
                })
                .ToList();

            if (newGrades.Any())
            {
                _context.SteelGrades.AddRange(newGrades);
                await _context.SaveChangesAsync();
                return Ok(new { message = $"{newGrades.Count} new steel grades added." });
            }

            return Ok(new { message = "No new steel grades to add." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}