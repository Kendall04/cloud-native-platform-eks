using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShipmentService.Application.Contracts.Events;
using ShipmentService.Application.Interfaces;
using ShipmentService.Domain.Entities;
using ShipmentService.Domain.Enums;
using ShipmentService.Infrastructure.Messaging;
using ShipmentService.Infrastructure.Persistence;

namespace ShipmentService.Infrastructure.Services;

public sealed class TrackingEventProcessor(
    ShipmentDbContext dbContext,
    IShipmentEventPublisher shipmentEventPublisher,
    TimeProvider timeProvider,
    ILogger<TrackingEventProcessor> logger) : ITrackingEventProcessor
{
    public async Task<TrackingEventHandlingResult> ProcessAsync(
        string messageBody,
        CancellationToken cancellationToken = default)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope<TrackingStatusUpdatedData>>(
            messageBody,
            ShipmentEventJson.Options)
            ?? throw new InvalidOperationException("Unable to deserialize tracking event envelope.");

        if (!string.Equals(envelope.EventType, "TrackingStatusUpdated", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported event type '{envelope.EventType}'.");
        }

        logger.LogInformation(
            "Processing tracking event {EventId} for shipment {ShipmentId}",
            envelope.EventId,
            envelope.Data.ShipmentId);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingProcessedEvent = await dbContext.ProcessedEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(processedEvent => processedEvent.EventId == envelope.EventId, cancellationToken);

        if (existingProcessedEvent is not null)
        {
            logger.LogInformation(
                "Skipping duplicate tracking event {EventId}",
                envelope.EventId);

            await transaction.CommitAsync(cancellationToken);

            return new TrackingEventHandlingResult(
                envelope.EventId,
                envelope.EventType,
                TrackingEventHandlingOutcome.Duplicate,
                envelope.Data.ShipmentId);
        }

        var processedEvent = new ProcessedEvent
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            ProcessedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        var shipment = await dbContext.Shipments
            .SingleOrDefaultAsync(candidate => candidate.Id == envelope.Data.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            logger.LogWarning(
                "Tracking event {EventId} referenced missing shipment {ShipmentId}",
                envelope.EventId,
                envelope.Data.ShipmentId);

            dbContext.ProcessedEvents.Add(processedEvent);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new TrackingEventHandlingResult(
                envelope.EventId,
                envelope.EventType,
                TrackingEventHandlingOutcome.ShipmentNotFound,
                envelope.Data.ShipmentId);
        }

        var previousStatus = shipment.Status;
        var outcome = shipment.ApplyTrackingStatusUpdate(
            envelope.Data.Status,
            envelope.Data.EventOccurredAt,
            timeProvider.GetUtcNow().UtcDateTime);

        switch (outcome)
        {
            case TrackingStatusUpdateOutcome.IgnoredOutOfOrder:
                logger.LogWarning(
                    "Ignored out-of-order tracking event {EventId} for shipment {ShipmentId}. EventOccurredAt={EventOccurredAt} LastTrackingEventAt={LastTrackingEventAt}",
                    envelope.EventId,
                    shipment.Id,
                    envelope.Data.EventOccurredAt,
                    shipment.LastTrackingEventAt);
                break;
            case TrackingStatusUpdateOutcome.RejectedInvalidTransition:
                logger.LogWarning(
                    "Rejected invalid tracking transition for shipment {ShipmentId}. CurrentStatus={CurrentStatus} IncomingStatus={IncomingStatus} EventId={EventId}",
                    shipment.Id,
                    shipment.Status,
                    envelope.Data.Status,
                    envelope.EventId);
                break;
        }

        dbContext.ProcessedEvents.Add(processedEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var handlingOutcome = MapOutcome(outcome);

        if (handlingOutcome == TrackingEventHandlingOutcome.Applied)
        {
            var published = await shipmentEventPublisher.PublishShipmentStatusChangedAsync(
                shipment,
                previousStatus,
                cancellationToken);

            if (!published)
            {
                logger.LogError(
                    "ShipmentStatusChanged event publication failed after applying tracking event {EventId} to shipment {ShipmentId}. The shipment status update remains committed and a future outbox can take over this path.",
                    envelope.EventId,
                    shipment.Id);
            }
        }

        return new TrackingEventHandlingResult(
            envelope.EventId,
            envelope.EventType,
            handlingOutcome,
            shipment.Id);
    }

    private static TrackingEventHandlingOutcome MapOutcome(TrackingStatusUpdateOutcome outcome) =>
        outcome switch
        {
            TrackingStatusUpdateOutcome.Applied => TrackingEventHandlingOutcome.Applied,
            TrackingStatusUpdateOutcome.AppliedWithoutStatusChange => TrackingEventHandlingOutcome.AppliedWithoutStatusChange,
            TrackingStatusUpdateOutcome.IgnoredOutOfOrder => TrackingEventHandlingOutcome.IgnoredOutOfOrder,
            TrackingStatusUpdateOutcome.RejectedInvalidTransition => TrackingEventHandlingOutcome.RejectedInvalidTransition,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown tracking update outcome.")
        };
}
