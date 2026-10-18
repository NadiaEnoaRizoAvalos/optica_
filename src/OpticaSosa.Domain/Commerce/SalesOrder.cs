using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Commerce;

public enum SalesOrderStatus { Draft = 1, Confirmed = 2, Cancelled = 3 }
public enum FulfilmentSource { Stock = 1, Laboratory = 2 }

public sealed class SalesOrder
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Currency { get; private set; } = "";
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;
    public int DraftNumber { get; private set; }
    public long TotalMinor { get; private set; }
    public long ReceivedMinor { get; private set; }
    public long RefundedMinor { get; private set; }
    public DateOnly? PromisedDate { get; private set; }
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public string? ConfirmedByUserId { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public string? CancelledByUserId { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public long NetPaidMinor => ReceivedMinor - RefundedMinor;
    public long BalanceMinor => TotalMinor - NetPaidMinor;

    private SalesOrder() { }
    public SalesOrder(Guid id, Guid customer, string currency, string actor, DateTime utcNow)
    {
        ValidateActor(actor, utcNow);
        if (id == Guid.Empty || customer == Guid.Empty) throw new ArgumentException("Identificación inválida.");
        Id = id; CustomerId = customer; Currency = Money.CurrencyCode(currency);
        CreatedByUserId = actor; CreatedAtUtc = utcNow; Version = Guid.NewGuid();
    }
    public void Revise(long totalMinor, DateOnly? promisedDate)
    {
        if (Status != SalesOrderStatus.Draft) throw new ArgumentException("Las líneas y precios sólo se editan en borrador.");
        if (totalMinor < 0) throw new ArgumentException("El total no puede ser negativo.");
        TotalMinor = totalMinor; PromisedDate = promisedDate; DraftNumber = checked(DraftNumber + 1); Touch();
    }
    public void Confirm(string actor, DateTime utcNow, int lineCount)
    {
        ValidateActor(actor, utcNow);
        if (Status != SalesOrderStatus.Draft || lineCount <= 0) throw new ArgumentException("Confirmá un borrador con al menos una línea.");
        Status = SalesOrderStatus.Confirmed; ConfirmedByUserId = actor; ConfirmedAtUtc = utcNow; Touch();
    }
    public void Receive(long amountMinor)
    {
        if (Status != SalesOrderStatus.Confirmed || amountMinor <= 0 || amountMinor > BalanceMinor)
            throw new ArgumentException("El pago debe ser positivo y no superar el saldo de un pedido confirmado.");
        ReceivedMinor = checked(ReceivedMinor + amountMinor); Touch();
    }
    public void Refund(long amountMinor)
    {
        if (Status != SalesOrderStatus.Confirmed || amountMinor <= 0 || amountMinor > NetPaidMinor)
            throw new ArgumentException("La devolución no puede superar lo cobrado pendiente de devolver.");
        RefundedMinor = checked(RefundedMinor + amountMinor); Touch();
    }
    public void Cancel(string actor, DateTime utcNow, string reason)
    {
        ValidateActor(actor, utcNow);
        if (Status == SalesOrderStatus.Cancelled || NetPaidMinor != 0)
            throw new ArgumentException("Devolvé explícitamente los pagos antes de cerrar la cancelación.");
        CancellationReason = CatalogText.Required(reason, 500, "el motivo de cancelación");
        Status = SalesOrderStatus.Cancelled; CancelledByUserId = actor; CancelledAtUtc = utcNow; Touch();
    }
    private void Touch() => Version = Guid.NewGuid();
    internal static void ValidateActor(string actor, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(actor) || utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("Actor o fecha inválidos.");
    }
}
