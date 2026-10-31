# Backend de calidad y entrega comercial — fase 10

## Contratos y límites

`IOrderFulfilmentService` (Application/Delivery) se consume desde Blazor/servidor mediante DI. Reutiliza `IWorkService`, publicación/asignación técnica, pedidos/pagos e inventario existentes; no crea endpoints HTTP duplicados, caja ni otro inventario. No agrega importes a la información visible al Profesional. Las pantallas definitivas dependen de Figma y su aceptación manual sigue pendiente.

| Operación | Entrada / resultado | Permiso |
| --- | --- | --- |
| `GetAsync(orderId)` | Cliente/identificadores, versión de pedido, estado operativo, indicador de saldo cero y responsable/fecha de entrega. No importes ni historia clínica. | Atención/Profesional o permiso de lectura comercial. |
| `MaterialsAsync(orderId,page)` | Líneas actuales: producto/SKU/categoría/variante, cantidad completa, asignación técnica, trabajo y unidades reservadas. | Igual lectura. |
| `WorksAsync(orderId,includeHistory,page)` | Identificador/versión/tipo/estado/ronda, vigencia del conjunto, materiales preparados, controles coincidentes, motivo del bloqueo y trabajo/motivo de reemplazo. | Igual lectura. |
| `CreateWorkAsync(orderId,input)` | ID de comando/nuevo trabajo, versión del pedido, lista explícita de línea/asignación y referencia de armazón. Para reemplazar: trabajo/versión anterior y motivo. | Atención o Profesional; Administrador solo no escribe. |
| `ReopenAsync(orderId,workId,input)` | ID de comando, versiones de pedido/trabajo, motivo obligatorio. Nueva ronda y revisión de ambos controles; conserva los anteriores. | Atención o Profesional. |
| `DeliverAsync(orderId,input)` | ID de comando, versión del pedido y lista completa de trabajos actuales con sus versiones. Entrega completa; no parciales. | Atención o Profesional. |

Listas de 50 registros con `HasMore`; no hay máximo total arbitrario. Ante conflicto, recargar versiones. Reintentar una operación fallida de red con el mismo identificador y contenido; cambiar el contenido exige otro identificador. Un reintento del comando ya completado no repite la salida física.

## Agrupación y estados

- El conjunto de anteojos agrupa explícitamente líneas completas de cristales OD/OI y, opcionalmente, la línea de su armazón. Si no se vende armazón, se exige su referencia operativa. No dividir cantidades ni agrupar líneas automáticamente. Los productos independientes tienen su propio trabajo.
- Los cristales/contactos requieren las asignaciones vigentes del Profesional. Para agrupar varias publicaciones deben corresponder al mismo cliente y versión de receta; los campos y productos autorizados se verifican por cada ojo mediante las reglas ya existentes. El armazón vendido debe coincidir con la referencia de montaje autorizada cuando esté informada.
- Cada línea tiene un único trabajo actual. El reemplazo crea otro OpticalWork y vínculos históricos, con motivo y autor; no edita graduaciones, recetas ni controles anteriores. Las reparaciones/trabajos históricos conservan su circuito propio, sin inventar ventas o productos de servicio.
- Preparación/control se realizan con materiales recibidos, utilizables, vigentes y reservados en su totalidad. Laboratorio conserva línea/autorización originales; cuarentena o recepción parcial no satisfacen la preparación. Reservar no descuenta físicamente.
- El estado comercial de cumplimiento se consulta aparte del estado financiero existente: sin agrupación, esperando materiales, preparación, revisión, listo, entregado o cancelado. El detalle de cada trabajo explica faltantes/técnica inválida. No se alteró el enum ni el contrato de pedidos comerciales para agregar una contabilidad paralela.

## Flujo para integrar el frontend

1. Seleccionar el pedido confirmado mediante los servicios comerciales existentes. Profesional consulta datos operativos sin precios; no necesita una compra para registrar atención clínica independiente.
2. Seleccionar las líneas del conjunto y sus asignaciones técnicas; guardar `CommercialWorkInput`. Armazón aportado: informar referencia. Para corregir una agrupación, indicar versión anterior y motivo.
3. Registrar recepciones/reservas con los servicios existentes. Abrir la ficha `/pedidos/{workId}`: comenzar preparación y enviar a control mediante `IWorkService.TransitionAsync`.
4. Completar control técnico con la matriz 10.1. Profesional obligatorio para cristales/contactos/reparación óptica; Atención/Profesional en no clínicos. Administrador requiere esos roles explícitos.
5. Registrar los pagos pendientes con `IOrderPaymentService`, sin cambiar su comportamiento. Aprobar control previo a entrega sólo con saldo cero y material vigente.
6. Mostrar resumen operativo y confirmar la entrega completa con las versiones de todos los trabajos actuales. No invocar la entrega individual de un trabajo comercial: el servidor la rechaza aunque se eluda el botón.
7. Mostrar fecha/responsable de la entrega registrada. Las devoluciones físicas siguen `IInventoryAdministration`, en cuarentena y separadas del reembolso monetario explícito; contactos abiertos/vencidos no vuelven a utilizable.

Pantallas futuras: preparación/conjuntos del pedido, selección de materiales, ficha/calidad existente, confirmación de entrega completa e historial. No hay una pantalla comercial nueva desplegada en esta entrega: el recorrido completo está probado en servicios/integración, y la prueba manual de ese frontend queda pendiente. No se arrancó ni migró una instalación operativa.

## Consistencia y seguridad

- Ambos controles se vinculan a una huella de los materiales reservados; cada QualityControl conserva su evidencia histórica. Cambiar reserva/material después del control bloquea la entrega hasta una revisión válida. Reabrir exige motivo y conserva rondas anteriores.
- La entrega revalida cliente, técnica, producto, lote/vencimiento, origen, cantidades, saldo y controles dentro de una única transacción SQLite. Registra trabajos entregados, consumos del libro existente, prueba de entrega de cada reserva, auditoría y recibo idempotente. Un fallo revierte toda esa operación.
- Una restricción única impide dos entregas del pedido. La entrega bloquea nuevas operaciones físicas/cancelación del pedido; pagos/reembolsos explícitos conservan su contrato, historial y autorización anteriores. Una devolución monetaria posterior no reescribe la fecha de entrega.
- Cambios/revocaciones de receta bloquean nuevos pasos; publicar y asignar una versión válida y reemplazar el conjunto expresamente cuando corresponda. Lo ya entregado conserva su historia, sin convertir material personalizado en stock genérico.
- No se incluyen síntomas, notas clínicas, fotografías/PDF ni precios en estas vistas operativas. No se cambian permisos clínicos generales, cuentas, claves, cifrado, servicios Windows ni respaldos reales.

## Esquema e integración con fase 11

- `20261031000110_CommercialWorkDelivery`: tres tablas nuevas (CommercialWorks, CommercialWorkLines, OrderDeliveries), índices/FK restrictivas y triggers de pertenencia; columna nullable Reason en WorkTransitions.
- `20261031000517_QualityMaterialEvidence`: columna nullable MaterialHash en QualityControls. Registros históricos conservan null, sin fabricar controles ni entregas anteriores.
- Los identificadores llevan fecha UTC; esta entrega se documenta con fecha local 30/10/2026. Ambas migraciones se aplicaron únicamente en bases ficticias. La adopción operativa exige autorización y copia completa verificable.
- El retroceso rechaza borrar evidencia, conjuntos, entregas o motivos existentes. No usar Down como procedimiento automático de recuperación: una cadena de migraciones SQLite puede contener límites de transacción; la recuperación operativa requiere respaldo y procedimiento específico autorizado.
- OpticaDbContext y contratos de clientes/productos/ventas/inventario/pagos permanecen intactos. DI registra sólo el nuevo módulo; SalesOperations comparte el bloqueo por entrega conservando las acciones monetarias existentes. Registrar/conciliar el esquema y snapshot con el colaborador en una rama de integración; no fusionar snapshots automáticamente ni alterar su rama activa.

5.2.A sigue PENDIENTE DE VERIFICACIÓN OPERATIVA. Estas pruebas no certifican el sistema para datos reales ni sustituyen Figma, pruebas manuales, cifrado/custodias o recuperación física pendiente.
