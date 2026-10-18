using OpticaSosa.Domain.Catalog;

namespace OpticaSosa.Domain.Commerce;

public enum OrderMoneyKind { Payment = 1, Refund = 2 }

public sealed class OrderMoneyEntry
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderMoneyKind Kind { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = "";
    public Guid? OriginalPaymentId { get; private set; }
    public string Method { get; private set; } = "";
    public string? Reference { get; private set; }
    public string? Reason { get; private set; }
    public string ActorUserId { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }

    private OrderMoneyEntry() { }
    public OrderMoneyEntry(Guid id, Guid orderId, OrderMoneyKind kind, Money amount, Guid? originalPaymentId,
        string method, string? reference, string? reason, string actor, DateTime utcNow)
    {
        SalesOrder.ValidateActor(actor, utcNow);
        if (id == Guid.Empty || orderId == Guid.Empty || !Enum.IsDefined(kind) || amount.MinorUnits <= 0 ||
            (kind == OrderMoneyKind.Refund ? originalPaymentId is null || originalPaymentId == Guid.Empty || originalPaymentId == id : originalPaymentId is not null))
            throw new ArgumentException("Revisá el pago original y el importe positivo.");
        Id = id; OrderId = orderId; Kind = kind; AmountMinor = amount.MinorUnits; Currency = amount.Currency;
        OriginalPaymentId = originalPaymentId; Method = CatalogText.Required(method, 80, "el medio de pago");
        Reference = CatalogText.Optional(reference, 160);
        Reason = kind == OrderMoneyKind.Refund ? CatalogText.Required(reason, 500, "el motivo de devolución") : null;
        ActorUserId = actor; OccurredAtUtc = utcNow;
    }
}

// El hash sólo compara reintentos: no guarda el formulario ni información clínica en un JSON genérico.
public sealed class OrderCommandReceipt
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Action { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ActorUserId { get; private set; } = "";
    public DateTime OccurredAtUtc { get; private set; }
    private OrderCommandReceipt() { }
    public OrderCommandReceipt(Guid id, Guid orderId, string action, string hash, string actor, DateTime utcNow)
    {
        SalesOrder.ValidateActor(actor, utcNow);
        if (id == Guid.Empty || orderId == Guid.Empty || hash.Length != 64 || hash.Any(x => !Uri.IsHexDigit(x)))
            throw new ArgumentException("Identificador de operación inválido.");
        Id = id; OrderId = orderId; Action = CatalogText.Required(action, 80, "la operación");
        RequestHash = hash; ActorUserId = actor; OccurredAtUtc = utcNow;
    }
}
