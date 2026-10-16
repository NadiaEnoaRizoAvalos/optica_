using OpticaSosa.Application.Security;
namespace OpticaSosa.Web.Components.Navigation;

public sealed record NavigationItem(string Path, string Label, string Description, string Mark, string? Permission = null);
public static class NavigationItems
{
    public static IReadOnlyList<NavigationItem> For(ICurrentUser user) => user.Id is null ? [] : All.Where(x => x.Permission is null || user.HasPermission(x.Permission)).ToArray();
    private static readonly NavigationItem[] All = [
        new("/", "Inicio", "Tu espacio de trabajo y accesos principales.", "01"),
        new("/clientes", "Clientes", "Buscá personas y consultá su ficha completa.", "02", Permissions.ReadCustomers),
        new("/clinica/profesionales", "Profesionales y médicos", "Informantes, especialidades y pacientes relacionados.", "03", Permissions.ReadClinical),
        new("/clinica/registros", "Recetas y antecedentes", "Elegí un cliente, revisá su historial y registrá información.", "04", Permissions.ReadClinical),
        new("/pedidos", "Pedidos", "Trabajos ópticos y seguimiento por cliente.", "05", Permissions.ReadOrders),
        new("/calidad", "Control de calidad", "Verificaciones técnicas y previas a la entrega.", "06", Permissions.ReadOrders),
        new("/entregas", "Entregas", "Pedidos aprobados y entregas registradas.", "07", Permissions.DeliverOrders),
        new("/administracion", "Configuración y administración", "Usuarios, negocio, archivos y requisitos de calidad.", "08", Permissions.ManageBusiness),
        new("/admin/respaldos", "Respaldos y recuperación", "Destino externo, programación e historial de copias.", "09", Permissions.ManageBackups)];
}
