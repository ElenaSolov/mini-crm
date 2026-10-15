using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>(lead =>
        {
            lead.Property(l => l.Name).HasMaxLength(200);
            lead.Property(l => l.Email).HasMaxLength(200);
            lead.Property(l => l.Phone).HasMaxLength(32);
            lead.Property(l => l.Source).HasMaxLength(100);
            lead.Property(l => l.Amount).HasPrecision(18, 2);
            lead.Property(l => l.Currency).HasMaxLength(3);
            lead.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            lead.Property(l => l.RowVersion).IsRowVersion();
        });
    }
}
