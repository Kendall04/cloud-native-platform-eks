namespace ShipmentService.Application.Contracts.Events;

public enum TrackingEventHandlingOutcome
{
    Applied,
    AppliedWithoutStatusChange,
    Duplicate,
    ShipmentNotFound,
    IgnoredOutOfOrder,
    RejectedInvalidTransition
}
