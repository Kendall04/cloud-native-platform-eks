using System.ComponentModel.DataAnnotations;

namespace ShipmentService.Infrastructure.Configuration;

public sealed class EventPublishingOptions
{
    public const string SectionName = "EventPublishing";

    [Range(1, 5)]
    public int MaxAttempts { get; set; } = 3;

    [Range(50, 5000)]
    public int BaseDelayMilliseconds { get; set; } = 200;
}
