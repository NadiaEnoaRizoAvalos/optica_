namespace OpticaSosa.Domain.Customers;

public sealed class Customer
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string Document { get; private set; } = "";
    public string DocumentKey { get; private set; } = "";
    public string SearchName { get; private set; } = "";
    public string? Phone { get; private set; }
    public string? PhoneKey { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid Version { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }

    private Customer() { }

    public Customer(Guid id, CustomerData data, string actorId, DateTime utcNow)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Identificación inválida.");
        Id = id;
        CreatedByUserId = actorId;
        CreatedAtUtc = utcNow;
        Update(data);
    }

    public void Update(CustomerData data)
    {
        FirstName = data.FirstName;
        LastName = data.LastName;
        Document = data.Document;
        DocumentKey = CustomerData.DocumentKey(data.Document);
        SearchName = CustomerData.Fold($"{data.FirstName} {data.LastName}");
        Phone = data.Phone;
        PhoneKey = data.Phone is null ? null : CustomerData.PhoneKey(data.Phone);
        Email = data.Email;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Touch() => Version = Guid.NewGuid();
}
