using ShipmentService.Domain.Enums;

namespace ShipmentService.Application.Contracts.Events;

public sealed record ShipmentCreatedEventData(
    Guid ShipmentId,
    string TrackingNumber,
    string CustomerId,
    string Origin,
    string Destination,
    decimal Weight,
    ShipmentStatus Status,
    string? ReferenceNumber,
    string? Priority,
    DateTime CreatedAt,
    int Version);
