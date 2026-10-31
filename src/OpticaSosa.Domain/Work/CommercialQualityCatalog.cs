namespace OpticaSosa.Domain.Work;

internal static class CommercialQualityCatalog
{
    internal static IReadOnlyList<CheckDefinition> For(OpticalWorkType type, QualityStage stage)
    {
        if (stage == QualityStage.Delivery) return [
            Required("customer", "Identidad del cliente verificada"), Required("order", "Pedido completo y correspondiente"),
            Required("condition", "Estado del producto y envase verificados"), Required("documents", "Documentación e instrucciones entregadas"),
            Required("balance", "Saldo del pedido igual a cero"), Required("delivery", "Materiales y controles vigentes para entregar")];
        return type switch
        {
            OpticalWorkType.ContactLenses => [Required("authorization", "Producto y parámetros autorizados por el Profesional"),
                Required("product", "Producto y presentación exactos"), Required("lot", "Lote identificado"),
                Required("expiry", "Vencimiento vigente"), Required("seal", "Envase cerrado e íntegro"), Required("condition", "Estado general conforme")],
            OpticalWorkType.CommercialGlasses or OpticalWorkType.OpticalRepair => [
                Required("authorization", "Receta y autorización técnica vigentes"), Required("power", "Graduación de cada ojo conforme"),
                Required("material", "Material y tratamiento correspondientes"), Required("mount", "Montaje y centrado conformes"),
                Required("condition", "Integridad y estado del producto"), Required("work", "Trabajo solicitado realizado")],
            _ => [Required("product", "Producto y pedido correspondientes"), Required("condition", "Integridad y estado conforme"),
                Required("work", "Trabajo solicitado realizado")]
        };
    }
    private static CheckDefinition Required(string key, string label) => new(key, label, Essential: true);
}
