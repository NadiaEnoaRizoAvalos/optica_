namespace OpticaSosa.Domain.Catalog;

public sealed record Money
{
    public long MinorUnits { get; }
    public string Currency { get; }
    public decimal Amount => MinorUnits / 100m;
    public Money(long minorUnits, string currency)
    {
        if (minorUnits < 0) throw new ArgumentException("El importe no puede ser negativo.");
        MinorUnits = minorUnits; Currency = CurrencyCode(currency);
    }
    public static Money FromDecimal(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("El importe no puede ser negativo.");
        try { return new(decimal.ToInt64(decimal.Round(amount, 2, MidpointRounding.AwayFromZero) * 100m), currency); }
        catch (OverflowException) { throw new ArgumentException("El importe excede la capacidad del sistema."); }
    }
    public static string CurrencyCode(string value)
    {
        var code = (value ?? "").Trim().ToUpperInvariant();
        if (code.Length != 3 || code.Any(x => x is < 'A' or > 'Z')) throw new ArgumentException("Indicá un código de moneda de tres letras.");
        return code;
    }
}

public sealed class CommercialSettings
{
    public int Id { get; private set; } = 1;
    public string Currency { get; private set; } = "ARS";
    public Guid Version { get; private set; }
    public CommercialSettings() { }
    public void SetCurrency(string currency) { Currency = Money.CurrencyCode(currency); Touch(); }
    public void Touch() => Version = Guid.NewGuid();
}
