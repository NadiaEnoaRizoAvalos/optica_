namespace OpticaSosa.Domain.Inventory;

public sealed class StockWithdrawal
{
    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid? HoldId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public int Quantity { get; private set; }
    public StockMovementKind Kind { get; private set; }
    private StockWithdrawal() { }
    public StockWithdrawal(Guid id, Guid batch, Guid? hold, Guid? supplier, int quantity, StockMovementKind kind)
    {
        if (id == Guid.Empty || batch == Guid.Empty || hold == Guid.Empty || supplier == Guid.Empty || quantity <= 0 ||
            kind is not (StockMovementKind.WriteOff or StockMovementKind.SupplierReturn) ||
            (kind == StockMovementKind.SupplierReturn ? supplier is null : supplier is not null)) throw new ArgumentException("Baja o devolución a proveedor inválida.");
        Id = id; BatchId = batch; HoldId = hold; SupplierId = supplier; Quantity = quantity; Kind = kind;
    }
}
