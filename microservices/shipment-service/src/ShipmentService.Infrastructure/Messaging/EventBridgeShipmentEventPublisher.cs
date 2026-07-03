using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipmentService.Application.Contracts.Events;
using ShipmentService.Application.Interfaces;
using ShipmentService.Domain.Entities;
using ShipmentService.Domain.Enums;
using ShipmentService.Infrastructure.Configuration;

namespace ShipmentService.Infrastructure.Messaging;

public sealed class EventBridgeShipmentEventPublisher(
    IAmazonEventBridge eventBridge,
    IOptions<AwsOptions> awsOptions,
    IOptions<EventPublishingOptions> eventPublishingOptions,
    TimeProvider timeProvider,
    ILogger<EventBridgeShipmentEventPublisher> logger) : IShipmentEventPublisher
{
    private readonly AwsOptions _awsOptions = awsOptions.Value;
    private readonly EventPublishingOptions _eventPublishingOptions = eventPublishingOptions.Value;

    public Task<bool> PublishShipmentCreatedAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        var eventData = new ShipmentCreatedEventData(
            shipment.Id,
            shipment.TrackingNumber,
            shipment.CustomerId,
            shipment.Origin,
            shipment.Destination,
            shipment.Weight,
            shipment.Status,
            shipment.ReferenceNumber,
            shipment.Priority,
            shipment.CreatedAt,
            shipment.Version);

        return PublishAsync("ShipmentCreated", eventData, cancellationToken);
    }

    public Task<bool> PublishShipmentStatusChangedAsync(
        Shipment shipment,
        ShipmentStatus previousStatus,
        CancellationToken cancellationToken = default)
    {
        var eventData = new ShipmentStatusChangedEventData(
            shipment.Id,
            shipment.TrackingNumber,
            previousStatus,
            shipment.Status,
            shipment.LastTrackingEventAt ?? shipment.UpdatedAt,
            shipment.UpdatedAt,
            shipment.Version);

        return PublishAsync("ShipmentStatusChanged", eventData, cancellationToken);
    }

    private async Task<bool> PublishAsync<T>(
        string eventType,
        T eventData,
        CancellationToken cancellationToken)
    {
        var envelope = new EventEnvelope<T>(
            Guid.NewGuid().ToString(),
            eventType,
            "1.0",
            "shipment-service",
            timeProvider.GetUtcNow().UtcDateTime,
            eventData);

        var detail = JsonSerializer.Serialize(envelope, ShipmentEventJson.Options);

        for (var attempt = 1; attempt <= _eventPublishingOptions.MaxAttempts; attempt++)
        {
            try
            {
                var response = await eventBridge.PutEventsAsync(
                    new PutEventsRequest
                    {
                        Entries =
                        [
                            new PutEventsRequestEntry
                            {
                                EventBusName = _awsOptions.EventBusName,
                                Source = envelope.Source,
                                DetailType = envelope.EventType,
                                Time = envelope.Timestamp,
                                Detail = detail
                            }
                        ]
                    },
                    cancellationToken);

                if (response.FailedEntryCount == 0)
                {
                    logger.LogInformation(
                        "Published EventBridge event {EventType} with id {EventId} on attempt {Attempt}",
                        envelope.EventType,
                        envelope.EventId,
                        attempt);

                    return true;
                }

                var failedEntry = response.Entries.FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.ErrorCode));
                logger.LogError(
                    "EventBridge rejected event {EventType} with id {EventId} on attempt {Attempt}. ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                    envelope.EventType,
                    envelope.EventId,
                    attempt,
                    failedEntry?.ErrorCode,
                    failedEntry?.ErrorMessage);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "EventBridge call failed for event {EventType} with id {EventId} on attempt {Attempt}",
                    envelope.EventType,
                    envelope.EventId,
                    attempt);
            }

            if (attempt < _eventPublishingOptions.MaxAttempts)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(_eventPublishingOptions.BaseDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        logger.LogError(
            "Giving up publishing event {EventType} with id {EventId} after {MaxAttempts} attempts. The current implementation publishes after the database commit, so a future outbox should take over this path.",
            envelope.EventType,
            envelope.EventId,
            _eventPublishingOptions.MaxAttempts);

        return false;
    }
}
