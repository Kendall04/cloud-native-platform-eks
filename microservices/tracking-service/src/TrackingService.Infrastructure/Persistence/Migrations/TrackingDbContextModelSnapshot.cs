using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TrackingService.Infrastructure.Persistence;

namespace TrackingService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TrackingDbContext))]
public sealed class TrackingDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("TrackingService.Domain.Entities.TrackingEvent", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("CreatedBy")
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<string>("Location")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<string>("Notes")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.Property<DateTime>("OccurredAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("SequenceNumber")
                .HasColumnType("integer");

            b.Property<Guid>("ShipmentId")
                .HasColumnType("uuid");

            b.Property<string>("SourceType")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)");

            b.HasKey("Id");

            b.HasIndex("OccurredAt");

            b.HasIndex("ShipmentId");

            b.HasIndex("ShipmentId", "OccurredAt");

            b.HasIndex("ShipmentId", "SequenceNumber")
                .IsUnique();

            b.ToTable("TrackingEvents");
        });
    }
}
