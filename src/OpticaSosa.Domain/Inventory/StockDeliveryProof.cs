using OpticaSosa.Domain.Work;

namespace OpticaSosa.Domain.Inventory;

// Consumir para un trabajo no demuestra entrega al cliente. La fase 10 registra esta relación al entregar.
public sealed class StockDeliveryProof
{
    public Guid ReservationId { get; private set; }
    public Guid WorkId { get; private set; }
    private StockDeliveryProof() { }
    public StockDeliveryProof(StockReservation reservation, OpticalWork work)
    {
        if (reservation.Status != ReservationStatus.Consumed || work.Status != WorkStatus.Delivered)
            throw new ArgumentException("La devolución al cliente requiere una entrega real con sus controles aprobados.");
        ReservationId = reservation.Id; WorkId = work.Id;
    }
}
