using TrackingService.Domain.Entities;

namespace TrackingService.Application.Interfaces;

public interface ITrackingEventPublisher
{
    // This interface is intentionally narrow so a future outbox-backed publisher can replace
    // the direct EventBridge implementation without changing application services.
    Task<bool> PublishTrackingStatusUpdatedAsync(
        TrackingEvent trackingEvent,
        CancellationToken cancellationToken = default);
}
