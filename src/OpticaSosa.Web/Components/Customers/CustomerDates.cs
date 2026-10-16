using OpticaSosa.Domain.Customers;
namespace OpticaSosa.Web.Components.Customers;

public static class CustomerDates
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
    public static string Display(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).ToString("dd/MM/yyyy HH:mm");
    public static string Status(VisitStatus status) => status switch
    {
        VisitStatus.InProgress => "En curso", VisitStatus.Completed => "Completada",
        VisitStatus.Cancelled => "Cancelada", _ => "Desconocido"
    };
}
