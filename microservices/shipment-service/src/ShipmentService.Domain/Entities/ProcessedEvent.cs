namespace ShipmentService.Domain.Entities;

public sealed class ProcessedEvent
{
    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
