using Microsoft.EntityFrameworkCore;
using TrackingService.Domain.Entities;

namespace TrackingService.Infrastructure.Persistence;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : DbContext(options)
{
    public DbSet<TrackingEvent> TrackingEvents => Set<TrackingEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TrackingEvent>(entity =>
        {
            entity.ToTable("TrackingEvents");

            entity.HasKey(trackingEvent => trackingEvent.Id);

            entity.Property(trackingEvent => trackingEvent.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(trackingEvent => trackingEvent.Location)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(trackingEvent => trackingEvent.Notes)
                .HasMaxLength(500);

            entity.Property(trackingEvent => trackingEvent.SourceType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(trackingEvent => trackingEvent.CreatedBy)
                .HasMaxLength(100);

            entity.HasIndex(trackingEvent => trackingEvent.ShipmentId);
            entity.HasIndex(trackingEvent => trackingEvent.OccurredAt);
            entity.HasIndex(trackingEvent => new { trackingEvent.ShipmentId, trackingEvent.OccurredAt });
            entity.HasIndex(trackingEvent => new { trackingEvent.ShipmentId, trackingEvent.SequenceNumber })
                .IsUnique();
        });
    }
}
