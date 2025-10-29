using HydroDescaleApp.Server.Data;
using HydroDescaleApp.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HydroDescaleApp.Server.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class SteelGradesController : ControllerBase
  {
    private readonly AppDbContext _context;

    public SteelGradesController(AppDbContext context)
    {
      _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SteelGrade>>> GetSteelGrades()
    {
      var grades = await _context.SteelGrades.ToListAsync();
      return Ok(grades);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SteelGrade>> GetSteelGrade(int id)
    {
      var grade = await _context.SteelGrades.FindAsync(id);
      if (grade == null) return NotFound();
      return Ok(grade);
    }

    [HttpPost]
    public async Task<ActionResult<SteelGrade>> CreateSteelGrade(SteelGrade grade)
    {
      grade.CreatedAt = DateTime.UtcNow;
      grade.UpdatedAt = DateTime.UtcNow;
      _context.SteelGrades.Add(grade);
      await _context.SaveChangesAsync();
      return CreatedAtAction(nameof(GetSteelGrade), new { id = grade.Id }, grade);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSteelGrade(int id, SteelGrade grade)
    {
      if (id != grade.Id) return BadRequest();
      grade.UpdatedAt = DateTime.UtcNow;
      _context.Entry(grade).State = EntityState.Modified;
      await _context.SaveChangesAsync();
      return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSteelGrade(int id)
    {
      var grade = await _context.SteelGrades.FindAsync(id);
      if (grade == null) return NotFound();
      _context.SteelGrades.Remove(grade);
      await _context.SaveChangesAsync();
      return NoContent();
    }
  }
}
