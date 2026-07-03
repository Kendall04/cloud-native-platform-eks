using ShipmentService.Domain.Entities;
using ShipmentService.Domain.Enums;

namespace ShipmentService.Application.Interfaces;

public interface IShipmentEventPublisher
{
    // This interface is intentionally narrow so a future outbox-backed publisher can replace
    // the direct EventBridge implementation without changing application services.
    Task<bool> PublishShipmentCreatedAsync(Shipment shipment, CancellationToken cancellationToken = default);

    Task<bool> PublishShipmentStatusChangedAsync(
        Shipment shipment,
        ShipmentStatus previousStatus,
        CancellationToken cancellationToken = default);
}
