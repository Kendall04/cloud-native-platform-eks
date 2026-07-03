using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ShipmentService.Infrastructure.Persistence;

namespace ShipmentService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ShipmentDbContext))]
public sealed class ShipmentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("ShipmentService.Domain.Entities.ProcessedEvent", b =>
        {
            b.Property<string>("EventId")
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<string>("EventType")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<DateTime>("ProcessedAt")
                .HasColumnType("timestamp with time zone");

            b.HasKey("EventId");

            b.ToTable("ProcessedEvents");
        });

        modelBuilder.Entity("ShipmentService.Domain.Entities.Shipment", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("CustomerId")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<string>("Destination")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<DateTime?>("LastTrackingEventAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Origin")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<string>("Priority")
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<string>("ReferenceNumber")
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)");

            b.Property<string>("TrackingNumber")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("Version")
                .IsConcurrencyToken()
                .HasColumnType("integer");

            b.Property<decimal>("Weight")
                .HasColumnType("numeric(18,2)");

            b.HasKey("Id");

            b.HasIndex("CustomerId");

            b.HasIndex("CustomerId", "Status");

            b.HasIndex("Status");

            b.HasIndex("TrackingNumber")
                .IsUnique();

            b.ToTable("Shipments");
        });
    }
}
