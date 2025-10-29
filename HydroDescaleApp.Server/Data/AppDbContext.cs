
using HydroDescaleApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace HydroDescaleApp.Server.Data
{
  public class AppDbContext : DbContext
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SteelGrade> SteelGrades { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.Entity<SteelGrade>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.PressureSetting).HasColumnType("decimal(5,2)");
        entity.HasIndex(e => e.SteelGradeName).IsUnique();
      });
    }
  }
}
