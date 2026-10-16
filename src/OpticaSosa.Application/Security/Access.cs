namespace OpticaSosa.Application.Security;

public static class Roles
{
    public const string Administrator = "Administrador";
    public const string Sales = "AtencionVentas";
    public const string Professional = "Profesional";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Administrator, Sales, Professional });
}

public static class Permissions
{
    public const string ManageUsers = "users.manage";
    public const string ReadAudit = "audit.read";
    public const string ReadClinical = "clinical.read";
    public const string WriteClinical = "clinical.write";
    public const string ReadCustomers = "customers.read";
    public const string WriteCustomers = "customers.write";
    public const string DeactivateCustomers = "customers.deactivate";
    public const string ManageBusiness = "business.manage";

    public static bool Grants(string role, string permission) => (role, permission) switch
    {
        (Roles.Administrator, ManageUsers or ReadAudit or ManageBusiness) => true,
        (Roles.Professional, ReadClinical or WriteClinical) => true,
        (Roles.Administrator or Roles.Sales or Roles.Professional, ReadCustomers or WriteCustomers) => true,
        (Roles.Administrator, DeactivateCustomers) => true,
        _ => false
    };
}

public interface ICurrentUser
{
    string? Id { get; }
    bool HasPermission(string permission);
}
