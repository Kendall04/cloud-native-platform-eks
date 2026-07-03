using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShipmentService.Application.Contracts.Events;
using ShipmentService.Application.Interfaces;
using ShipmentService.Domain.Entities;
using ShipmentService.Domain.Enums;
using ShipmentService.Infrastructure.Persistence;
using ShipmentService.Infrastructure.Services;

namespace ShipmentService.Tests.Processing;

public sealed class TrackingEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_SkipsDuplicateEventsIdempotently()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var createdAt = new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc);
        var shipment = Shipment.Create(
            "SHP-DUPLICATE0001",
            "cust-123",
            "San Jose",
            "Tokyo",
            3.5m,
            null,
            null,
            createdAt);

        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();

        var eventPublisher = new FakeShipmentEventPublisher();
        var processor = new TrackingEventProcessor(
            dbContext,
            eventPublisher,
            new FrozenTimeProvider(createdAt.AddMinutes(10)),
            NullLogger<TrackingEventProcessor>.Instance);

        var messageBody = SerializeEnvelope(
            new EventEnvelope<TrackingStatusUpdatedData>(
                "evt-1",
                "TrackingStatusUpdated",
                "1.0",
                "tracking-service",
                createdAt.AddMinutes(5),
                new TrackingStatusUpdatedData(
                    shipment.Id,
                    "trk-evt-1",
                    ShipmentStatus.PICKED_UP,
                    "San Jose",
                    createdAt.AddMinutes(5))));

        var firstResult = await processor.ProcessAsync(messageBody);
        var secondResult = await processor.ProcessAsync(messageBody);

        var reloadedShipment = await dbContext.Shipments.SingleAsync(candidate => candidate.Id == shipment.Id);

        Assert.Equal(TrackingEventHandlingOutcome.Applied, firstResult.Outcome);
        Assert.Equal(TrackingEventHandlingOutcome.Duplicate, secondResult.Outcome);
        Assert.Equal(ShipmentStatus.PICKED_UP, reloadedShipment.Status);
        Assert.Equal(2, reloadedShipment.Version);
        Assert.Equal(1, await dbContext.ProcessedEvents.CountAsync());
        Assert.Equal(1, eventPublisher.ShipmentStatusChangedCalls);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresOutOfOrderEventsSafely()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var createdAt = new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc);
        var shipment = Shipment.Create(
            "SHP-ORDER0000001",
            "cust-123",
            "San Jose",
            "Tokyo",
            3.5m,
            null,
            null,
            createdAt);

        shipment.ApplyTrackingStatusUpdate(ShipmentStatus.PICKED_UP, createdAt.AddMinutes(5), createdAt.AddMinutes(5));
        shipment.ApplyTrackingStatusUpdate(ShipmentStatus.IN_TRANSIT, createdAt.AddMinutes(10), createdAt.AddMinutes(10));

        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync();

        var eventPublisher = new FakeShipmentEventPublisher();
        var processor = new TrackingEventProcessor(
            dbContext,
            eventPublisher,
            new FrozenTimeProvider(createdAt.AddMinutes(20)),
            NullLogger<TrackingEventProcessor>.Instance);

        var messageBody = SerializeEnvelope(
            new EventEnvelope<TrackingStatusUpdatedData>(
                "evt-2",
                "TrackingStatusUpdated",
                "1.0",
                "tracking-service",
                createdAt.AddMinutes(8),
                new TrackingStatusUpdatedData(
                    shipment.Id,
                    "trk-evt-2",
                    ShipmentStatus.OUT_FOR_DELIVERY,
                    "Los Angeles",
                    createdAt.AddMinutes(8))));

        var result = await processor.ProcessAsync(messageBody);
        var reloadedShipment = await dbContext.Shipments.SingleAsync(candidate => candidate.Id == shipment.Id);

        Assert.Equal(TrackingEventHandlingOutcome.IgnoredOutOfOrder, result.Outcome);
        Assert.Equal(ShipmentStatus.IN_TRANSIT, reloadedShipment.Status);
        Assert.Equal(createdAt.AddMinutes(10), reloadedShipment.LastTrackingEventAt);
        Assert.Equal(3, reloadedShipment.Version);
        Assert.Equal(1, await dbContext.ProcessedEvents.CountAsync());
        Assert.Equal(0, eventPublisher.ShipmentStatusChangedCalls);
    }

    private static ShipmentDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ShipmentDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ShipmentDbContext(options);
    }

    private static string SerializeEnvelope(EventEnvelope<TrackingStatusUpdatedData> envelope)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(envelope, options);
    }

    private sealed class FakeShipmentEventPublisher : IShipmentEventPublisher
    {
        public int ShipmentCreatedCalls { get; private set; }

        public int ShipmentStatusChangedCalls { get; private set; }

        public Task<bool> PublishShipmentCreatedAsync(Shipment shipment, CancellationToken cancellationToken = default)
        {
            ShipmentCreatedCalls += 1;
            return Task.FromResult(true);
        }

        public Task<bool> PublishShipmentStatusChangedAsync(
            Shipment shipment,
            ShipmentStatus previousStatus,
            CancellationToken cancellationToken = default)
        {
            ShipmentStatusChangedCalls += 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FrozenTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
