using System.ComponentModel.DataAnnotations;

namespace ShipmentService.Infrastructure.Configuration;

public sealed class ShipmentConsumerOptions
{
    public const string SectionName = "ShipmentConsumer";

    [Range(1, 10)]
    public int MaxMessagesPerPoll { get; set; } = 5;

    [Range(1, 20)]
    public int WaitTimeSeconds { get; set; } = 20;

    [Range(1, 300)]
    public int FailureBackoffSeconds { get; set; } = 5;
}
