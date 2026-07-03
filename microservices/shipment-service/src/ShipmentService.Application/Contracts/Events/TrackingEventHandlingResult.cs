namespace ShipmentService.Application.Contracts.Events;

public sealed record TrackingEventHandlingResult(
    string EventId,
    string EventType,
    TrackingEventHandlingOutcome Outcome,
    Guid? ShipmentId = null);
