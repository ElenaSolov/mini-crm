using Microsoft.EntityFrameworkCore;
using MiniCrm.Api.Models;

namespace MiniCrm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<TrackerEndpoint> TrackerEndpoints => Set<TrackerEndpoint>();
    public DbSet<TrackerEvent> TrackerEvents => Set<TrackerEvent>();
    public DbSet<TrackerAttempt> TrackerAttempts => Set<TrackerAttempt>();

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

        modelBuilder.Entity<TrackerEndpoint>(endpoint =>
        {
            endpoint.HasIndex(e => e.Name).IsUnique();
            endpoint.Property(e => e.Name).HasMaxLength(50);
            endpoint.Property(e => e.Method).HasMaxLength(10);
            endpoint.Property(e => e.TriggerStatus).HasConversion<string>().HasMaxLength(20);
            endpoint.Property(e => e.UrlTemplate).HasMaxLength(2000);
            endpoint.Property(e => e.EventIdTemplate).HasMaxLength(128);
            endpoint.HasData(DefaultEndpoints.All());
        });

        modelBuilder.Entity<TrackerEvent>(trackerEvent =>
        {
            // The tracker allows event_id up to 128 characters.
            trackerEvent.HasIndex(e => e.EventId).IsUnique();
            trackerEvent.Property(e => e.EventId).HasMaxLength(128);

            // One event per lead and endpoint: converting again reuses it.
            trackerEvent.HasIndex(e => new { e.LeadId, e.TrackerEndpointId }).IsUnique();

            // Restrict: a lead with events can't be deleted, the events are the audit trail.
            trackerEvent.HasOne<Lead>().WithMany().HasForeignKey(e => e.LeadId).OnDelete(DeleteBehavior.Restrict);
            trackerEvent.HasOne(e => e.TrackerEndpoint).WithMany().HasForeignKey(e => e.TrackerEndpointId)
                .OnDelete(DeleteBehavior.Restrict);

            trackerEvent.Property(e => e.Method).HasMaxLength(10);
            trackerEvent.Property(e => e.Url).HasMaxLength(2000);
            trackerEvent.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            trackerEvent.HasIndex(e => new { e.Status, e.NextAttemptAt });
        });

        modelBuilder.Entity<TrackerAttempt>(attempt =>
        {
            attempt.Property(a => a.Url).HasMaxLength(2000);
            attempt.HasOne<TrackerEvent>().WithMany(e => e.Attempts).HasForeignKey(a => a.TrackerEventId);
        });
    }
}
