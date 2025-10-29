using System.ComponentModel.DataAnnotations;
namespace HydroDescaleApp.Server.Models
{
  public class SteelGrade
  {
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string SteelGradeName { get; set; } = string.Empty;

    public int NumberOfPumps { get; set; } = 2;

    public double PressureSetting { get; set; } = 18.3;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
  }
}
