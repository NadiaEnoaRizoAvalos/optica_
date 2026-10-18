# Inventario de integración — backend y futuras pantallas

Documento incremental para preparar el prototipo. No constituye un diseño visual aprobado ni declara terminada la lógica comercial. Los contratos implementados están en `src/OpticaSosa.Application`; PLANS.md es la fuente de estado y decisiones pendientes.

## Criterios de integración

- Mantener Domain, Application, Infrastructure y Web en un repositorio. Pantallas Blazor y adaptadores Razor usan servicios de aplicación con autorización en servidor, sin EF ni reglas de negocio en componentes visuales.
- Agregar endpoints HTTP sólo por una necesidad concreta. Las rutas de páginas siguientes no constituyen una API REST ni requieren crear otra para cada operación.
- Conservar identificadores, versiones de concurrencia, paginación y resultados de validación de los contratos. Las revisiones clínicas son históricas; una corrección no reemplaza una receta anterior.
- Diferenciar estado técnico verificado de aceptación manual y operativa. El kit de recuperación 5.2.A permanece congelado y fuera del alcance de este inventario.
- Al implementar cada incremento, actualizar aquí datos, acciones, permisos y contratos efectivamente disponibles. No presentar contratos o rutas propuestos como implementados.

## Contratos implementados que deben reutilizarse

| Módulo / pantallas | Contratos de aplicación | Información y operaciones necesarias |
| --- | --- | --- |
| Clientes: búsqueda, ficha y edición | `ICustomerService`, `CustomerInput`, `CustomerSummary`, `CustomerDetails`, `CustomerSearchResult`, `CustomerResult` | Identificación, documento obligatorio, contacto, estado, concurrencia y condición nuevo/recurrente; buscar, consultar, registrar, editar e inactivar conforme a permisos. |
| Visitas | `IVisitService`, `VisitInput`, `VisitSummary` | Cliente, motivo, fechas, estado y versión; iniciar/cerrar visita y consultar historial paginado desde la ficha. La atención no depende de una compra. |
| Clínica: historial, registro y corrección | `IClinicalService`, `ClinicalInput`, `ClinicalHistory`, `ClinicalDetails`, `EyeInput` | Receta/antecedente, consulta/control, profesional, fecha, síntomas, graduaciones OD/OI, distancia pupilar, versiones y motivo de corrección; listar, consultar, crear y corregir. Sólo permisos clínicos explícitos. |
| Profesionales | `IProfessionalService`, `ProfessionalInput`, `ProfessionalDetails` | Nombre, matrícula/jurisdicción, especialidad, contacto, estado y versión. Registros de profesionales externos no crean cuentas de acceso. |
| Adjuntos y política de archivos | `IClinicalAttachmentService`, `IClinicalFilePolicyService` | Listado paginado por revisión, carga/descarga privada, optimización explícita, retiro con motivo y recuperación; límites administrables separados del acceso a contenido clínico. Conservar originales. |
| Trabajos y calidad existentes | `IWorkService`, `WorkInput`, `WorkPage`, `WorkDetails`, `ControlInput`, `TemplateDetails` | Trabajo vinculado a cliente y versión de receta, estados, controles técnicos/de entrega, rondas y responsables. Reutilizar en fase 10; no confundir con venta comercial. |
| Usuarios y auditoría | `IAccountService`, `AccountSummary`, `AccountResult`, `ISecurityAudit` | Cuentas individuales, roles múltiples, estado y concurrencia; crear, cambiar roles, inactivar, cambiar contraseña y consultar auditoría según permiso. Nunca incluir contraseñas en DTOs de consulta o registros. |
| Configuración del negocio | `IBusinessSettingsService`, `BusinessBranding`, `BusinessDetails`, `BusinessProfile` | Nombre e identidad visual configurables por instalación, logo y versión; consulta pública de marca y administración autorizada separadas. |
| Respaldos | `IBackupService`, `BackupOverview`, `BackupConfiguration`, `BackupRunView`, `BackupAction` | Destino/estado, última copia verificada, último intento, próxima ejecución, errores e historial; configurar, inspeccionar, vincular, solicitar y verificar. Las acciones reales mantienen sus bloqueos/autorizaciones operativas. |
| Visor de emergencia | `IViewerReader`, `IViewerAccounts`, `IViewerAudit` | Consulta aislada de clientes, clínica, adjuntos y trabajos según rol; gestión local de revocaciones y auditoría. No publicar operaciones de escritura sobre datos restaurados ni asumir permisos vigentes a partir de un respaldo antiguo. |
| Proveedores y laboratorios | `ISupplierService`, `SupplierInput`, `SupplierDetails`, `SupplierPage`, `CatalogResult` | Nombre, clasificación proveedor/laboratorio/ambos, contacto/fiscal opcionales, servicios/notas, activo y versión. Buscar/paginar, consultar, crear y actualizar/inactivar. Administrador administra; Atención/Ventas consulta; sin cuentas automáticas. |
| Productos y variantes | `ICatalogService`, `ProductInput`, `VariantInput`, `ProductDetails`, `VariantDetails`, `ProductPage`, `VariantPage` | Producto/categoría/marca/modelo, proveedores asociados y variantes reales con SKU único, especificaciones tipadas, presentación, precio/moneda, estado y versiones. Sin crear existencias ni combinaciones. Administrador administra; Atención/Ventas consulta. |
| Catálogo técnico | `ITechnicalCatalog`, `TechnicalVariant`, `TechnicalVariantPage` | Productos activos y atributos técnicos por categoría; contrato separado sin precio, costo o moneda. Acceso Profesional y roles de catálogo. No constituye acceso a recetas de clientes. |
| Moneda comercial | `ICatalogService.ConfigurationAsync/ConfigureCurrencyAsync`, `CommercialConfiguration`, `Money` | Moneda inicial ARS configurable por instalación, importes en centavos enteros y redondeo aprobado. Administrador configura; no convertir ni renombrar precios existentes automáticamente. |

### Datos y acciones para el futuro frontend de catálogo

- **Listado/ficha de producto:** búsqueda por nombre, marca/modelo o SKU; categoría, estado y paginación de 50 elementos con `HasMore`. Ficha y editor muestran nombre obligatorio, marca/modelo/descripción y proveedores asociados; no presentan stock hasta implementar inventario. Inactivar conserva referencias.
- **Listado/editor de variante:** SKU único y estado; armazones con color y medidas en mm; cristales con material, índice, tratamiento y graduación del producto; contactos con graduación, curva base/diámetro y cantidad declarada en caja cerrada. Separar estas especificaciones de la graduación clínica OD/OI. No representar valores desconocidos como cero.
- **Unidades:** armazones/accesorios/cristales por unidad individual; contactos por caja cerrada. No mostrar venta suelta de contactos ni tratar un par de cristales como una unidad. Los atributos no pertinentes a la categoría se rechazan en servidor.
- **Precio:** importe final con 2 decimales y moneda explícita de la instalación, opcional mientras se prepara el catálogo; vacío difiere de precio cero. Sin pantalla de costos en este incremento. Consultas técnicas del Profesional no usan los DTOs comerciales.
- **Concurrencia:** conservar `SubmissionId` para reintentar un alta sin duplicar; enviar `Version` y, para variantes, `ProductVersion`. Tras guardar una variante recargar producto/configuración, cuyas versiones también cambian para evitar carreras con inactivaciones y cambios de moneda. Mostrar `CatalogResult` y conservar la entrada ante conflicto/validación, sin sobrescribir silenciosamente.
- **Trazabilidad:** una variante usada por otros módulos bloqueará cambios de identidad/especificaciones mediante `SpecificationLocked`; se permiten precio/estado nuevos sin reescribir datos históricos de las operaciones. Los módulos que la utilicen deben activar ese bloqueo dentro de su transacción.
- **Navegación propuesta, no rutas creadas:** catálogo → producto → variantes; proveedores/laboratorios → ficha; administración → moneda. No se agregaron endpoints o pantallas comerciales: se integrarán con estos servicios al implementar Figma, o con una interfaz técnica mínima si hace falta verificar otro incremento.

### Publicación técnica profesional — incremento 7.1

Contrato implementado `ITechnicalPublicationService`: `ListAsync(customerId, recordId, page)`, `GetAsync(customerId, publicationId)`, `PublishAsync(customerId, recordId, TechnicalPublicationInput)` y `RevokeAsync(customerId, publicationId, version, reason)`. Servicio de aplicación con autorización clínica en servidor, sin nuevos endpoints HTTP. No está concedido a Atención/Ventas ni a Administrador sin Profesional.

- **Pantalla futura de autorización:** partir de una receta/version concreta de la ficha clínica; confirmar destino cristales/contactos y ojos incluidos. `SourceRevisionId`, `RecordVersion`, `SubmissionId` estable y `Confirmed` explícito. La clínica puede conservar registros incompletos; fabricar exige esfera, eje si cilindro no cero y base si prisma no cero.
- **Montaje:** alturas/DP monoculares opcionales por ojo en mm con 2 decimales, referencia de armazón e instrucciones. DP binocular se copia de la receta para cristales; no derivar monoculares ni rellenar valores desconocidos. Para contactos seleccionar variante exacta, curva base y diámetro por ojo, coincidentes con graduaciones; no convertir prescripciones automáticamente.
- **Consulta:** `TechnicalPublicationDetails` contiene identificadores de cliente/registro/revisión, solicitud técnica, valores de los ojos incluidos, DP, autor/fecha, versión y vigencia. No contiene historia de síntomas, antecedentes, notas, fotos/PDF, precios o costos. Paginar `TechnicalPublicationPage` de 50 con `HasMore`, sin límite total de publicaciones.
- **Corrección/revocación:** enviar identificador y versión previos, motivo y confirmación; conservar originales. Mostrar `PublicationStatus` (vigente, revocada, sustituida, receta modificada o cliente inactivo). Una publicación independiente no sustituye otra salvo vínculo de corrección explícito. Un conflicto requiere recargar; nunca reactivar una publicación histórica por reintentar un comando.
- **Separación comercial:** este DTO profesional incluye motivos de corrección/revocación y no debe reutilizarse completo en Ventas. La siguiente sección comercial incorporará un contrato mínimo ligado al pedido y revalidará vigencia al reservar/encargar. No agregar acceso clínico general mediante el menú de pedidos.
- **Prueba desde navegador:** pendiente de interfaz mínima/integración del frontend. Actualmente las operaciones se verifican mediante pruebas de aplicación con SQLite aislada; no se anuncia una pantalla/ruta que todavía no existe.

## Rutas actuales relevantes

Estas rutas ya existen en la aplicación principal y se conservan hasta integrar el prototipo aprobado:

| Área | Rutas de páginas |
| --- | --- |
| Cuenta | `/cuenta/ingresar`, `/cuenta/salir`, `/cuenta/clave`, `/cuenta/denegado` |
| Clientes | `/clientes`, `/clientes/nuevo`, `/clientes/{id}`, `/clientes/{id}/editar`, `/clientes/{id}/inactivar`, `/clientes/{id}/visitas/nueva` |
| Clínica | `/clinica/registros`, `/clientes/{customerId}/clinica`, `/clientes/{customerId}/clinica/{id}`, `/clientes/{customerId}/clinica/nuevo/{id?}` |
| Documentos | `/clientes/{customerId}/clinica/{id}/adjuntos`, `/clientes/{customerId}/clinica/{id}/adjuntos/{attachmentId}/descargar` |
| Profesionales | `/clinica/profesionales`, `/clinica/profesionales/{id}`, `/clinica/profesionales/editar/{id?}` |
| Trabajos / calidad | `/pedidos`, `/pedidos/nuevo/{id?}`, `/pedidos/{id}`, `/pedidos/{id}/control`, `/calidad`, `/entregas` |
| Administración | `/administracion`, `/admin/usuarios`, `/admin/auditoria`, `/admin/negocio`, `/admin/calidad`, `/admin/archivos-clinicos`, `/admin/respaldos` |

`GET /negocio/logo/{version}` entrega el logo configurado sin contenido clínico. Los parámetros identificadores de estas rutas usan restricciones GUID en la implementación cuando corresponda. `/pedidos` representa actualmente trabajos ópticos: el futuro módulo comercial deberá distinguirlos claramente, sin reinterpretar registros históricos como ventas.

## Módulos comerciales pendientes

| Fase | Futuras pantallas y operaciones | Decisiones antes de fijar contratos |
| --- | --- | --- |
| 6 | Backend de catálogo, variantes, proveedores/laboratorios y moneda implementado y verificado automáticamente. Pantallas definitivas y aceptación manual pendientes. | P-06 de catálogo y P-03 monetaria confirmadas. Contratos y campos descritos arriba; no se presume habilitación operativa. |
| 7 | Pedido comercial y líneas, reserva/disponibilidad/faltantes, señas/pagos/saldo y publicación técnica autorizada. | P-02 y P-03; inventario mínimo seguro y separación de información clínica/comercial. |
| 8 | Encargos, seguimiento, recepción parcial/observada y documentación mínima para laboratorio. | Autorizaciones técnica/comercial, estados, recepción y cancelación de P-06; reutilizar entradas idempotentes de inventario. |
| 9 | Movimientos, ajustes justificados, bajas, devoluciones, lotes, vencimientos y alertas. | Ampliar el núcleo de fase 7; no duplicar existencias ni ocultar ajustes editando saldos. |
| 10 | Calidad y entrega integradas con producto/trabajo/materiales. | P-05: matriz de aplicabilidad y transición segura. Reutilizar motor e historia de fase 4.1. |
| 11 | Caja, ingresos/medios de pago, cierres e informes reconciliables. | Reglas monetarias y permisos explícitos; sin contabilidad integral. |

La entrega consolidada para Figma se realiza al completar la lógica principal: inventario final de módulos, contratos, endpoints necesarios, datos/acciones por pantalla y flujos de navegación. Este documento inicial no sustituye esa entrega ni la aprobación del prototipo.
