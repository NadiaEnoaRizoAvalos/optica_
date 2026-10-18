# Requisitos del sistema Óptica

## Alcance y estado

La aplicación conserva dos áreas con igual importancia: gestión clínica e historial visual, y gestión comercial y operativa. Un Profesional puede registrar y consultar la atención de cualquier cliente aunque no haya compras, pedidos ni pagos.

Es un sistema interno para empleados. Clientes, médicos externos, proveedores y laboratorios no reciben cuentas automáticamente. Cada instalación conserva su propia base; no se incorpora un portal público ni una plataforma multiempresa compartida.

Las fases 0–4.1 están completadas según las verificaciones históricas. En fase 5, multirrol, respaldos, visor y revisión de seguridad tienen avances implementados y verificados automáticamente; permanecen pendientes las aceptaciones manuales y operativas registradas en PLANS.md. La fase 5 no está cerrada para producción. Las fases 6–12 continúan pendientes y conservan su alcance, organizado ahora en seis etapas metodológicas sin renumerar el historial. Los detalles de entregas están en [BITACORA.md](BITACORA.md), los estados técnicos en [DECISIONES.md](DECISIONES.md) y el alcance y las confirmaciones pendientes únicamente en [PLANS.md](PLANS.md).

## Metodología de entrega

1. **Lógica funcional:** completar los módulos pendientes con pruebas unitarias, de integración y regresión; mantener pantallas existentes e interfaces mínimas de comprobación técnica. No realizar todavía un rediseño visual general.
2. **Diseño del frontend:** al completar la lógica principal, entregar inventario de pantallas, información, acciones y flujos de navegación. Esperar el prototipo Figma del usuario y su aprobación antes de implementar el diseño definitivo.
3. **Implementación del frontend:** conectar el prototipo aprobado con servicios existentes y endpoints necesarios, mediante componentes reutilizables. No duplicar reglas de negocio; actualizar pruebas de integración si cambian contratos.
4. **Pruebas manuales y correcciones:** el usuario prueba el sistema integrado con datos ficticios. Registrar y corregir sus observaciones funcionales y visuales; no atribuir aceptación a pruebas que no realizó.
5. **Celulares e instalación:** tras estabilizar la interfaz principal, adaptar y verificar celulares, acceso Wi-Fi privado, instalador Windows, actualizaciones seguras y funcionamiento en otros equipos.
6. **Verificación final:** comprobar integralmente seguridad clínica, roles, respaldos, cifrado y custodia, restauración física en otra PC, visor de emergencia, instalación, actualizaciones y varios dispositivos antes de permitir datos reales.

Estas etapas no postergan controles críticos: seguridad, permisos, integridad y recuperación se implementan y prueban cuando otros módulos dependen de ellos. Los pendientes manuales u operativos no bloquean tareas independientes autorizadas. Se mantiene un único repositorio, datos ficticios y autorización específica para bases operativas, cifrado, instalaciones y demás cambios sensibles.

## Funcionalidades existentes que deben conservarse

### RF-01. Clientes y visitas

- Registrar, editar, buscar, consultar e inactivar clientes; nombre, apellido y documento obligatorios. Documento normalizado único incluso para registros inactivos; teléfono y correo opcionales.
- Validar en el servidor y avisar posibles duplicados según las reglas existentes. No agregar fecha de nacimiento ni domicilio como campos obligatorios.
- Conservar visitas y sus estados, fechas y autores. Mantener la restricción de una visita abierta por cliente. Los tres roles existentes pueden consultar y administrar clientes/visitas; sólo Administrador puede inactivar clientes.
- Identificar como recurrente a quien tenga una visita completada anterior; no usar compras como condición. Conservar historial y referencias aunque se inactive al cliente.

### RF-02. Gestión clínica independiente

- Registrar recetas médicas, recetas de la óptica, síntomas, antecedentes visuales, consultas y controles, con fechas y profesional responsable.
- Asociar cliente, profesional, matrícula, procedencia y visita cuando corresponda; una visita asociada debe pertenecer al mismo cliente. Conservar las instantáneas históricas de los profesionales.
- Registrar por separado ojo derecho e izquierdo: esfera, cilindro, eje, adición, prisma y base. Esfera, cilindro y adición en dioptrías, prisma en dioptrías prismáticas, todos con tres decimales; eje entero entre 0 y 180 grados; base como texto y distancia pupilar binocular en milímetros con dos decimales.
- Los valores clínicos son opcionales y distinguen vacío de cero. No exigir saltos de 0,25 ni generar diagnósticos o sustituciones automáticas. El límite técnico de representación de 9999 en valor absoluto no expresa un rango clínico recomendado.
- Cada nueva receta constituye un documento independiente. Corregir mediante una nueva versión, conservando motivo, autor, fecha, graduaciones y versiones anteriores. No cambiar silenciosamente el cliente, tipo o vínculo histórico.
- Mantener consultas/controles y antecedentes sin exigir una compra. Conservar la historia al cancelar visitas o inactivar registros; aplicar las restricciones de corrección existentes.
- Administrar profesionales y matrículas sin duplicados según nombre, registro y jurisdicción. Inactivar en lugar de borrar referencias; un profesional registrado y una cuenta de usuario son conceptos distintos.
- Consultar cronológicamente el historial con paginación o carga progresiva. No imponer un máximo fijo de recetas, consultas, antecedentes o documentos por cliente.

### RF-03. Documentos clínicos

- Aceptar JPG/JPEG, PNG y PDF, validando contenido, tipo, tamaño, integridad y permisos. Conservar los archivos originales en almacenamiento privado del servidor, con nombres internos y hash; nunca servirlos como archivos públicos.
- Permitir tantos adjuntos por receta/registro como requiera el historial, sujetos a capacidad técnica y seguridad, sin límite fijo de diez.
- Límites iniciales configurables: imágenes 25 MiB por archivo y PDF 50 MiB. Mantener centralizados los controles adicionales existentes: solicitud de 128 MiB, imágenes de hasta 40 megapíxeles y reserva de disco de 512 MiB. Estos límites no son un máximo de documentos por paciente.
- La administración de límites debe respetar permisos, auditoría y concurrencia. Reducir un límite de carga no impide descargar un documento previo válido.
- Permitir seleccionar varios archivos, revisar la selección y volver al historial del cliente; informar claramente cargas parciales y errores sin ocultar qué se guardó.
- Proteger carga, consulta y descarga mediante permiso clínico Profesional; auditar acciones relevantes sin copiar contenido clínico a logs.
- Conservar operaciones pendientes recuperables y escrituras atómicas. Un retiro debe conservar trazabilidad y documentos necesarios para la historia y los respaldos.
- Consultar adjuntos por páginas de 25, sin máximo total. Los documentos conservan su relación con la versión clínica correspondiente.
- Ante exceso de tamaño, informar el límite y permitir elegir otro archivo o solicitar ajuste administrativo. La optimización compatible debe ser explícita, conservar el original y no reducir la legibilidad.
- Conservar la optimización PNG sin pérdida ya verificada, incluidos los controles de píxeles, orientación, color y metadatos necesarios. No generalizarla a recompresión JPEG ni comprimir PDF clínicos automáticamente.
- Evaluar HEIC/HEIF sólo con decodificación/conversión segura, límites y pruebas de legibilidad. No afirmar compatibilidad mientras no esté verificada.
- Todos los documentos clínicos, incluidos los necesarios para versiones y retiros, forman parte de los respaldos. Controlar espacio disponible antes de escribir.

### RF-04. Trabajos ópticos y calidad existentes

- Preservar OpticalWork como trabajo operativo mínimo, distinto de una venta: tipos actuales lentes recetados, sólo armazón y reparación; referencia opcional a una versión clínica fija. No contiene todavía catálogo, precios, pagos ni stock comercial.
- Conservar los seis estados existentes y la exigencia actual de aprobación técnica y de entrega para marcar listo/entregado, tanto en dominio como en la restricción SQLite.
- Mantener dos etapas de control, sus permisos, ítems, resultados, observaciones, autor, rol y fecha. Los controles guardados conservan una instantánea de sus ítems y configuración.
- Registrar resultados pendientes, conformes, observados y no aplicables; este último requiere justificación y sólo corresponde a ítems no esenciales. No aprobar con controles esenciales pendientes u observados.
- Conservar los diez controles técnicos y siete de entrega existentes y su historia, sin reinterpretar aprobaciones anteriores al cambiar plantillas.
- Cambiar datos relevantes invalida las aprobaciones correspondientes de trabajos no entregados, manteniendo rondas previas. Conservar la inmutabilidad y trazabilidad de entregas.
- Mantener configuración administrativa y controles esenciales, transacciones, concurrencia, idempotencia, auditoría y consulta paginada.
- La futura adaptación por tipo de producto/trabajo requiere un diseño aprobado y transición segura en fase 10; no se permite eludir el circuito existente durante fases anteriores.

### RF-05. Acceso, negocio y respaldo manual

- Mantener Identity, cuentas individuales, ingreso/cierre de sesión, bloqueo o desactivación, recuperación administrativa y protección del último administrador. No incorporar claves predeterminadas.
- Las cuentas admiten uno o varios roles explícitos: Administrador, Atención/Ventas y Profesional, mediante casillas en Administración → Usuarios. Una fila muestra todos los roles de cada cuenta. Administrador por sí solo no obtiene información clínica; Profesional gestiona clínica y control técnico; Atención/Ventas gestiona operación y control de entrega.
- Mantener configuración del negocio y marca genérica adaptable. Nombre y logo pueden presentarse donde corresponda; la configuración completa requiere administración.
- Conservar los datos opcionales del negocio, domicilio/localidad/provincia, contactos, WhatsApp, correo, CUIT e información complementaria. La validación de once dígitos del CUIT no constituye una validación fiscal.
- Conservar logo JPG/PNG de hasta 2 MiB y 2048 píxeles, normalizado a PNG de hasta 512 píxeles, sin metadatos, almacenado privadamente con versión y auditoría.
- Mantener la copia manual consistente de SQLite y documentos clínicos, manifiesto/hashes y restauración aislada existente. No presentarla como automatización ni protección completa ante pérdida del disco.

## Ampliaciones planificadas y estado de implementación

### RF-06. Multirrol y recuperación prioritaria — fase 5

- Visor auxiliar de emergencia separado y sólo de lectura: cuentas Identity/multirrol de un respaldo firmado y compatible, clientes/visitas, clínica/versiones/graduaciones/documentos originales y trabajos/calidad según los permisos existentes. No crear cuentas predeterminadas ni simular pedidos comerciales futuros. Usar loopback y runtime privado offline, sin migrar o escribir sobre el respaldo original.
- Verificar autenticidad con confianza independiente e integridad antes de consultar una copia privada nueva. Auditar fuera del snapshot y denegar mutaciones tanto en endpoints como en EF/SQLite. Administrador sin Profesional no obtiene clínica. Permitir revocaciones locales auditadas sin modificar la cuenta original; advertir que los permisos capturados pueden ser anteriores a revocaciones desconocidas sin conexión.
- Desarrollo de 5.2.B y luego 5.3 autorizado con datos ficticios sin esperar revisiones visuales: conservar esas revisiones y los controles operativos de 5.2.A como pendientes, sin presentar fase 5 como lista para producción.

- Permitir múltiples roles explícitos por cuenta, preservando identificadores, contraseñas y asignaciones existentes. La relación Identity ya admite varios roles; revisar servicios e interfaz sin duplicar cuentas.
- Sólo Administrador puede cambiar asignaciones, con confirmación del impacto clínico/administrativo, control de concurrencia y al menos un rol válido no repetido. No permitir quitarse el propio acceso administrativo ni dejar al sistema sin un administrador activo. Rechazar cambios en cuentas inactivas. Una edición sin cambios no invalida sesiones.
- Cambiar roles, registrar cada alta/baja y actualizar los sellos de seguridad/concurrencia en una transacción. Las sesiones de esa cuenta se revocan en su siguiente solicitud; al volver a ingresar sólo obtiene los permisos de sus roles vigentes. No cambia su contraseña. Mantener los responsables históricos de recetas y controles aunque cambien sus roles actuales.
- Aplicar permisos del servidor, mínimo privilegio, auditoría, protección del último administrador, concurrencia e invalidación de sesiones cuando corresponda. No otorgar acceso clínico por el mero rol administrativo.
- Implementar respaldos automáticos y manuales consistentes de SQLite, documentos y configuración necesaria; verificar manifiesto, integridad, espacio, retención y alertas.
- Programación administrativa activable/desactivable, hora local, zona, frecuencia cada N días y retención configurables. Iniciar desactivada y sin horario obligatorio. Mantener copia manual aunque el cronograma esté desactivado.
- En Administración → Respaldos distinguir última copia exitosa y verificada del resultado del último intento, aunque haya fallado; mostrar fecha/hora de ambos, próxima ejecución, disco configurado y estado de conexión, alertas e historial. Consultar conexión en sólo lectura aunque la escritura esté bloqueada; no confundir conexión con permisos, vinculación o cifrado. Cada instalación conserva sus propios parámetros; cambiar horario recalcula la próxima ejecución si está activa y no activa una programación desactivada.
- Identificar destino por volumen/dispositivo y verificar sistema de archivos, capacidad, permisos e independencia física, sin asumir una letra fija. No formatear, cifrar, preparar el disco ni cambiar su configuración sin autorización; ante ausencia, alertar sin sustituir destino.
- Permitir carpeta relativa configurable por instalación. La carpeta debe existir con ACL privadas; guardar configuración no modifica sus permisos. Vincular mediante marcador privado de instalación antes de escribir, sin confiar sólo en un GUID genérico. Cambiar disco/carpeta invalida la vinculación; conservar carpeta/vinculación histórica de cada copia. No sobrescribir marcadores ajenos o dañados ni crear otra carpeta raíz como sustituto.
- Elegir la carpeta visualmente desde el servidor: inspeccionar volumen, explorar sólo carpetas visibles/permitidas, navegar y confirmar, mostrando ruta completa antes de guardar. No usar rutas del dispositivo del visitante ni exigir escritura manual. GUID como dato técnico secundario; nombre, capacidad y espacio visibles. Revalidar selección en servidor y rechazar raíz, traversal, enlaces, almacenamiento activo, carpetas protegidas y contenidos de bases/copias clínicas; paginar directorios sin listar archivos.
- Crear una carpeta sólo con Administrador, autorización de instalación FolderCreationAuthorized, confirmación explícita y padre existente con permisos privados ya preparados. Crear de manera exclusiva sin sobrescritura, cambio de permisos ni eliminación; auditar sin nombres/rutas sensibles. Dejar creación bloqueada por defecto y probarla sólo en entornos ficticios hasta autorización operativa específica. Guardar configuración no activa respaldos.
- Validar en español y conservar horario/frecuencia/zona/retención al inspeccionar o navegar. Diferenciar seleccionar de guardar; permitir conservar/desactivar un destino ya configurado aunque el disco esté desconectado.
- Ejecutar desde el servidor/servicio sin navegador, serializando manuales y automáticas; recuperar vencimientos con una copia actual y registro de períodos perdidos, sin inventar copias históricas. Mostrar última copia válida, próxima ejecución/reintento, fechas, resultados y errores seguros.
- Estado de 5.2.A: migraciones AutomaticBackups/BackupDestinationBinding aplicadas con autorización tras copia completa verificada y comparación de datos anteriores. Configuración administrativa habilitada; programación, trabajador automático y escritura externa siguen desactivados. Pruebas con datos/destinos ficticios; no se cargó configuración de prueba en la instalación operativa. 5.1, 5.3 y visor 5.2.B permanecen pendientes. No hay restauración web sobre la base activa.
- Preparación externa autorizada por separado: carpeta privada y comprobación operativa exclusivamente con archivo ficticio, sin copiar datos clínicos reales. Vinculación/escritura al disco, copias reales, activación automática, servicio, cifrado y recuperación completa siguen pendientes. La pantalla muestra las fechas en la zona seleccionada y conserva la fecha original de una copia reconciliada tras un corte.
- Mantener una copia recuperable fuera del disco del servidor. Un destino desconectado o copia fallida debe informarse y conservar las copias buenas anteriores.
- Probar restauración aislada, incluidos documentos/versiones, autorizaciones y recuperación ante pérdida del equipo y de claves.
- Autenticidad implementada para ensayos: CMS/PKCS#7 separado del manifiesto, SHA-256 y certificado RSA de al menos 3072 bits con uso de firma digital. Confiar sólo en huellas autorizadas previamente y rechazar revocadas; nunca confiar por incluir el certificado en el respaldo. Verificar antes de aceptar/restaurar; no exportar claves privadas. Formato 3 requiere firma. Lectura de copias históricas sin firma de formato 2 sólo bajo política explícita de compatibilidad, que no acredita origen ni resiste degradación deliberada de formato. Para producción exigir firmas y aprobar transición/custodia; sin activación real en esta entrega. Mostrar política actual sin atribuirla a copias históricas.
- Evaluar cifrado adecuado para el volumen real de datos y los respaldos, con custodia y recuperación de claves. No activar cifrado sin verificar compatibilidad y autorización; no confundir cifrado del disco con seguridad de una copia externa.
- Consulta administrativa de sólo lectura de edición Windows, proveedor BitLocker, estado del servicio y protección del destino por GUID, sin recuperar/mostrar claves. Antes de copiar al externo exigir cifrado completo, protección activa, desbloqueo y protector de recuperación verificables; estado desconocido o falta de permisos bloquean la copia, sin destino alternativo. Custodia y ensayo de desbloqueo se acreditan por separado.
- Herramienta local Recovery independiente del host para verificar/restaurar con firma obligatoria, huella confiable externa y directorio vacío separado. Sin acceso de empleados por pantalla, sin migrar/iniciar automáticamente ni reemplazar base activa. Comprobar PFX protegido sólo en memoria y con contraseña interactiva, sin instalar claves; dos custodias independientes protegidas y kit/runtime compatible antes de producción. El visor sigue pendiente en 5.2.B.
- Kit de simulacro para Windows 11 x64 con runtime privado incluido, sin Visual Studio, SDK, instalación ni internet en destino; versión/licencias, ZIP comprobable por hash externo, manifiesto firmado y sólo SQLite/documentos ficticios. Guía verifica carpeta local NTFS nueva privada, firma/hashes, inventario y SQLite idénticos, copia corrupta rechazada y origen intacto. No interpretar una simulación automatizada como ensayo físico o prueba de cifrado. Copia al carry disk sujeta a autorización de ruta exacta, sin alterar copias anteriores.
- Preparación del servicio mediante plan sin efectos por defecto: rutas locales absolutas separadas, Production, localhost, inicio automático y recuperación ante fallo. Instalar/iniciar/desinstalar exige autorización específica, identidad dedicada no administradora y revisión de derechos/ACL/claves. No crear cuentas ni permisos implícitos; desinstalar no borra datos, binarios o copias. Ensayos del BackgroundService no sustituyen reinicio SCM/Windows ni prueba física en otra PC.
- No utilizar datos clínicos reales hasta aceptar evidencias de seguridad, cifrado, autorizaciones y recuperación. Los parámetros y responsables se confirman en PLANS.md.

### RF-07. Catálogo práctico y proveedores — fase 6

- Administrar armazones, cristales, lentes de contacto y accesorios, con código, descripción, marca/modelo, estado, unidades, precios y datos pertinentes.
- Separar producto y variantes realmente ofrecidas, con atributos tipados. Distinguir características del artículo, graduación individual por ojo y datos de montaje; no crear un catálogo combinatorio de variantes inexistentes.
- Para contactos contemplar parámetros pertinentes como curva base y diámetro; lotes, vencimientos y presentación deben conservar trazabilidad desde su primera utilización.
- Administrar proveedores y laboratorios con datos prácticos de contacto, productos/servicios, estado y notas; datos fiscales opcionales según necesidad.
- No duplicar productos por proveedor ni implementar todavía un motor de ofertas, compras agrupadas o estructuras de ERP. No simular existencias al crear artículos.
- P-06 confirmada para catálogo: armazones/accesorios por unidad; cristales por unidad individual; contactos por caja cerrada con cantidad declarada, sin venta suelta inicial. Variantes reales con código único: color/medidas de armazón; material/índice/tratamiento y graduación del cristal cuando corresponda; esfera/cilindro/eje/adición, curva base y diámetro de contactos. No generar combinaciones automáticamente.
- Administrador administra catálogo/proveedores; Atención/Ventas consulta; Profesional consulta datos técnicos del catálogo sin precios/costos, salvo que tenga además un rol que lo autorice. Proveedor/laboratorio requiere nombre, puede cumplir ambas funciones y no crea cuenta; contacto y datos fiscales opcionales. Preservar registros referenciados mediante inactivación, sin eliminación funcional.

### RF-08. Pedidos, ventas y pagos con inventario mínimo — fase 7

- Registrar pedido comercial, cliente, materiales, cantidades, precios, descuentos autorizados, moneda, totales, señas, pagos posteriores, saldo, medio de pago y fechas prometidas.
- Distinguir pedido, venta, trabajo y receta; reutilizar vínculos con trabajos existentes sin inventar ventas para el historial anterior.
- Conservar importes exactos, condiciones y versiones relevantes. Reintentos no duplican pagos; correcciones/anulaciones requieren trazabilidad. Confirmar redondeo, excedentes, devoluciones y entrega con deuda antes de implementarlos.
- P-03 confirmada: moneda configurable por instalación, inicialmente ARS; precios finales e importes con 2 decimales, cantidades enteras en las unidades aprobadas y redondeo de empates alejándose de cero. Cambiar precios no modifica operaciones históricas.
- Administrador y Atención/Ventas registran pagos y descuentos; descuento con motivo y no superior al importe de la línea. Admitir señas/pagos parciales; rechazar sobrepagos y entrega con deuda. Sólo Administrador anula o devuelve dinero, con motivo y compensaciones trazables sin borrar historia. Estas reglas reemplazan la solicitud de confirmación general anterior; los detalles adicionales no resueltos se consultan antes de implementarlos.
- Atención/Ventas sólo puede consultar la proyección técnica mínima publicada y autorizada para el pedido, de sólo lectura y ligada a una versión; no accede a síntomas, antecedentes ni receta completa, ni modifica graduaciones.
- P-02 confirmada: proyección con graduaciones OD/OI, distancia pupilar, producto/material/tratamiento y montaje. Sin notas clínicas ni documentos originales. Corregir/revocar conserva la versión histórica y bloquea nuevos pasos técnicos hasta nueva publicación válida; no enviar automáticamente al laboratorio.
- Montaje: alturas OD/OI y distancias pupilares monoculares opcionales en mm con 2 decimales, armazón e instrucciones; no derivar mediciones ni rellenar desconocidos. Para autorizar fabricación seleccionar ojos y exigir esfera explícita (incluido cero), eje si cilindro no cero y base si prisma no cero. La clínica sigue admitiendo registros incompletos sin compra.
- Cristales y contactos requieren autorización técnica del Profesional aun siendo de stock. Contactos: confirmar producto exacto, graduación, curva base y diámetro por ojo, sin convertir automáticamente una receta de anteojos. Armazones y accesorios pueden venderse sin receta.
- Se permite confirmar pedidos y recibir señas con técnica pendiente; las líneas de cristales/contactos no se reservan ni se encargan hasta disponer de autorización válida. Armazones/accesorios siguen su circuito independiente.
- Reservar un pedido confirmado no exige seña. La liberación al cancelar es explícita, sin vencimientos automáticos de reservas no aprobados.
- Editar líneas y precios únicamente en borrador. Después de confirmar, corregir mediante cancelación y nuevo pedido, sin transferir dinero automáticamente. Si hubo pagos, sólo Administrador puede cancelar y debe registrar la devolución explícita antes del cierre. Lo entregado requiere devolución, no cancelación.
- El Profesional debe conocer el estado operativo necesario sin recibir automáticamente permisos sobre costos, márgenes o edición de pagos.
- Incluir desde esta fase existencias iniciales trazables, entradas mínimas necesarias, disponibilidad, reservas, liberaciones y faltantes. No postergar estos controles indispensables hasta fase 9.
- Admitir materiales de stock, encargados a laboratorio y combinación de ambos, incluso por ojo. Validar requisitos técnicos autorizados y la variante exacta.
- Asignar explícitamente las líneas técnicas a OD, OI o ambos. Cristales para ambos requieren dos unidades y variante de stock coincidente con los dos. Una caja cerrada de contactos puede cubrir ambos ojos sólo con el mismo producto y parámetros expresamente autorizados por Profesional para los dos; no dividir la caja ni convertir graduaciones.
- Reservar sólo material recibido, utilizable y no vencido, con transacciones e idempotencia; impedir saldos negativos y doble reserva de la última unidad.
- Mantener separados existencias físicas, reservas, disponibilidad y encargos pendientes. No transformar un encargo en stock antes de su recepción.
- Proveer pantallas para probar pedidos, señas, saldos y faltantes. El circuito nuevo de entrega completo depende de fase 10; los trabajos históricos conservan su funcionamiento.

### RF-09. Encargos simples a laboratorio — fase 8

- Crear encargos vinculados al pedido y materiales concretos, con laboratorio/proveedor, versión técnica autorizada, aprobación comercial y fechas estimadas y reales.
- Distinguir preparación técnica pendiente, envío pendiente, enviado, en preparación, listo en laboratorio, recibido en óptica, observado/en corrección y cancelado cuando corresponda.
- Registrar actores y cambios, mostrar demoras y permitir recepción por material/cantidad, con observaciones sobre material dañado o incorrecto.
- Una recepción válida incorpora material una sola vez mediante el inventario mínimo existente. Recibir no equivale a aprobar calidad ni entregar al cliente.
- Separar autorización técnica y comercial. Una exportación no prueba envío efectivo; no enviar mensajes automáticamente.
- Circuito inicial confirmado: Profesional autoriza técnica; Administrador/Atención autorizan comercialmente y registran envío explícito. Admitir recepción parcial. Material observado queda en cuarentena, sin disponibilidad. Cancelar sólo cantidades no recibidas; después de enviar, exigir confirmación registrada del laboratorio. Conservar lo recibido y sus movimientos; devoluciones de material y dinero son operaciones separadas.
- Preparar documentación/PDF con la información mínima autorizada por destinatario, sin exponer historia clínica. Cualquier dependencia nueva requiere autorización.
- No introducir PurchaseAllocation, agrupaciones complejas ni circuitos de compras que no necesite el encargo simple.

### RF-10. Administración ampliada del inventario — fase 9

- Ampliar el núcleo de fase 7 con entradas, salidas, ajustes justificados, devoluciones, bajas y alertas, sin crear otro libro de movimientos.
- Mantener saldos reconciliables con movimientos auditados y compensaciones; no editar directamente un saldo para ocultar una diferencia.
- Separar físico, reservado, disponible, no utilizable y pendiente de recepción. Preservar lotes y trazabilidad de presentaciones.
- Controlar lotes/vencimientos de contactos desde el primer manejo físico, aunque ocurra en fase 7 u 8. Revalidar vencimiento tras reservar y antes de preparar/entregar.
- En contactos el lote y el vencimiento son obligatorios. La fecha indicada es el último día válido: desde el siguiente no se reserva ni entrega. Conservar la precisión mes/año cuando ésa sea la del envase y utilizar el último día de ese mes, sin inventar fechas ni omitir el vencimiento.
- No sustituir automáticamente marca, modelo, graduación, curva base o diámetro. Gestionar paquetes/unidades mediante conversiones exactas y sólo cuando se necesiten.
- Reservar no consume; cancelar libera sólo reservas no consumidas. Reponer una devolución exige revisión; no duplicar existencias al abrir un paquete o reintentar recepción/entrega.

### RF-11. Calidad y entrega adaptadas — fase 10

- Adaptar los controles existentes al trabajo y producto: anteojos recetados, armazones, reparaciones y contactos requieren reglas pertinentes, sin aprobaciones ficticias.
- Aprobar previamente una matriz de controles esenciales, roles y aplicabilidad; revisar reglas de dominio y restricción SQLite antes de migrar.
- Conservar controles, rondas, versiones, autores y entregas históricas. No reinterpretar aprobaciones anteriores con plantillas nuevas.
- Para entregar exigir materiales preparados, utilizables y vigentes, todos los controles aplicables aprobados y la regla de pago acordada. No usar “no aplica” para omitir controles esenciales.
- Registrar quién entrega y cuándo, impedir duplicación y definir tratamiento de entregas parciales antes de habilitarlas.

### RF-12. Caja, informes y experiencia final — fases 11 y 12

- Fase 11: caja básica, ingresos y medios de pago, cierres y reportes operativos reconciliados con ventas/pagos/anulaciones; permisos explícitos. No implementar contabilidad integral.
- Fase 12: conservar el alcance de experiencia final y despliegue, distribuido entre etapas 2–6: diseño y navegación sujetos a Figma aprobado, integración, aceptación manual, uso en Windows/tres celulares y verificación operativa final.
- Habilitar red local privada, HTTPS, firewall y sesiones simultáneas, sin internet; probar desconexión, reintento y recuperación.
- Documentar instalación y actualización en Windows conservando datos y respaldos. No exponer el servidor a internet.

## Requisitos transversales y aceptación

- Computadora Windows como servidor único, SQLite y archivos en disco local privado. Navegadores Windows/móviles acceden a la aplicación, nunca directamente al archivo SQLite.
- C#/.NET 10, ASP.NET Core/Blazor, Entity Framework Core y arquitectura Domain/Application/Infrastructure/Web. Interfaz sin dependencia de internet.
- Claves foráneas, índices, restricciones y migraciones revisadas deben conservar relaciones cliente–receta–versión–profesional–adjunto, y futuras relaciones comerciales sin acoplar la atención clínica a una compra.
- Autorizar cada operación sensible en el servidor; proteger sesiones, formularios, descargas y auditoría. No registrar secretos ni contenido clínico en logs ni publicarlos en Git.
- Usar exclusivamente datos ficticios en pruebas. Probar permisos y regresiones, atención sin compra, historias extensas, múltiples documentos, concurrencia, pagos, reservas y recuperación según el módulo implementado.
- Durante la etapa 1, verificar cada módulo mediante pruebas automatizadas y, cuando sea necesario, pantallas mínimas con pasos reproducibles desde el navegador. La validación técnica de lógica no equivale a aceptación de la interfaz definitiva ni del sistema integrado; estas quedan para etapas 3 y 4.
- Conservar Domain/Application/Infrastructure/Web y límites entre clientes, clínica, productos, ventas, laboratorios, inventario, calidad, caja y seguridad. Contratos, DTOs y casos de uso pertenecen a aplicación; las pantallas no deciden reglas ni acceden directamente a EF. Evitar endpoints redundantes cuando Blazor pueda usar los servicios de aplicación.
- Paginar historiales grandes y limitar recursos por seguridad sin imponer máximos arbitrarios al historial. El objetivo orientativo de respuesta interactiva de dos segundos requiere medir equipo y volumen; no constituye una verificación ya realizada.
- Informar errores/advertencias de compilación y pruebas ejecutadas, aprobadas, fallidas y omitidas por separado. Registrar evidencia real en BITACORA.md.
- Cuentas corrientes ampliadas, estadísticas avanzadas, obras sociales/prepagas, mensajería automática, compras agrupadas, integración fiscal, pagos en línea, sincronización, aplicaciones nativas y licencias no forman parte del alcance inicial de estas fases.

## Referencias técnicas previas

Referencias conservadas de la investigación anterior; esta reorganización no constituye una nueva validación de catálogos o compatibilidad clínica:

- [ZEISS — ingredientes de lentes](https://www.zeiss.com/vision-care/en/lens-ingredients.html).
- [CooperVision — parámetros tóricos](https://coopervision.com/practitioner/toric) y [ficha MyDay toric](https://coopervision.com.sg/practitioner/our-products/myday-family/myday-toric).
- [Ray-Ban — guía de talles](https://india.ray-ban.com/size-guide).

Las especificaciones reales de cada producto deberán confirmarse antes de cargarlas; ninguna referencia habilita recomendaciones o diagnósticos automáticos.
