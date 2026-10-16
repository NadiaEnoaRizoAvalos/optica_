using System.Globalization;
using OpticaSosa.Domain.Clinical;

namespace OpticaSosa.Web.Pages.Clinical;

public static class ClinicalDisplay
{
    public static string Kind(ClinicalKind kind) => kind switch
    {
        ClinicalKind.MedicalPrescription => "Receta médica",
        ClinicalKind.OpticalPrescription => "Registro de la óptica",
        _ => "Antecedente visual informado"
    };
    public static string Number(decimal? value, int digits = 3) => value?.ToString("F" + digits, CultureInfo.GetCultureInfo("es-AR")) ?? "Sin informar";
}
