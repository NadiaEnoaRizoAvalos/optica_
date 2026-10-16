# Requisitos — Sistema de Gestión para Ópticas

Estado: fases 1–3 implementadas y fase 4 con recetas, antecedentes y adjuntos implementados, verificados y actualizados localmente. Pendiente revisión y aceptación del usuario. Fase 5 no iniciada.

## 1. Objetivo y alcance

Centralizar la atención de clientes, registros profesionales, inventario y operaciones comerciales de Óptica Sosa en una computadora Windows. La aplicación debe funcionar sin internet y permitir, en una etapa posterior, el acceso de tres celulares conectados a la misma red Wi-Fi.

La primera entrega operativa se utilizará desde el navegador de la propia PC. La interfaz será adaptable desde el inicio. Habilitar celulares no deberá requerir duplicar información ni reemplazar las reglas de negocio.

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

En fase 3 se implementan únicamente visitas administrativas con motivo y observaciones administrativas, estados en curso/completada/cancelada y autoría. Profesionales informantes, observaciones clínicas y antecedentes visuales se incorporarán en fase 4 con sus permisos específicos. No ingresar contenido médico en los campos administrativos.

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

- Adjuntar tantos documentos como sean necesarios a cada versión de una receta o antecedente, sin máximo fijo de cantidad. La carga inicial se realiza de a un archivo por operación; no limita el total de documentos del registro.
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

- Administrar catálogo de armazones y cristales con código interno, descripción, categoría y atributos pertinentes. Diferenciar variantes que tengan stock propio.
- Registrar entradas, salidas y ajustes con cantidad, fecha, motivo y usuario. Calcular existencias a partir de movimientos verificables.
- Evitar stock negativo y registrar correcciones mediante movimientos compensatorios.
- Definir antes del módulo qué cristales se almacenan y cuáles se encargan a laboratorio, y cuándo se reserva o descuenta stock.
- Mantener precios e importes históricos de cada operación aunque cambie el catálogo.

### RF-06. Pedidos y ventas

- Crear pedidos vinculados con cliente, productos, cantidades, precios acordados y receta cuando corresponda.
- Registrar estados y transiciones autorizadas. Propuesta inicial: pendiente, en preparación, listo, entregado y cancelado; validar el circuito real antes de implementar.
- Registrar ventas y sus detalles, totales, descuentos y forma de pago. Confirmar necesidad de señas, saldos y pagos parciales antes del módulo.
- Actualizar stock y operación comercial de forma atómica, sin duplicar descuentos ante doble envío o reconexión.
- Anular con motivo, usuario y movimientos compensatorios; no eliminar ventas confirmadas. No presentar comprobantes internos como facturas fiscales.

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

## 4. Modelo de información propuesto

| Entidad | Relaciones y propósito |
| --- | --- |
| Cliente | Tiene muchas visitas, recetas, pedidos y ventas. |
| Visita | Pertenece a un cliente; agrupa observaciones y registros profesionales. |
| Profesional | Identifica al autor informante; puede existir sin cuenta de acceso. |
| Antecedente visual | Pertenece a un cliente, con visita opcional, profesional, procedencia y autor del registro. |
| Receta | Pertenece a un cliente, con visita opcional, tipo, fecha y profesional. |
| Graduación por ojo | Pertenece a una receta; como máximo una por lateralidad. |
| Adjunto | Metadatos y referencia al archivo privado, vinculados a una receta o registro permitido. |
| Producto / variante | Identifica cada artículo inventariable y sus atributos. |
| Movimiento de stock | Vincula variante, cantidad, motivo y operación de origen. |
| Pedido / detalle | Vincula cliente, receta opcional y artículos con precios históricos. |
| Venta / detalle | Registra la operación comercial y referencia al pedido cuando corresponda. |
| Usuario / rol / permiso | Controla acceso y autoría; separado de cliente y profesional. |
| Evento de auditoría | Identifica actor, acción, objeto, fecha y resultado sin copiar contenido médico. |
| Registro de respaldo | Conserva ejecución, estado, versión y referencia al manifiesto. |

Este modelo es conceptual. Las cardinalidades finales, índices, restricciones y política de conservación se documentarán antes de cada módulo. Los vínculos deben impedir asociar una visita o receta a otro cliente por error.

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
