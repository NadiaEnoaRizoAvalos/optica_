# Requisitos — Sistema de Gestión para Ópticas

Estado: fases 1–4.1 implementadas y verificadas; ampliación comercial en diseño. Catálogo, proveedores, inventario y circuito comercial todavía no implementados. Fase 5 pendiente de aprobación.

## 1. Objetivo y alcance

Centralizar la atención de clientes, registros profesionales, inventario y operaciones comerciales de Óptica Sosa en una computadora Windows. La aplicación debe funcionar sin internet y permitir, en una etapa posterior, el acceso de tres celulares conectados a la misma red Wi-Fi.

La primera entrega operativa se utilizará desde el navegador de la propia PC. La interfaz será adaptable desde el inicio. Habilitar celulares no deberá requerir duplicar información ni reemplazar las reglas de negocio.

**Sistema exclusivamente interno:** sólo empleados autorizados pueden iniciar sesión. Los clientes no interactúan con el programa ni tienen cuentas. Los médicos externos y proveedores son registros independientes de los usuarios internos; no se crean accesos automáticamente. Los tres celulares serán utilizados por personal de la óptica. No habrá portal de pacientes, registro público, consulta externa de pedidos ni cuentas de proveedores. Cada óptica conserva su propia instalación y base local; no se introduce multiempresa en una base compartida.

La ampliación prioriza backend, modelo de datos, reglas, seguridad y pruebas; las pantallas serán mínimas y utilizables. El rediseño visual definitivo, la comercialización del software, licencias de pago y panel comercial externo quedan fuera de este alcance. El plan A–G es una propuesta: esta tarea sólo actualiza documentación y no autoriza implementar la siguiente fase.

No se incluyen inicialmente facturación fiscal electrónica, contabilidad integral, pagos en línea, acceso remoto por internet, sincronización entre sucursales, aplicaciones móviles nativas ni funcionamiento de cada celular desconectado del servidor. Su incorporación requiere acordar un nuevo alcance.

## 2. Operación y despliegue

- La PC será el único servidor de aplicación, base de datos y adjuntos. Los navegadores accederán a ASP.NET Core; nunca directamente al archivo SQLite.
- El uso local debe funcionar sin conexión a internet. No depender de CDN, fuentes remotas, servicios de autenticación externos ni descargas durante el uso habitual.
- Para usar celulares deben estar encendidos la PC y el servidor, con conectividad local disponible. Ante pérdida de conexión, mostrar el estado y evitar confirmar operaciones que no se guardaron.
- La instalación deberá documentar inicio y apagado seguro, ubicación de datos, recuperación y requisitos del equipo. La ejecución automática se definirá en fase 1.
- Guardar la base activa en disco local, fuera de carpetas compartidas o sincronizadas. Separar archivos de datos de binarios y código fuente.

## 3. Funcionalidades

### RF-01. Clientes

- Crear, consultar y actualizar clientes; inactivar sin perder su historial.
- Registrar nombre, apellido y documento obligatorios. El usuario confirmó en fase 3 que el documento debe ser obligatorio y único; teléfono y correo son opcionales. Fecha de nacimiento y domicilio quedan fuera de esta primera versión por minimización de datos.
- Buscar por nombre y datos de identificación autorizados. Advertir posibles duplicados sin fusionar registros automáticamente.
- El documento se compara sin espacios, puntos, guiones ni diferencias entre mayúsculas/minúsculas, y conserva unicidad incluso en clientes inactivos. Coincidencias de nombre o teléfono requieren revisión y confirmación explícita cuando se trate de personas distintas con documentos diferentes.
- Mostrar cronológicamente visitas, recetas, pedidos y compras.
- Distinguir clientes nuevos y recurrentes con la regla confirmada: nuevo hasta la primera visita completada; recurrente cuando exista una visita completada anterior a la atención actual. Distinguirla de “cliente con compras previas”.

### RF-02. Visitas y antecedentes registrados

En fase 3 se implementaron visitas administrativas con motivo y observaciones administrativas, estados en curso/completada/cancelada y autoría. Fases 4–4.1 incorporaron profesionales informantes, registros clínicos, antecedentes y consultas con sus permisos específicos. No ingresar contenido médico en los campos administrativos.

- Registrar fecha, motivo, observaciones, usuario responsable y profesional informante cuando corresponda.
- Registrar problemas visuales comunicados por profesionales como antecedentes documentados, con fecha y procedencia, sin inferencias automáticas.
- Conservar historial de correcciones relevantes, responsable y motivo; no sobrescribir silenciosamente información clínica.

### RF-03. Recetas y graduaciones

- Diferenciar receta médica y registro o receta de la óptica; conservar fecha, profesional, procedencia, observaciones y vínculo con cliente y visita opcional.
- Registrar por separado ojo derecho e izquierdo y los campos que correspondan: esfera, cilindro, eje, adición, prisma/base y distancia pupilar, según definición validada con la óptica.
- Definir unidades, precisión, campos opcionales y validaciones con el usuario antes de programarlos. Diferenciar dato ausente de cero.
- Permitir varias recetas por cliente y consulta histórica; una nueva no reemplaza la anterior.
- No limitar la cantidad de recetas, antecedentes, consultas, controles ni documentos por cliente, por rol o por receta. Conservar fechas, profesionales y graduaciones durante años, sujetos a la capacidad real del servidor. Paginar el historial cronológico sin truncarlo. Consultas y controles pueden registrarse como nuevos antecedentes informados, asociados opcionalmente a visitas; los campos administrativos no deben contener datos clínicos.
- No producir diagnósticos, prescripciones ni recomendaciones clínicas automáticamente.

Definición confirmada por el usuario durante fase 4: esfera, cilindro, adición y prisma con hasta **3 decimales**; dioptrías para los primeros tres y dioptrías prismáticas para prisma. Eje entero entre 0 y 180 grados y base como texto informado. Distancia pupilar **binocular**, en milímetros con hasta **2 decimales**. Todos estos campos son opcionales, sin exigir saltos de 0,25 y distinguiendo vacío de cero. No se implementan valores calculados ni conversiones clínicas. La capacidad técnica inicial de los campos numéricos es hasta 9999 en valor absoluto; no constituye un rango clínico de normalidad. Distancia pupilar no negativa.

Las recetas y antecedentes conservan cliente y tipo; pueden asociarse a una visita del mismo cliente. Las correcciones crean versiones con motivo obligatorio, fecha de registro y usuario, sin sustituir versiones anteriores. Cada versión conserva una copia de los datos identificatorios del profesional informante, para que editar su ficha no cambie el documento histórico. Se permite corregir registros de clientes inactivos y de visitas posteriormente canceladas, conservando los vínculos originales.

Profesionales informantes: nombre obligatorio; matrícula y ámbito/jurisdicción opcionales cuando no constan. Registro separado de las cuentas de acceso; crear una ficha no concede permisos ni certifica habilitación. Inactivación sin borrado. Duplicados exactos normalizados de nombre/matrícula/ámbito se rechazan incluso si están inactivos. Acceso clínico según matriz vigente: sólo Profesional; Administrador y AtenciónVentas no reciben acceso clínico por esos roles.

### RF-04. Fotografías y documentos

- Adjuntar tantos documentos como sean necesarios a cada versión de una receta o antecedente, sin máximo fijo de cantidad. Desde fase 4.1 el formulario clínico admite selección múltiple y vistas previas locales; la pantalla específica de Adjuntos permite también cargas individuales. La capacidad técnica por solicitud no limita el total almacenado.
- Formatos iniciales: JPG/JPEG, PNG y PDF. Validar extensión, tipo y contenido real; decodificar imágenes estáticas y verificar estructura PDF sin reescribirlo. HEIC/HEIF se evalúa por viabilidad de conversión y conservación del original; no se habilita hasta disponer de un conversor verificado en esta instalación.
- Límites iniciales centralizados: fotografías 25 MB por archivo y PDF 50 MB por archivo (MB operativos de 1024 × 1024 bytes, explicitados en la interfaz). Administrador puede modificarlos en Almacenamiento clínico; usuarios Profesional no pueden elevarlos. Guardar cambios y versiones de concurrencia en SQLite y auditarlos. No dependen de cantidades de recetas ni de roles del paciente.
- Mantener configurables los límites técnicos del servidor en ClinicalFiles: envolvente máxima por solicitud, píxeles a decodificar y reserva de espacio. Un límite de consulta o solicitud protege recursos y no es un límite del historial almacenado.
- Si el archivo excede el límite, mostrar el tamaño permitido y ofrecer otra selección, optimización compatible explícita o ajuste administrativo. La opción sin pérdida inicial recomprime sólo datos IDAT de PNG, conservando dimensiones, bytes de píxeles filtrados, profundidad, orientación, perfiles de color y demás chunks. Entregar una copia descargable para revisión; no adjuntarla, reemplazar ni borrar originales silenciosamente. La selección original en el dispositivo se conserva. Si se desea archivar además ese original y excede el límite, el administrador debe autorizar elevarlo.
- No recomprimir JPEG ni PDF clínicos cuando no existe una transformación sin pérdida verificada. No reducir resolución ni inferir legibilidad por OCR. Si una optimización no mejora el tamaño o no es viable, informar y conservar la selección original.
- Guardar archivos originales, sin reencodificar, fuera de wwwroot en el almacenamiento privado Windows. Metadatos por documento: versión clínica, nombre original seguro, tamaño, tipo, SHA-256, fecha, autor y estado. Nombres físicos generados por GUID, sin rutas proporcionadas por el navegador. Verificar integridad antes de descargar y no entregar contenido incompleto o alterado.
- Sólo Profesional puede cargar, listar, recuperar, consultar y descargar documentos clínicos. Las descargas requieren sesión, no son enlaces públicos, se envían como archivos adjuntos sin caché y no se ejecutan/previsualizan dentro de la aplicación. Administrador configura tamaños y genera copias privadas sin acceso clínico por navegador.
- Cargas con metadatos Pending durables antes de publicar bytes, escritura temporal y movimiento atómico sin sobrescritura. Fallos no publican archivos parciales; reenvíos idempotentes y recuperación explícita verifican el original mediante hash. No eliminar originales automáticamente. Retiro con motivo/autor/fecha, conservando archivo y consulta histórica autorizada.
- Comprobar espacio libre y mantener reserva antes de cargas y copias. Mostrar espacio/reserva al administrador, y mensajes controlados si una operación no puede completarse.
- Paginar adjuntos y versiones en grupos de 25, sin cupos totales. Una corrección conserva sus documentos en la versión anterior y permite adjuntar otros a la nueva; una receta nueva crea un registro diferente.
- Incluir SQLite y todos los archivos privados, históricos y retirados, en las copias. La copia manual de fase 4 usa respaldo consistente de SQLite, hashes, verificación de relaciones y documentos y manifiesto complete.json final; ninguna copia fallida sobrescribe la anterior. Verificar restauración en un directorio aislado con documentos ficticios. Programación, retención, cifrado, copia externa y recuperación operativa integral permanecen en fase 7.

### RF-05. Inventario

- Existencias por variante, presentación y lote cuando corresponda; categorías independientes de cristales, armazones, lentes de contacto, accesorios y otros productos configurables. Los artículos por encargo no se consideran disponibles por estar en catálogo.
- Distinguir físico, reservado, disponible, pendiente de recepción, dañado/no utilizable y mínimo configurado. Disponible = físico utilizable vigente menos reservas activas, sin contar compras pendientes. Vencidos, dañados, cuarentena y productos rechazados no se reservan ni entregan. No sustituir un mínimo desconocido por una regla de reposición inventada.
- Movimientos para apertura de existencias, compras recibidas, consumo, ventas, devoluciones, ajustes y bajas. Registrar variante/presentación, lote, cantidad/unidad y factor histórico, tipo, fecha del servidor, usuario, motivo y documento/línea de origen. Reservar no produce por sí mismo una salida física.
- Historial de movimientos inmutable; errores se compensan mediante otra operación referenciada, sin borrar ni editar movimientos confirmados. Las proyecciones de saldo deben reconciliarse con el historial; nunca editar directamente el saldo para corregirlo.
- No permitir físico, disponible o reservas inconsistentes ni negativos. Modificar saldos, reservar/liberar/consumir y auditar en transacciones con concurrencia e identificadores idempotentes. Una cancelación libera sólo lo pendiente; un material consumido exige devolución o compensación explícita, no reaparece automáticamente.
- Conversión exacta de cajas, blísteres y unidades; abrir una caja transfiere su contenido sin duplicarlo y conserva lote/vencimiento. La presentación sellada debe poder distinguirse de unidades disponibles. No mezclar lotes ni cantidades incompatibles con la unidad de medida.
- Alertas de stock bajo y vencimientos próximos, con umbrales configurables. Vencimiento verificado con fecha de negocio del servidor al reservar, consumir y entregar; una reserva previa no habilita entregar vencidos. Productos devueltos o dañados quedan no utilizables hasta revisión autorizada.
- La recepción sólo incrementa disponible para cantidades efectivamente recibidas y aceptadas. Pendiente de recepción se obtiene del circuito de compras, no de promesas ni de stock ficticio.

### RF-06. Pedidos y ventas

- Atención/Ventas busca o registra al cliente, selecciona artículos, cantidades y precios autorizados y crea presupuestos y pedidos. Cuando corresponda, selecciona una referencia clínica habilitada para uso comercial; si falta especificación aprobada, solicita intervención profesional. No recibe recetas completas ni antecedentes por esa selección.
- Separar presupuesto y sus versiones, venta y detalle, pedido del cliente y líneas, trabajo óptico, solicitud de materiales, orden de compra, recepción y entrega. Una venta no es una compra a un proveedor y no debe generar una segunda salida de stock si el material ya fue consumido por el trabajo.
- Conservar copias de nombre/SKU, variante, presentación, unidades, precio, moneda, descuentos y especificaciones aprobadas en documentos versionados. Cambios del catálogo, proveedor o receta no reescriben documentos confirmados. Dinero con precisión y redondeo explícitos; nunca float/double. Costos, márgenes y autorización comercial tienen permisos distintos del precio de venta visible.
- Reutilizar el trabajo OpticalWork de fase 4.1 y su historial. Los estados comerciales, abastecimiento, aprobación técnica y preparación son dimensiones separadas. No reemplazar la máquina de estados de calidad por un estado libre de pedido.
- Registrar señas, pagos parciales, saldo, devolución/anulación y medios de pago al aprobar su subtarea; no marcar pagado por crear una venta ni emitir facturas fiscales. Alcance y reglas monetarias deben validarse antes de programarlos.
- Confirmación, descuentos, reservas, consumo y auditoría coordinados mediante transacciones e idempotencia. Anular con motivo/autor/fecha y compensaciones; no eliminar ventas, compras ni su historial. La confirmación comercial no salta las verificaciones de entrega.

### RF-07. Usuarios y permisos

- Autenticación con usuarios individuales, cierre de sesión e inactivación de cuentas.
- Roles iniciales confirmados: administrador, atención/ventas y profesional autorizado. Datos clínicos restringidos al personal autorizado. Validar la matriz detallada de permisos antes de implementar.
- Restringir lectura y edición de antecedentes y recetas a usuarios autorizados; la operación de venta no otorga por sí sola acceso clínico completo.
- Controlar en servidor operaciones sobre clientes, registros clínicos, adjuntos, inventario, ventas, usuarios, auditoría y respaldos.
- Definir alta del primer administrador y recuperación de acceso local sin credenciales predeterminadas.

### RF-08. Respaldo y restauración

- Generar respaldos manuales y programados de SQLite, adjuntos y configuración necesaria, con manifiesto y versión del esquema.
- Obtener una copia consistente mediante mecanismos compatibles con SQLite y coordinación de escrituras y adjuntos; contemplar archivos auxiliares si corresponde.
- Informar fecha, resultado y errores del último respaldo. Un respaldo fallido no debe sobrescribir la última copia válida.
- Proponer copia diaria y copia adicional antes de cambios de esquema; acordar retención, destino externo, cifrado, pérdida máxima tolerada y tiempo de recuperación.
- Las copias ubicadas sólo en el mismo disco no cubren la falla de ese disco. Prever una unidad externa autorizada y una recuperación de claves documentada.
- Restaurar primero en un directorio separado, comprobar integridad y adjuntos, y requerir autorización antes de reemplazar datos activos.

### RF-09. Productos, variantes y especificaciones

- Producto: ID, código interno, nombre, categoría, marca, modelo/línea, descripción, unidad base, activo e indicador de gestión de inventario. Variante: SKU único normalizado, combinación técnica concreta, estado y disponibilidad propia. Presentación: unidad/caja/blíster/otra, cantidad por envase y factor exacto a unidad base. Inactivación sin perder referencias históricas.
- Costos de compra en ofertas del proveedor y documentos de compra; precio de venta vigente por variante/presentación. La ficha reúne estos datos según permisos. Costo actual de referencia no sustituye el costo histórico de una compra; definir su cálculo antes de implementarlo. Un mismo artículo puede tener varios proveedores sin duplicar producto ni stock.
- Cristal oftálmico: diseño monofocal/bifocal/progresivo/ocupacional, material, índice de refracción, marca/línea, diámetro, tratamientos y recubrimientos, antirreflejo, endurecido, fotocromático, polarizado y filtros identificados. En variantes terminadas: SPH, CYL, AXIS, ADD y prisma/base cuando correspondan. Especificar stock/encargo/fabricación personalizada; no crear miles de combinaciones ficticias de potencias.
- Separar ficha de catálogo, valores prescritos OD/OI y medidas técnicas de montaje. Cada ojo puede requerir variante o fabricación distinta; compartir producto no significa compartir graduación. Valores opcionales conservan ausencia frente a cero. Reutilizar precisión clínica de 3 decimales en dioptrías y 2 en DP binocular; validar aparte unidades y precisión de dimensiones/índice/curva base antes de la etapa A.
- Lente de contacto como familia independiente: marca/modelo, tipo esférico/tórico/multifocal/otro, potencia, cilindro, eje, adición, BC, DIA, material, frecuencia de reemplazo y presentación/cantidad por caja. Lote y vencimiento pertenecen a las partidas físicas recibidas, no a la ficha global del producto. Precio y costo dependen de presentación/proveedor/documento; cantidades disponibles provienen del inventario.
- Armazón: marca/modelo, código, material, color, tipo de montura, ancho de cristal, puente, largo de patillas y otras medidas estructuradas cuando existan. Color/tamaño y demás características comerciales distinguen variantes con disponibilidad propia.
- Accesorios y otros: atributos estructurados pertinentes; usar extensiones específicas sólo si el negocio las requiere. No una tabla gigante con todos los campos de todas las familias ni especificaciones críticas escondidas en una descripción o JSON libre.
- No sustituir marca, modelo, potencia, BC, DIA, material ni tratamientos automáticamente. Comparar identificadores y valores exactos contra especificaciones aprobadas; no inferir equivalencia clínica de una coincidencia comercial. Cambios de adaptación requieren aprobación profesional y revalidación del trabajo.

### RF-10. Proveedores y catálogo de abastecimiento

- Ficha independiente: razón social/nombre, CUIT cuando corresponda, dirección, localidad/provincia, teléfono, correo, contacto, categorías suministradas, estado y observaciones. Validar duplicados y formatos; inactivar sin borrar documentos relacionados. El proveedor no posee cuenta.
- Relación muchos a muchos con variantes/presentaciones mediante oferta del proveedor: código externo, costo/moneda y unidad cotizada, vigencia, plazo estimado, disponibilidad informada y capacidad de encargo. Una oferta no representa existencia local ni autoriza sustituir artículos.
- Ficha con catálogo suministrado, historial de órdenes y líneas compradas, cantidades, costos históricos, fechas, pendientes, recepciones y devoluciones. Cada sección se habilita al implementar su etapa, sin inventar datos ni duplicar productos por proveedor.

### RF-11. Verificación de materiales e intervención profesional

- Profesional revisa pedidos pendientes, receta autorizada, especificaciones por ojo y montaje, confirma materiales requeridos, consulta disponibilidad y prepara solicitudes técnicas para laboratorio/proveedor. Aprobación técnica independiente de autorización comercial de compra.
- Al confirmar materiales de un pedido, verificar en servidor variante/presentación exactas, cantidad/unidad, lote/vencimiento, reservas existentes y fabricación requerida. Si aún falta validación técnica, mostrar pendiente de revisión; no calcular requerimientos clínicos automáticamente desde un nombre comercial.
- Reservar sólo lo disponible y autorizado en una transacción. En faltantes mostrar **Pedido pendiente de materiales**, cantidades necesarias/disponibles/faltantes, proveedores posibles y estado de abastecimiento. Mantener detalle técnico protegido: Ventas recibe un resumen operativo, Profesional el detalle óptico necesario y Administrador la información de compra autorizada, sin historial clínico.
- Generar una solicitud idempotente por necesidad faltante aprobada; reintentos no duplican pedidos ni compras. Fabricación personalizada genera requerimiento por encargo, no stock ficticio ni una variante clínica global por paciente.

### RF-12. Compras y abastecimiento

- Circuito: solicitud de materiales → revisión técnica → autorización comercial → orden de compra por proveedor → recepción → inventario. Agrupar solicitudes compatibles de varios pedidos mediante asignaciones de cantidades, preservando sus orígenes y sin duplicar necesidades.
- Orden: número único, proveedor, fecha/usuario del servidor, líneas con variantes/especificaciones autorizadas, cantidades/presentaciones, costos y moneda históricos, pedidos relacionados, fecha estimada, estado y observaciones. Precio o cantidad fuera de la autorización exige nueva revisión comercial.
- PDF descargable con versión de la orden y datos mínimos para fabricar/proveer. Documento comercial para Administrador; anexo técnico con valores ópticos individuales sólo para Profesional y condicionado a autorización comercial vigente. Aprobar una compra no habilita a descargar anexos clínicos. El generador y sus dependencias se eligen y autorizan en su etapa; disponer de un lector PDF no equivale a disponer de generador. No enviar automáticamente correos, mensajes ni órdenes. Registrar confirmación autorizada de emisión/envío, sin implementar un conector externo ahora; exportar no acredita que se haya enviado.
- No transmitir historial clínico ni notas de consulta. La documentación técnica para el laboratorio contiene únicamente las especificaciones necesarias, revisadas por Profesional; costos y datos clínicos completos no se mezclan en las vistas por rol.

### RF-13. Recepciones, devoluciones y entrega

- Recepción completa o parcial por línea de compra, cantidades aceptadas, rechazadas/dañadas, diferencias, productos incorrectos, lote y vencimiento cuando corresponda. Sobreentregas o sustituciones no se incorporan silenciosamente: exigir autorización y documento de diferencia.
- Confirmar recepción, movimientos, asignaciones a necesidades y auditoría atómicamente; repetir un envío no duplica entradas. Relacionar lo aceptado con los pedidos pendientes y reservar según sus necesidades verificadas. Devoluciones al proveedor y correcciones se documentan y compensan sin borrar la recepción.
- La recepción no aprueba calidad ni habilita entrega. La entrega requiere especificaciones/materiales vigentes y todos los trabajos aplicables preparados, ambas etapas de calidad aprobadas y ningún artículo vencido/no utilizable. Evitar doble entrega/consumo y conservar responsable, fecha, detalle de unidades/lotes y constancia interna.
- Mantener la restricción actual de fase 4.1: Profesional realiza/aprueba técnico y Atención/Ventas o Profesional realiza/aprueba previo a entrega. Las nuevas categorías necesitarán checklists aplicables aprobados, sin clasificarlas falsamente como reparaciones ni omitir controles mediante No aplica.

## 4. Modelo de información propuesto

### 4.1. Implementado y conservado

Monolito modular: Dominio → sin infraestructura; Aplicación → contratos y casos de uso; Infraestructura → EF Core/SQLite, Identity y archivos; Web → Razor Pages y Blazor SSR, autorización y composición. Contextos por operación, originales privados y auditoría mínima.

| Módulo actual | Entidades / relaciones verificadas | Límite actual |
| --- | --- | --- |
| Usuarios y administración | Identity, ApplicationUser, roles cerrados, AuditEntry, BusinessSettings | Sin clientes/externos como cuentas; sin permisos clínicos implícitos para Administrador. |
| Clientes y visitas | Customer 1:N Visit | Documento obligatorio único, clasificación por visitas completadas, historial sin borrado. |
| Clínica y profesionales | Customer 1:N ClinicalRecord; ClinicalRecord 1:N ClinicalRevision; Professional 1:N ClinicalRevision; revisión 0..2 EyeMeasurement | Receta médica/óptica e historial visual; consultas como IsFollowUp; versiones inmutables, emisor histórico separado de usuario que carga. |
| Adjuntos y copias clínicas | ClinicalRevision 1:N ClinicalAttachment; ClinicalFilePolicy | Archivos privados, hash, límites administrables y copia manual verificada; operación integral de respaldo pendiente. |
| Trabajos y calidad | Customer 1:N OpticalWork; receta/versión opcional; OpticalWork 1:N QualityControl y WorkTransition; control 1:N QualityCheckItem; QualityTemplate por tipo/etapa | La pantalla actual dice Pedidos, pero OpticalWork es trabajo mínimo sin artículos estructurados, precios, venta, compras, stock ni lentes de contacto como tipo. |

La navegación agrupa áreas relacionadas; no duplicar el historial clínico para crear otra pantalla. Las nuevas áreas lógicas son Productos, Inventario, Proveedores, Compras/abastecimiento y Ventas/presupuestos; separar Pedido del cliente de Trabajo óptico cuando se incorpore el circuito comercial. La navegación definitiva se revisará después, sin crear ahora rutas vacías.

### 4.2. Diseño conceptual de la ampliación (todavía sin tablas ni migraciones)

| Responsabilidad | Entidades propuestas y relaciones | Reglas de propiedad |
| --- | --- | --- |
| Catálogo | ProductCategory 1:N Product; Product 1:N ProductVariant; variante 1:N ProductPresentation | Código de producto y SKU comerciales únicos normalizados; la presentación define envase/factor, no otra identidad clínica. |
| Especificaciones de catálogo | Perfiles tipados OphthalmicLens, ContactLens y Frame asociados a producto/variante según el atributo; tratamientos identificados mediante relación N:M | Datos comunes en producto, valores concretos en variante. No guardar lotes ni stock en perfiles ni crear combinaciones no ofrecidas. |
| Proveedores | Supplier N:M variante/presentación mediante SupplierOffering; categorías suministradas N:M | Código del proveedor y costo cotizado pertenecen a la oferta, no duplican la variante. |
| Precios | Precio vigente por variante/presentación y moneda; costos cotizados en ofertas | Documentos confirmados conservan precio/costo, moneda, unidad, descuento y redondeo propios. Historial de tarifas sólo si hace falta para vigencias. |
| Inventario | StockLot N:1 variante cuando aplica; StockMovement y líneas por variante/presentación/lote/condición; StockBalance como proyección reconciliable con versión | Lote conserva código, vencimiento y procedencia. Presentaciones convertidas conservan el lote. No utilizable es una condición física diferenciada. |
| Comercial | Quote 1:N QuoteLine; versiones aprobadas; Sale 1:N SaleLine; Customer 1:N CustomerOrder; CustomerOrder 1:N OrderLine | Conversión explícita con referencias al documento origen, sin obligación de que presupuesto ya sea venta/pago. Líneas con snapshots comerciales. |
| Trabajo óptico | CustomerOrder 1:N OpticalWork mediante vínculo opcional para trabajos nuevos; asignación de líneas al trabajo cuando un trabajo reúna armazón y dos cristales | Un trabajo pertenece como máximo a un pedido comercial; ambos deben corresponder al mismo cliente. Conservar trabajos previos sin inventar ventas retroactivas. |
| Especificación aprobada | Trabajo 1:N TechnicalSpecificationVersion; versión 1:N EyeSpecification y MountingMeasurement tipados; referencia a ClinicalRevision cuando corresponda | OD/OI independientes; valores clínicos protegidos. La aprobación refiere a una versión concreta, con responsable/fecha. No copiar automáticamente recetas como encargos sin revisión. |
| Necesidades y reservas | OpticalWork 1:N MaterialRequirement; requerimiento 1:N StockReservation; reserva referencia variante/presentación/lote y cantidad | Línea comercial origen opcional; puede haber necesidades internas. Mantener cantidad requerida, asignada, consumida y faltante sin mezclar reserva con consumo. |
| Abastecimiento | SupplyRequest 1:N SupplyRequestLine referida a necesidades; PurchaseOrder 1:N PurchaseOrderLine; cada orden pertenece a un Supplier | PurchaseAllocation distribuye cantidades de una línea de compra entre necesidades; evita duplicar cantidades al agrupar pedidos. |
| Recepciones | PurchaseOrderLine 1:N GoodsReceiptLine; GoodsReceipt 1:N líneas; recepción aceptada origina movimientos y lotes; devoluciones referenciadas | Aceptado, rechazado y devuelto no se confunden. Recalcular pendiente según decisiones documentadas, sin recibir dos veces por reenvío. |
| Calidad y entrega | Reutilizar QualityControl/QualityCheckItem/WorkTransition por trabajo/ronda; CustomerOrder 1:N Delivery; Delivery 1:N DeliveryLine con asignaciones reales | Referenciar aprobación técnica, materiales y controles vigentes; inicialmente entrega total, parcial sólo si se aprueba su regla. Entrega final no reconsume materiales ya consumidos. |
| Pagos (subtarea futura) | Sale/pedido → Payment y asignaciones para señas/saldos/reintegros | Relación y momento contable por validar; no anticipar un módulo financiero ni facturación fiscal. |

Los nombres representan responsabilidades y cardinalidades propuestas, no una instrucción para crear todas las tablas. Materializar cada entidad al implementar su etapa; objetos de valor sin identidad no requieren tablas propias. Usar FKs restrictivas, índices por código/estado/fecha y coherencia cliente–receta–pedido–trabajo–material. Vínculos con documentos comerciales no cambian propiedad ni autoría clínica. No borrar documentos confirmados ni sus referencias.

### 4.3. Contratos, precisión y consistencia

- Casos de uso separados por catálogo, proveedores, inventario, presupuestos/ventas/pedidos, abastecimiento y recepción; reutilizar Work y Clinical. No un servicio comercial universal ni EF desde la interfaz. Integración local por contratos de Aplicación; transacciones sobre una única SQLite, sin microservicios ni conectores obligatorios.
- Cada documento mantiene su ciclo: presupuesto borrador/aceptado/vencido/anulado; venta confirmada/anulada; compra propuesta/autorizada/emitida/parcial/recibida/cerrada/anulada. Son propuestas a validar por etapa. Pedido expone por separado situación comercial, validación técnica, abastecimiento y preparación; una recepción no asigna libremente Listo/Entregado.
- Unidades base y conversiones explícitas exactas; cantidades de lentes/armazones enteras. Si otro artículo requiere fracciones, acordar escala propia antes de habilitarlo. Propuesta monetaria: enteros escalados con moneda, precios/costos unitarios hasta 4 decimales y totales según decimales de la moneda; validar moneda y redondeo por línea antes de la etapa A/C. No depender de aritmética decimal aproximada de SQLite.
- Disponible por lote excluye vencidos/no utilizables y reservas activas. En presentaciones, contabilizar cada existencia una sola vez en unidades base y conservar cuántos envases siguen cerrados. Una transferencia de presentación descuenta el envase origen y agrega su contenido en el mismo acto; factor histórico inmutable.
- StockBalance con clave única por posición y token de concurrencia; movimiento, reserva, saldo y auditoría se guardan juntos. Comprobar disponibilidad y modificar la posición en la misma transacción, no con una consulta previa separada. Reconciliación desde movimientos y eventos de reserva; claves idempotentes en confirmaciones, asignaciones, recepciones y entrega. Sin stock negativo ni sobreasignación.
- Revisiones relevantes de pedido/especificación/material invalidan aprobación técnica y controles afectados, liberan o revisan reservas aún no consumidas y conservan versiones anteriores. El backend de entrega comprueba reglas actuales, no sólo una bandera histórica. Una sustitución exige revisión profesional y, si cambia el acuerdo, aceptación comercial registrada por empleado autorizado.
- Nuevas órdenes de clientes se habilitan progresivamente: antes de la integración G no se ofrecen como un circuito completo de entrega. Trabajos históricos conservan rutas, recetas fijadas y controles de fase 4.1; no crearles pedidos, ventas, precios ni stock retroactivos. Cualquier conversión posterior requiere revisión y trazabilidad.

### 4.4. Matriz comercial propuesta (sin cambios de permisos en esta tarea)

| Operación | Administrador | Atención/Ventas | Profesional |
| --- | --- | --- | --- |
| Usuarios, configuración, auditoría; catálogo, proveedores, precios y costos | Gestiona | Sólo catálogo/precio de venta autorizado | Sólo catálogo técnico necesario |
| Disponibilidad y faltantes | Consulta operativa sin datos clínicos | Consulta comercial | Consulta técnica |
| Entradas, ajustes y bajas manuales; recepción y devoluciones al proveedor | Autoriza/ejecuta inicialmente | Sin permiso inicial | Sin permiso inicial |
| Presupuesto, venta y pedido comercial | Administra sin detalle clínico | Registra según permisos específicos | Consulta operativa necesaria, sin costos/márgenes/pagos |
| Referencia a receta para pedido | Sin acceso clínico por rol | Sólo referencia previamente habilitada y resumen operativo | Consulta clínica autorizada y aprueba especificaciones |
| Solicitud de abastecimiento | Revisa parte comercial | Genera por faltantes, sin especificaciones clínicas completas | Prepara y valida detalle técnico mínimo |
| Autorizar/emitir compra y PDF comercial | Autoriza/ejecuta; recibe sólo datos necesarios | Sin autorización de compra | Prepara encargo técnico; no autoriza gasto ni envío |
| Clínico y control técnico | Sin acceso por rol | Sin acceso por rol | Registra/consulta/aprueba |
| Control previo y entrega | Sin permiso operativo implícito vigente | Realiza/aprueba/entrega | Realiza/aprueba/entrega |

Implementar políticas por operación y proyecciones distintas, sin cambiar ni ampliar automáticamente roles existentes. La nueva administración comercial del Administrador no concede control técnico ni entrega: hoy esos permisos son de Profesional/Ventas. Selección opaca de referencia por Ventas será un caso de uso nuevo con permiso específico y control de pertenencia; no reutilizar ListAsync/GetAsync clínicos para exponer graduaciones. Datos ópticos de un catálogo genérico no equivalen a permiso de consultar su asociación con un paciente. El control de stock procesa detalles técnicos en servidor y devuelve faltantes sin filtrarlos a vistas no autorizadas. Delegar recepción/precios/costos o pagos a otros roles requiere aprobación expresa.

### 4.5. Referencias técnicas consultadas — 2026-10-16

El diseño de datos es una propuesta de arquitectura, basada en los requisitos y en la distinción que hacen las siguientes fichas oficiales; no son reglas de prescripción ni un catálogo comercial argentino actualizado.

- ZEISS distingue diseños, materiales/índices y recubrimientos, y separa opciones personalizadas. Se propone por ello modelar estas dimensiones por separado del paciente y del precio. [Ingredientes de lentes ZEISS](https://www.zeiss.com/vision-care/en/lens-ingredients.html).
- CooperVision publica BC y DIA en mm, esfera/cilindro/eje y diferencias entre líneas tóricas. La ficha de MyDay toric también identifica material, presentación y reemplazo. Esto fundamenta perfiles de contacto específicos y presentaciones separadas; sus combinaciones no deben generalizarse a otras marcas o mercados. [Parámetros tóricos oficiales](https://coopervision.com/practitioner/toric), [ficha MyDay toric](https://coopervision.com.sg/practitioner/our-products/myday-family/myday-toric).
- Para armazones se contrastarán las dimensiones de lente, puente y patillas con fichas del fabricante; no inferir tamaños a partir del nombre del modelo. [Guía de talles Ray-Ban](https://india.ray-ban.com/size-guide).

Usar datos ficticios en ejemplos/pruebas; no importar catálogos completos ni imágenes de terceros. En la etapa A validar fichas reales de los proveedores elegidos por la óptica, unidades, combinaciones ofrecidas y disponibilidad regional, sin imponer intervalos de catálogo a las recetas clínicas existentes.

## 5. Requisitos no funcionales

### Ampliación autorizada 4.1 — navegación y calidad por pedido

- Inicio y navegación común: Clientes, Profesionales y médicos, Recetas y antecedentes, Pedidos, Control de calidad, Entregas y Configuración y administración, filtrados por permisos.
- La ficha del cliente reúne datos personales, clasificación histórica, cantidad y última fecha de visitas, recetas/antecedentes, profesionales relacionados, pedidos y entregas. Los resúmenes clínicos requieren Profesional. Los listados completos siguen paginados y sin cupos de almacenamiento.
- Crear un cliente no exige receta. Confirmar su alta y ofrecer continuar desde su ficha. El registro clínico parte de la selección del cliente y separa tipo/fecha, motivo declarado y procedencia, profesional, graduaciones, adjuntos y guardado. Volver automáticamente a la ficha mostrando el nuevo documento y el resultado de cada lote de adjuntos.
- Consultas/controles informados son registros independientes del historial visual, diferenciados de antecedentes. Síntomas opcionales con procedencia explícita (paciente, profesional o documento externo); no inferir diagnósticos ni valores ausentes.
- Profesionales externos: nombre, matrícula/ámbito informados, especialidad, estado y contactos opcionales. Ficha con documentos y pacientes relacionados, incluidas versiones previas en que participó. Diferenciar emisor de autor de carga y de cuenta de usuario.
- Adjuntos múltiples JPG/JPEG, PNG y PDF, revisión local previa y retiro de la selección sin borrar originales. Mantener tamaños centralizados, validación, almacenamiento privado, SHA-256, paginación y respaldo de fase 4. Envíos divididos por capacidad técnica conjunta; un fallo parcial debe indicarse y permitir completar desde Adjuntos. No guardar PDF clínicos optimizados ni convertir HEIC sin conversor verificado.
- Pedido mínimo: cliente, tipo (anteojos con receta, sólo armazón o reparación), producto/especificaciones operativas, armazón, receta opcional vinculada por Profesional con versión fijada, autor/fecha y versión de concurrencia. No registrar información clínica en campos operativos visibles para ventas. Inventario y circuito comercial se completan en fases 5 y 6, sin duplicar ventas.
- Dos etapas obligatorias por pedido: técnica (Profesional) y previa a entrega (Atención/Ventas o Profesional). Cada control registra ID, pedido/ronda, etapa, estado, usuario/rol verificados en servidor, fecha de realización/aprobación, observaciones y snapshot de verificaciones vigentes. El cliente se determina por el pedido.
- Checklist técnico inicial de diez verificaciones: receta, graduación/especificaciones, tipo de cristales, armazón, montaje, centrado/alineación, estado de cristales, estado de armazón, defectos y observaciones finales. Etapa de entrega: identidad, correspondencia, producto preparado, accesorios, documentación, estado general y condiciones de entrega.
- Cada resultado: pendiente, conforme, observado o no aplica. No aplica exige justificación y sólo se admite si no corresponde al tipo de trabajo. El Administrador configura obligatoriedad por tipo/etapa; las verificaciones esenciales no se desactivan. Cambiar requisitos invalida los controles de los pedidos no entregados afectados; conserva entregados e historia.
- Aprobación sólo con checklist íntegro, verificaciones obligatorias conformes, sin observados y no aplicables justificados. Backend valida permisos específicos de realización y aprobación, responsable activo, plantilla y versión vigentes. La interfaz mantiene deshabilitada la aprobación hasta completar las condiciones.
- Estados: Registrado → En preparación → Pendiente de control; un observado exige corrección → nueva ronda. Sólo ambas etapas aprobadas habilitan Listo para entrega → Entregado. Cambiar especificaciones o versión de receta invalida ambas aprobaciones y obliga a nueva ronda. Conservar controles anteriores; rectificar uno aprobado requiere motivo y un nuevo documento de control.
- Guardar control, pedido/transición y auditoría en la misma transacción. Concurrencia optimista para evitar aprobaciones simultáneas inconsistentes; IDs de envío para no duplicar reintentos. No exponer asignación directa de estado. Historial de estados y controles paginado.
- Permisos separados: consultar/escribir pedidos, realizar/aprobar técnico, realizar/aprobar previo a entrega, registrar entrega y configurar requisitos. Administrador configura sin acceso clínico ni aprobación operativa. Auditoría registra acciones, responsables e identificadores sin contenido clínico.

- **Seguridad:** mínimo privilegio, validación en servidor, protección contra solicitudes indebidas, adjuntos privados y controles de sesión. HTTPS confiable será requisito para acceso desde celulares.
- **Privacidad:** datos ficticios durante desarrollo; permisos de Windows y cifrado de disco/respaldos según estrategia acordada. Definir retención, acceso y obligaciones aplicables antes de usar datos reales.
- **Integridad:** claves foráneas, transacciones, restricciones y control de concurrencia. Mostrar conflictos de edición sin perder cambios silenciosamente.
- **Usabilidad:** formularios en español, navegación clara, etiquetas accesibles y uso desde PC y celular; errores comprensibles sin detalles internos.
- **Rendimiento:** validar en el equipo real con datos sintéticos y cuatro sesiones simultáneas (PC y tres celulares). Propuesta de objetivo: búsquedas y guardados habituales en hasta dos segundos en condiciones acordadas, excluyendo cargas de archivos y respaldos.
- **Mantenibilidad:** capas con responsabilidades claras, migraciones versionadas, decisiones documentadas y pruebas de reglas críticas.
- **Disponibilidad:** el funcionamiento depende de la PC y red local; documentar recuperación ante apagado inesperado y fallas de disco. No prometer alta disponibilidad.
- **Observabilidad:** logs técnicos sin contenido clínico ni credenciales, auditoría de operaciones sensibles y errores visibles de respaldo.

## 6. Decisiones pendientes

### Ampliación autorizada previa a fase 4: configuración del negocio

El producto se instala independientemente en cada óptica con su propia base local. La instalación original corresponde a Óptica Sosa, pero la interfaz no debe tener una marca comercial fija; los nombres de proyectos y namespaces se conservan por compatibilidad.

- Administrador configura nombre comercial, logo, dirección, provincia, localidad, teléfono, WhatsApp, correo, CUIT y datos adicionales del establecimiento. Todos los campos son opcionales.
- Persistir la configuración separada de clientes/visitas en SQLite y conservarla al reiniciar. Exponer un contrato de lectura administrativo reutilizable para futuros comprobantes; todavía no generar documentos.
- Mostrar nombre/logo en cabeceras de Blazor y páginas de cuentas. Sin nombre, usar "Sistema de Gestión para Ópticas"; sin logo, un identificador visual genérico.
- Sólo Administrador puede leer la ficha completa y modificarla; los demás usuarios y la pantalla de ingreso reciben únicamente nombre y logo para presentación.
- Logos PNG/JPEG estáticos de hasta 2 MB y 2048 × 2048 píxeles. Verificar contenido real, extensión y MIME, decodificar y reencodificar a PNG de hasta 512 píxeles por lado, sin metadatos originales. Guardar bytes en la fila de configuración, nunca rutas proporcionadas por el usuario ni archivos públicos originales.
- Permitir retirar o reemplazar el logo, mostrar la vista guardada y prevenir sobrescrituras con versión de concurrencia. Registrar cambios en auditoría sin copiar los datos configurados.
- Validar longitudes/contactos y formato de 11 dígitos de CUIT opcional. Esto es una validación sintáctica, no una comprobación fiscal.
- Licencias comerciales, períodos de prueba y panel de ventas continúan pospuestos. Fase 4 no forma parte de esta ampliación.

Resolver en la fase pertinente: versión de Windows y recursos de la PC; Blazor y modo de interacción definitivo; reglas de cliente recurrente; campos clínicos y unidades; permisos; circuito de stock y pedidos; pagos parciales; volumen estimado; retención y objetivos de recuperación; certificados y direcciones de red; alcance normativo antes de producción.

Decisiones resueltas en fase 3: campos de cliente indicados en RF-01; lectura, alta, edición y visitas administrativas para los tres roles existentes; inactivación sólo para Administrador. La ficha muestra la clasificación para la próxima atención según visitas completadas, y cada visita conserva si era nuevo o recurrente al iniciarse (sólo cuentan visitas completadas antes de ese inicio).
