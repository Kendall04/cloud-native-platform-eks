using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrackingService.Application.Contracts.Events;
using TrackingService.Application.Interfaces;
using TrackingService.Domain.Entities;
using TrackingService.Infrastructure.Configuration;

namespace TrackingService.Infrastructure.Messaging;

public sealed class EventBridgeTrackingEventPublisher(
    IAmazonEventBridge eventBridge,
    IOptions<AwsOptions> awsOptions,
    IOptions<EventPublishingOptions> eventPublishingOptions,
    TimeProvider timeProvider,
    ILogger<EventBridgeTrackingEventPublisher> logger) : ITrackingEventPublisher
{
    private readonly AwsOptions _awsOptions = awsOptions.Value;
    private readonly EventPublishingOptions _eventPublishingOptions = eventPublishingOptions.Value;

    public Task<bool> PublishTrackingStatusUpdatedAsync(
        TrackingEvent trackingEvent,
        CancellationToken cancellationToken = default)
    {
        var eventData = new TrackingStatusUpdatedEventData(
            trackingEvent.ShipmentId,
            trackingEvent.Id,
            trackingEvent.Status,
            trackingEvent.Location,
            trackingEvent.OccurredAt);

        return PublishAsync("TrackingStatusUpdated", eventData, cancellationToken);
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
            "tracking-service",
            timeProvider.GetUtcNow().UtcDateTime,
            eventData);

        var detail = JsonSerializer.Serialize(envelope, TrackingEventJson.Options);

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
