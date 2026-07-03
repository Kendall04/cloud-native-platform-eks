using ShipmentService.Domain.Enums;

namespace ShipmentService.Application.Contracts.Events;

public sealed record ShipmentStatusChangedEventData(
    Guid ShipmentId,
    string TrackingNumber,
    ShipmentStatus PreviousStatus,
    ShipmentStatus Status,
    DateTime EventOccurredAt,
    DateTime UpdatedAt,
    int Version);
