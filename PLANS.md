# Plan vigente — sistema Óptica

## Estado y límites de esta entrega

Reorganización y desglose aprobados por el usuario. Fases 0–4.1 completadas. El usuario autorizó implementar primero 5.2 y confirmó sus condiciones de seguridad. Implementación de 5.2 verificada con datos/destinos ficticios; habilitación operativa, disco real, cifrado y recuperación ante pérdida del equipo siguen pendientes. 5.1 y 5.3 continúan pendientes; no se avanza automáticamente.

Esta entrega desarrolla exclusivamente 5.2 y actualiza su documentación. Se agregó WindowsServices 10.0.12 con autorización y se generó AutomaticBackups, aditiva y no aplicada a la base operativa. No se modificaron cuentas existentes, disco externo real, BitLocker, configuración del disco ni registro del servicio Windows.

[REQUISITOS.md](REQUISITOS.md) define comportamiento; [AGENTS.md](AGENTS.md), reglas permanentes; [DECISIONES.md](DECISIONES.md), decisiones y estados; [BITACORA.md](BITACORA.md), historial y evidencia de verificaciones. El plan anterior se conserva allí como histórico, sin continuar sus numeraciones caducas.

## Fases completadas

| Fase | Entrega conservada | Evidencia histórica |
| --- | --- | --- |
| 0 | Alcance y planificación inicial | Documentos y decisiones iniciales. |
| 1 | Base técnica por capas y aplicación local | Compilación y 8 pruebas aprobadas en esa entrega. |
| 2 | Usuarios, permisos y auditoría | Compilación y 23 pruebas aprobadas en esa entrega. |
| 3 | Clientes, documento obligatorio y visitas | Compilación y 44 pruebas aprobadas en esa entrega. |
| 4 | Recetas, antecedentes, versiones y adjuntos privados | Cierre registrado con 94 pruebas aprobadas; entregas intermedias en BITACORA.md. |
| 4.1 | Reorganización funcional, trabajos y controles de calidad | 113 pruebas aprobadas y 16 comprobaciones de navegador registradas por separado. |

Estos conteos son históricos, no nuevas ejecuciones ni sumandos de una única batería. La configuración del negocio y los avances de GitHub también quedan conservados en la bitácora. La verificación de esta reorganización se registra allí antes del commit.

Estado actual que condiciona las próximas fases: gestión de cuentas con un rol por cuenta; respaldo manual consistente; sin catálogo, ventas/pagos, existencias ni encargos comerciales implementados. El trabajo óptico actual exige dos aprobaciones en dominio y SQLite y debe conservarse.

## Reglas para las próximas fases

- Área clínica y comercial conservan igual importancia. La atención clínica existente sigue disponible sin compra y no depende de completar las fases comerciales.
- Trabajar por funcionalidades pequeñas dentro de la fase aprobada; no saltar a otra fase sin autorización.
- Cada módulo incluye pantallas funcionales, permisos, auditoría, pruebas pertinentes y pasos para comprobarlo en el navegador.
- No usar datos clínicos reales hasta aceptar seguridad, cifrado, autorizaciones y recuperación. La primera prioridad operativa es resolver esta protección en fase 5.
- El inventario mínimo necesario para vender se implementa en fase 7; fase 9 lo amplía. Lotes/vencimientos aplican desde el primer manejo físico de productos que los requieran.
- La adaptación del circuito de calidad se realiza en fase 10. Hasta entonces se mantienen las reglas actuales y no se presentan pedidos nuevos como un circuito de entrega completo.

## Fases pendientes de implementación

### Fase 5 — Multirrol, seguridad y recuperación prioritaria

**Estado: 5.2 implementada y verificada en aislamiento; aceptación y habilitación operativa pendientes. 5.1 y 5.3 pendientes.**

Se mantienen tres subtareas independientes. Orden recomendado de aplicación: 5.2 (recuperación comprobada), 5.1 (multirrol), 5.3 (seguridad integral). La numeración identifica alcance, no autoriza ejecución. Cada entrega se presenta y se espera aprobación antes de iniciar otra.

Condición previa a cualquier cambio operativo: aprobar el procedimiento concreto de copia, restauración, transición y reversión correspondiente, ensayarlo con datos ficticios y verificar una copia recuperable. La revisión de esquema puede concluir que no hace falta una migración; nunca se genera por obligación. Programar/practicar en entornos aislados no autoriza aplicar cambios a la instalación de uso.

#### Subtarea 5.1 — Usuarios con múltiples roles

1. **Funcionalidades propuestas:** crear cuentas con uno o varios de los tres roles existentes; listar una fila por cuenta con todos sus roles; modificar asignaciones con confirmación, control de concurrencia, auditoría y revocación de sesiones. Preservar cuentas, contraseñas e identificadores. Mantener bloqueo/inactivación y protección del último administrador activo. Propuesta para P-04: sólo Administrador gestiona asignaciones; impedir dejar una cuenta activa sin roles y evitar cambios propios que permitan perder el acceso administrativo. No ampliar permisos de un rol por su nombre: Administrador sin Profesional sigue sin acceso clínico.
2. **Archivos/componentes previstos:** Application/Security/IAccountService.cs (contratos y resumen multirrol), Infrastructure/Identity/AccountService.cs (consultas agrupadas y asignaciones atómicas), Web/Pages/Admin/Users.cshtml y Users.cshtml.cs (selección múltiple y edición). Revisar Application/Security/Access.cs, Web/Security/HttpCurrentUser.cs y SecurityRegistration.cs sin reescribir lo que ya admite unión de roles; pruebas de autenticación/autorización y nuevas pruebas de gestión de roles. Los paths de esta sección son relativos a src/, salvo los tests de tests/.
3. **Entidades/permisos/migraciones:** AspNetUsers, AspNetRoles, AspNetUserRoles, sellos de seguridad/concurrencia y AuditEntry. Identity ya tiene relación muchos a muchos: no se prevé nueva tabla de roles ni migración estructural para habilitarla. Si se descubre otra necesidad, presentar su diff y procedimiento antes de aprobarla. Conservar users.manage y autorización clínica explícita; revisar snapshots de rol en auditoría/calidad sin alterar controles históricos.
4. **Pruebas automatizadas previstas:** siete combinaciones no vacías de roles, selección vacía/desconocida/duplicada, cuenta inactiva, listado sin filas duplicadas, último administrador, cambios propios, dos administradores editando simultáneamente, fallo intermedio con rollback, CSRF y revocación de sesión al retirar permisos. Prueba de compatibilidad con cuentas ficticias previas: mismos IDs, hashes, relaciones e historial; regresiones de clínica sin compra y calidad.
5. **Riesgos y mitigación:** pérdida de acceso, permisos excesivos o sesiones con derechos anteriores. Mantener asignaciones previas hasta un cambio explícito; transacción, sello de seguridad y comprobación de último administrador. Antes de aplicar: recuperación aprobada y ensayada, inventario privado de cuentas/permisos y copia consistente. Un fallo debe conservar la asignación anterior completa, sin cambios parciales.
6. **Prueba desde navegador:** en entorno ficticio, Administrador abre /admin/usuarios, crea una cuenta Profesional + Atención/Ventas, revisa sus dos roles en una sola fila e ingresa con ella en otra sesión. Comprueba clínica y operación; al retirar Profesional desde la sesión administradora, la sesión anterior pierde acceso clínico. Administrador solo y Atención/Ventas solos deben ser rechazados al entrar por URL directa a clínica. Intentar quitar el último administrador debe mostrar un error claro. No usar cuentas reales para esta demostración.
7. **Aceptación:** matriz P-04 aprobada; pantallas utilizables; permisos combinados exactos, sin acceso clínico implícito; cambios auditados, atómicos y efectivos sobre sesiones existentes; ninguna cuenta, contraseña, relación o historia anterior perdida. Compilación sin errores y pruebas pertinentes aprobadas; procedimiento de transición/reversión aprobado antes de aplicar cambios operativos. Cerrar y esperar aprobación de la siguiente subtarea.

#### Subtarea 5.2 — Respaldos automáticos y recuperación

**Estado: implementación autorizada y verificada en entorno ficticio; no habilitada para datos clínicos reales.**

Entrega: pantalla /admin/respaldos; frecuencia cada N días, hora/zona y retención configurables; programación desactivada y sin hora inicial. Copias manuales nuevas encoladas e independientes del cronograma; se conserva la copia local anterior. La escritura externa requiere además Backups:ExternalWritesAuthorized=true en configuración privada, sólo tras autorización de instalación; permanece false por defecto. El servidor nuevo inicia sin migraciones automáticas y muestra aviso si falta AutomaticBackups.

Se reutilizó el motor manual extrayendo ClinicalSnapshotWriter: bloqueo compartido de adjuntos y entre procesos de copia, SQLite BackupDatabase, hashes/FKs, originales y configuración permitida, manifiesto final. BackgroundService ejecuta fuera de una sesión HTTP y puede alojarse como servicio Windows; no se registró dicho servicio. Persistencia de programación/ejecuciones, recuperación de vencimientos y manifiestos completos tras corte, reintentos automáticos a 15/30/60 minutos, conservación de la última copia válida y restauración sólo a directorio aislado. El código Windows valida GUID de volumen, NTFS, disco físico independiente, espacio y ACL; el hardware real todavía no fue inspeccionado ni preparado.

Validación final: compilación 0 errores/0 advertencias; 141 pruebas ejecutadas y aprobadas, 0 fallidas/omitidas (40 Infraestructura, 101 Web); modelo/migración sin diferencias. Detalles e incidentes en BITACORA.md. La migración se ensayó hacia adelante en base ficticia y conservó hashes de todas las tablas anteriores. En la base operativa se comprobó sólo por lectura que la última migración sigue siendo QualityWorkflow y no existen las dos tablas nuevas.

Antes de usarlo realmente: revisar/autorizar migración y reversión, identidad y cambios previstos del disco, ACL y espacio; autorizar preparación de carpeta y registro del servicio/identidad Windows si corresponde; configurar destino/horario/frecuencia/retención y recién entonces habilitar programación. Cifrado y custodia/recuperación de claves requieren autorización y ensayo específicos; no usar datos clínicos reales mientras falten estas evidencias.

1. **Funcionalidades propuestas:** conservar la copia manual y agregar programación configurable, destino externo privado, retención, verificación, registro de ejecuciones y alertas visibles. Incluir SQLite consistente, todos los adjuntos/versiones/retirados y configuración necesaria. Proponer verificación/restauración de prueba en un directorio aislado, nunca sobre la base activa desde el navegador. La programación funciona mientras servidor y PC estén encendidos; mostrar tareas vencidas y reintentar de forma controlada al volver, sin prometer respaldo con el equipo apagado.
2. **Archivos/componentes previstos:** reutilizar Application/Clinical/IClinicalSnapshotService.cs e Infrastructure/Clinical/ClinicalSnapshotService.cs; extraer sólo el núcleo reutilizable para ejecución manual/programada, sin segundo motor de copias. Revisar ClinicalFileGate, StoragePaths, StorageProtection, DependencyInjection.cs y Web/Program.cs. Agregar contratos/servicios de Backups, programador hospedado y pantalla administrativa dedicada enlazada desde la administración; conservar compatibilidad de Pages/Admin/ClinicalFiles.cshtml y .cshtml.cs. Ampliar tests/OpticaSosa.Web.Tests/AttachmentBackupTests.cs y agregar pruebas de programación/recuperación/capacidad; configuración privada fuera de Git.
3. **Entidades/permisos/migraciones:** propuesta mínima BackupSettings y BackupRun (programación/destino, resultado, fechas, integridad y error seguro), con migración aditiva sólo si se aprueba esa persistencia. Sin cambios en tablas clínicas ni reescritura de archivos. Proponer permisos administrativos explícitos backups.manage y backups.verify; restaurar sobre instalación activa no se ofrece como permiso web. El proceso automático usa un contexto de sistema acotado y auditado, no una cuenta de empleado falsa ni privilegios clínicos. Manifiesto con versión de esquema/aplicación; claves y secretos requieren estrategia separada de custodia, no copia en claro indiscriminada.
4. **Pruebas automatizadas previstas:** reloj simulado para horarios/reinicio; exclusión de copias simultáneas; fallos por disco lleno, permisos, USB ausente, corte y cancelación; carpetas incompletas no restaurables, hashes alterados y relaciones inválidas rechazados; retención sin borrar la última copia válida; backup durante escritura clínica y carga/retiro de adjuntos. Restauración aislada comparando clientes, cuentas/roles, recetas/versiones, profesionales, adjuntos y controles de calidad, con descargas byte a byte. Verificar que datos y sesiones de la instalación original no se usan ni se modifican durante el ensayo.
5. **Riesgos y mitigación:** copia inconsistente, respaldo sin documentos, pérdida por retención, copia externa expuesta o restauración sobre datos actuales. Usar API de backup SQLite y coordinación de archivos, publicación de copia completa sólo tras verificarla, protección de rutas/enlaces y directorios aislados. Antes de configurar/aplicar: aprobar P-01 y el procedimiento de recuperación, revisar la migración si existe, preparar copia previa y plan de reversión. La restauración que reemplaza la instalación requiere autorización específica y ventana de mantenimiento; puede perder cambios posteriores a la copia, por lo que debe explicitarse el punto de recuperación.
6. **Prueba desde navegador:** Administrador abre la futura pantalla Respaldos, configura un destino de prueba aprobado, ejecuta una copia y ve fecha, resultado, archivos y próxima ejecución. Prueba una programación corta con datos ficticios; ante destino ausente debe ver alerta sin perder la copia anterior. Solicita verificación/restauración aislada y consulta un informe sin contenido clínico ni secretos. Para comprobar documentos y calidad recuperados se abre una instancia de prueba separada, identificada claramente, con los roles ficticios autorizados; no se habilita un navegador de archivos privados ni descarga completa de base para cualquier empleado.
7. **Aceptación:** parámetros P-01 y procedimiento aprobados; copia automática y manual verificadas en destino fuera del disco principal, con fallos visibles y retención segura. Ensayo de recuperación de base, documentos y configuración reproducible, preservando relaciones y calidad; tiempos y pérdida máxima medidos frente a lo acordado. No declarar resuelta la pérdida del equipo sin comprobar acceso y recuperación de claves en un entorno independiente. Cifrado sólo se evalúa/documenta: activarlo requiere autorización específica posterior, prueba de recuperación y nueva comprobación. Cerrar y esperar aprobación; no habilitar datos clínicos reales por completar sólo esta subtarea.

##### Diseño propuesto de 5.2 — revisión del mecanismo existente

**Reutilización y límites comprobados.** ClinicalSnapshotService ya usa BackupDatabase, verifica integridad SQLite/FKs y hashes de originales, conserva documentos retirados y escribe complete.json al terminar. IClinicalSnapshotService y la pantalla ClinicalFiles mantienen la entrada manual. ClinicalFileGate es compartido por carga, recuperación, optimización y retiro de adjuntos; los originales se publican sin sobrescritura. AttachmentBackupTests ya comprueba una restauración aislada y que una copia fallida no reemplace la anterior. Falta programación, destino externo, retención, recuperación operativa y configuración externa; la capacidad actual sólo mide el volumen de datos y el servicio exige un usuario HTTP autorizado. Se ampliarán estos puntos sin reemplazar el motor ni abrir permisos clínicos.

**Componentes nuevos y cambios acotados.** Agregar un coordinador de ejecuciones manuales/programadas, BackgroundService con TimeProvider, política de retención, validador de destino y verificador/restaurador exclusivamente aislado. Agregar contratos y pantalla Respaldos con configuración, última copia válida, historial paginado, próxima ejecución y alerta persistente. Proponer sólo BackupSettings y BackupRun como tablas nuevas, mediante migración aditiva revisada: horario/zona, destino privado validado, retención, concurrencia, instante programado, inicio/fin, disparador, estado y error sin contenido sensible. Reutilizar auditoría con actor humano o identidad de sistema diferenciada; no crear una cuenta Identity de respaldo. Revisar DI, Program, configuración y políticas. No cambiar entidades clínicas ni usuarios.

**Programación Windows.** Recomendar BackgroundService dentro del mismo proceso ASP.NET Core y alojar ese proceso como servicio Windows con inicio automático/recuperación ante fallo. No depende de navegador ni inicio de sesión de un empleado. Mantener un solo servidor por instalación: el bloqueo de adjuntos actual es en memoria y no coordina otro proceso independiente. Agregar exclusión por instalación/proceso y serialización de copias manuales/programadas. No usar una tarea Windows que abra SQLite y copie adjuntos por su cuenta. El servicio requiere preparar alojamiento, rutas absolutas, identidad Windows con mínimo privilegio y persistencia de claves; Microsoft.Extensions.Hosting.WindowsServices no está instalado y cualquier dependencia/registro del servicio requerirá autorización posterior. No se instalará ni activará nada al aprobar sólo este documento; Wi-Fi y despliegue integral de fase 12 siguen pendientes.

**Horario y reinicios.** Ejecutar diariamente a la hora local configurada y persistir instantes UTC y zona horaria. Identificar cada vencimiento por fecha local y configuración, sin duplicarlo por reinicio, reintento o cambio de reloj. Al iniciar, detectar ejecuciones en curso interrumpidas y períodos vencidos; crear una única copia de recuperación del estado actual y registrar días omitidos, sin simular copias históricas inexistentes. Propuesta: reintentos a los 15, 30 y 60 minutos y luego cada hora mientras la causa sea transitoria; errores de integridad/configuración requieren intervención, con alerta. Ningún reintento se superpone con otra copia. La copia manual no borra el registro de una ejecución perdida. Mostrar atrasos y distinguir próxima hora diaria del próximo reintento. Con PC o servicio apagados no hay copias; al volver se recupera la programación, no el estado pasado perdido.

**Consistencia y publicación.** Tomar el bloqueo compartido antes de enumerar originales y capturar SQLite con su API de backup. Mantenerlo durante copia/verificación de documentos; los lectores pueden continuar y la escritura de adjuntos espera con respuesta controlada. No bloquear arbitrariamente todas las operaciones clínicas: SQLite proporciona el punto consistente de la base y sus referencias; los originales son inmutables y el bloqueo evita cambios del conjunto de archivos. Conservar estados Pending/Failed de cargas interrumpidas sin inventar archivos disponibles. Capturar configuración versionada y manifiesto de versión de aplicación/esquema, tamaños/hashes y requisitos de restauración. Validar cada original requerido por la base copiada, incluidas versiones y retiros. Un archivo ausente o corrupto impide declarar válida la copia; no modificar el original para corregirla. Escribir en un directorio único incompleto y publicar estado completo sólo tras cerrar/verificar datos. Nunca usar una copia ordinaria del SQLite activo ni dar por válido sólo un hash sin verificar relaciones.

**Destino y retención.** Proponer disco externo NTFS dedicado, independiente del disco físico que contiene la base y de los temporales. Validar dispositivo/volumen esperado, no sólo letra de unidad; otra partición del mismo disco no es copia independiente. Rechazar carpeta pública, destino dentro del árbol de origen, enlaces, volumen incorrecto, permisos insuficientes o espacio insuficiente. Verificar espacio y margen en origen si hay staging y en destino, contemplando crecimiento; no borrar la última copia buena para intentar liberar espacio. Usar ACL para identidad del servicio y custodios Windows autorizados; el administrador de la aplicación no obtiene acceso clínico ni un explorador/descarga de base por ese permiso. El acceso físico o como administrador Windows requiere custodia propia y cifrado: las ACL solas no protegen frente a extracción del disco. Retención configurable por cantidad de copias diarias verificadas; depurar sólo carpetas creadas por este sistema después de una copia nueva válida. Revalidar la copia que se conserva antes de borrar anteriores, proteger como mínimo la última válida y detener depuración ante corrupción o destino incorrecto. Registrar limpiezas y conservar archivos incompletos identificados para recuperación/limpieza controlada, nunca como copias buenas.

**Material restaurable.** Incluir base completa (usuarios, configuración del negocio, clientes, clínica, profesionales y calidad), todos los originales y estados necesarios, configuración operativa indispensable, manifiesto y procedimiento. Inventariar claves ASP.NET Core Data Protection/certificados y otros secretos por separado: no copiar indiscriminadamente perfiles Windows ni credenciales en claro. Documentar la versión de aplicación/runtime y cómo recuperar una versión compatible. No borrar datos porque una configuración nueva reduzca límites. La prueba verifica también logo/configuración, cuentas/roles y controles históricos, no sólo tablas clínicas y archivos.

**Cifrado y claves — propuesta sin activación.** Evaluar BitLocker para los volúmenes reales de datos y temporales, y BitLocker To Go para el disco externo compatible. No cambiar a otro motor SQLite ni introducir criptografía propia. Definir desbloqueo operativo del destino; si está bloqueado mostrar alerta y no guardar copia en un lugar alternativo en claro. BitLocker protege el volumen cerrado, no sustituye permisos cuando está desbloqueado. Crear y verificar recuperación antes de activar: identificar cada volumen/protector, guardar recuperación en dos custodias protegidas separadas del equipo y disco respaldado, probar desbloqueo en equipo independiente y registrar evidencia sin claves. No incluir claves en Git, logs, capturas o el mismo paquete accesible de respaldo. Las claves DPAPI/Data Protection pueden depender de equipo/cuenta: copiar archivos no demuestra portabilidad. Confirmar si sólo protegen cookies/tokens (cerrar sesiones y volver a ingresar al recuperar) o también datos persistidos; en este último caso exigir exportación recuperable protegida y prueba antes de declarar la copia completa. La identidad Windows del servicio y cualquier cambio de protección de claves requieren procedimiento aprobado; no cambiar contraseñas ni cuentas de la aplicación. Ningún cifrado se activa como efecto implícito de esta subtarea.

**Recuperación segura.** Validar manifiesto/hashes, versión, SQLite y referencias; restaurar en directorio vacío, privado y separado, sin sobrescribir. En datos ficticios, arrancar instancia separada sólo en localhost, con puerto/directorio/cookies diferenciados y programador deshabilitado: no debe ejecutar tareas ni usar destinos de producción. Consultar con los roles ficticios correspondientes y comparar originales, relaciones, configuración y calidad. Recuperación operativa real requiere aprobación específica, servicio detenido, copia del estado actual, punto de recuperación y pérdida esperada explícitos, validaciones, plan de reversión y recién entonces cambio controlado de destino. No agregar botón web de restauración sobre la base activa. No afirmar recuperación completa ante pérdida del equipo mientras no se haya ensayado sin depender de sus claves locales.

**Pantallas y permisos.** Proponer backups.manage y backups.verify para Administrador, sin clinical.read ni clinical.write implícitos; preservar permisos/documentos clínicos sólo para Profesional. La pantalla muestra metadatos y resultado seguro, nunca nombres/contenido de pacientes, claves ni descarga integral de base. Crear/verificar manualmente debe encolarse y continuar aunque cierre la pestaña; la solicitud web no es dueña del ciclo de vida del trabajo. El programador usa un caso de uso interno restringido, sin construir un usuario HTTP falso ni conceder a empleados permisos del servicio.

**Pruebas previstas.** Reloj simulado para horario, zona, cambio de reloj, reinicios y días perdidos; programación sin navegador; idempotencia y exclusión manual/automática; captura concurrente con cargas/retiros/recuperaciones; caída antes/después de publicar manifiesto y antes de guardar resultado; corte de destino, disco lleno en ambos volúmenes y permisos denegados. Rechazo de volumen equivocado, ruta pública/enlace y directorio origen; corrupción de SQLite, manifiesto y originales; recuperación de ejecuciones interrumpidas y retención que conserva la última válida. Restauración ficticia completa e independiente, sin escrituras al origen, sin programador y con descarga byte a byte; permisos/CSRF y ausencia de datos clínicos en pantalla/logs. Comprobar ACL, disco físico distinto, reinicio real del servicio y recuperación de claves mediante verificaciones operativas aparte; no atribuirlas a tests HTTP. Cifrado activado y ensayo sobre hardware se mantienen pendientes de autorización y medios disponibles.

**Parámetros por confirmar:** P-01 contiene la única lista vigente; todos sus valores propuestos necesitan confirmación. Al aprobar diseño todavía deben revisarse dependencias, procedimiento de migración y aplicación operativa; no se inicia 5.1 ni 5.3.

Fuentes oficiales consultadas para el diseño: [SQLite Online Backup](https://sqlite.org/backup.html), [servicios hospedados ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0), [alojamiento como servicio Windows](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0), [recuperación BitLocker](https://learn.microsoft.com/en-us/windows/security/operating-system-security/data-protection/bitlocker/recovery-overview) y [configuración Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

#### Subtarea 5.3 — Verificación integral de seguridad

1. **Funcionalidades propuestas:** comprobar los controles existentes y los resultados de 5.1/5.2, corregir exclusivamente hallazgos dentro del alcance aprobado y emitir informe de evidencias y pendientes. Verificar acceso por rol, sesiones, CSRF, archivos privados, auditoría sin datos sensibles, permisos Windows, respaldos y recuperación. No crear un tablero que afirme seguridad por detectar configuraciones, ni un sistema de seguridad paralelo. Cifrado se inventaría y evalúa, nunca se activa en esta subtarea sin aprobación específica.
2. **Archivos/componentes previstos:** ampliar tests/OpticaSosa.Web.Tests/AuthenticationTests.cs, AuthorizationTests.cs, ClinicalHttpTests.cs, AttachmentHttpTests.cs, AttachmentBackupTests.cs, QualityHttpTests.cs y las nuevas pruebas 5.1/5.2; tests/OpticaSosa.Infrastructure.Tests/StorageProtectionTests.cs. Revisar Web/Security/SecurityRegistration.cs, HttpCurrentUser.cs, Infrastructure/Identity/SecurityAudit.cs y persistencia/almacenamiento privado; tocar producción sólo si una prueba demuestra un defecto y documentar el archivo concreto. Registrar informe en BITACORA.md y resultado en PLANS.md; decisiones nuevas en DECISIONES.md si corresponde.
3. **Entidades/permisos/migraciones:** ninguna nueva entidad o migración prevista por la verificación. Comprobar todas las políticas relevantes y ausencia de acceso clínico implícito. Cualquier corrección que requiera cambiar datos, roles, esquema o claves debe presentarse con procedimiento y aprobación antes de aplicarse.
4. **Pruebas automatizadas previstas:** acceso anónimo y URL directa, roles simples/combinados, manipulación de IDs entre clientes/registros, CSRF, revocación/inactivos, rutas maliciosas, enlaces, MIME/tamaño/contenido falso, alteración de hashes, descarga no autorizada, datos sensibles en logs y ausencia de archivos privados en recursos públicos. Repetir restauración y regresiones de clientes, clínica sin compra, profesionales, historias extensas, adjuntos y controles de calidad. Permisos del equipo, custodia de claves y pérdida física requieren además verificaciones operativas documentadas; no atribuirlas a tests HTTP.
5. **Riesgos y mitigación:** falsos positivos de seguridad, tests contra datos de uso o correcciones que alteren autorizaciones. Usar datos ficticios y entornos aislados, separar evidencia automatizada de comprobación operativa, no modificar ACL/cifrado/firewall/base activa durante una inspección y revisar cada corrección. No publicar información privada, rutas sensibles o claves en informes.
6. **Prueba desde navegador:** ejecutar una matriz guiada con cuentas ficticias en sesiones separadas: clientes disponibles según permiso, clínica/documentos sólo con Profesional, administración sólo con permiso, calidad técnica y entrega conforme a políticas existentes. Probar enlaces directos y sesión revocada; revisar auditoría y estado de respaldos como Administrador. Navegador permite comprobar comportamiento, pero no certifica cifrado de disco ni recuperación de claves; mostrar esas evidencias en el informe.
7. **Aceptación:** toda la batería pertinente aprobada, módulos anteriores conservados y sin hallazgos críticos/altos abiertos para el entorno evaluado; pruebas de recuperación repetibles y permisos verificados. Informar compilación y conteos reales de pruebas, más controles operativos y limitaciones. Si cifrado/custodia/recuperación o algún control requerido sigue pendiente, cerrar el informe con ese bloqueo y mantener prohibido el uso de datos clínicos reales. Operación segura de celulares/HTTPS sigue sujeta a fase 12; no declarar seguridad Wi-Fi ya validada. No avanzar a fase 6 automáticamente.

**Conservación obligatoria:** ninguna subtarea elimina ni reduce clientes, recetas médicas/ópticas, graduaciones, historial clínico, adjuntos, profesionales ni calidad. Fase 10 reutilizará OpticalWork, QualityControl, QualityCatalog y los servicios/pantallas de fase 4.1; adaptará aplicabilidad mediante aprobación y pruebas, sin reconstruir el motor ni borrar aprobaciones anteriores.

### Fase 6 — Catálogo y proveedores prácticos

**Estado: pendiente.**

Tareas: pantallas de armazones, cristales, contactos y accesorios; códigos, variantes tipadas realmente utilizadas, unidades, precios y estado; proveedores/laboratorios y contactos. Separar atributos de producto de graduaciones individuales y montaje.

Criterios de aceptación: alta, edición, búsqueda e inactivación desde navegador, permisos y auditoría; sin duplicados ni existencias ficticias; variantes y unidades comprensibles. Resolver P-06 en lo aplicable. No implementar ofertas ni compras agrupadas.

Pruebas: códigos/variantes duplicados, precios exactos, parámetros pertinentes, permisos y conservación de referencias al inactivar.

### Fase 7 — Pedidos, ventas, señas y stock mínimo seguro

**Estado: pendiente.**

Tareas: pedidos comerciales y vínculos con trabajos/versiones técnicas; materiales de stock, de laboratorio y mixtos; precios, descuentos, moneda, pagos parciales, saldo y fechas prometidas. Implementar existencias iniciales trazables, entradas mínimas, reservas/liberaciones y faltantes desde esta fase. Proyección técnica mínima de sólo lectura para operación autorizada.

Criterios de aceptación: circuito práctico en navegador para pedido, seña, saldo, reserva y faltante; disponibilidad real, sin negativos ni doble reserva; material encargado no disponible antes de recibirlo. Publicación técnica autorizada y versionada sin acceso comercial a historia completa. Resolver P-02 y P-03 antes de sus implementaciones. No inventar ventas históricas ni habilitar entrega nueva completa antes de fase 10.

Pruebas: importes/redondeo, pago repetido o anulado, reserva concurrente de última unidad, cancelaciones, pedido mixto/por ojo, material no recibido o vencido, separación de permisos y atención clínica sin compra.

### Fase 8 — Encargos simples y recepción de laboratorio

**Estado: pendiente.**

Tareas: laboratorio, materiales/cantidades, autorización técnica y comercial, fechas estimadas/reales y estados del encargo; demoras, observaciones/correcciones y recepción por material. Documentación mínima exportable sin mensajería automática.

Criterios de aceptación: seguir y recibir un encargo desde navegador; cantidad recibida válida incorporada una sola vez al inventario existente; recepción no equivale a calidad ni entrega. Exportar no marca enviado. Resolver reglas de recepción/cancelación y datos de P-06; no introducir asignaciones de compras agrupadas.

Pruebas: permisos por proyección, cambios técnicos, envíos registrados, demora, recepción parcial, material observado, cancelación y reintento sin duplicar existencias.

### Fase 9 — Inventario ampliado

**Estado: pendiente.**

Tareas: ampliar el núcleo existente con movimientos, ajustes justificados, bajas, devoluciones, alertas y vistas reconciliables; lotes, vencimientos y conversiones exactas de presentaciones cuando correspondan.

Criterios de aceptación: administración práctica desde navegador; separar físico/reservado/disponible/no utilizable/pendiente; conservar trazabilidad sin editar saldos directamente ni crear un segundo libro de inventario. No postergar hasta aquí validaciones indispensables para fase 7.

Pruebas: reconciliación, ajuste/compensación, reintentos, última unidad concurrente, vencimiento posterior a reserva, paquetes/unidades, cancelación sin reponer consumidos y devolución revisada.

### Fase 10 — Calidad y entrega según producto/trabajo

**Estado: pendiente.**

Tareas: aprobar matriz de controles aplicables y roles; adaptar circuito existente sin duplicarlo; revisar restricciones de dominio y SQLite, compatibilidad y migración; preparación y entrega con materiales válidos y regla de pago acordada.

Criterios de aceptación: pruebas desde navegador por tipo de trabajo/producto; historia anterior conservada; ningún “no aplica” evita controles esenciales ni obliga a aprobaciones ficticias. Entregar sólo con controles aplicables aprobados y materiales disponibles/no vencidos; entrega auditada e idempotente. Resolver P-05 antes de alterar reglas existentes.

Pruebas: trabajos y aprobaciones históricos, estados listos/entregados, observaciones y cambios que invalidan aprobación, permisos, contactos vencidos, faltantes, deuda y entregas parciales si se aprueban.

### Fase 11 — Caja básica e informes operativos

**Estado: pendiente.**

Tareas: caja, ingresos por medio de pago, cierres y reportes básicos conciliados con pedidos, pagos y anulaciones, con permisos explícitos.

Criterios de aceptación: consulta y cierre desde navegador con cifras reconciliables y trazabilidad; sin contabilidad integral ni reportes que filtren información clínica o financiera fuera de permiso.

Pruebas: cierre, anulaciones, medios de pago, moneda, saldos y diferencias justificadas; regresiones del circuito de pagos.

### Fase 12 — Experiencia final y operación Windows/Wi-Fi

**Estado: pendiente.**

Tareas: rediseño visual definitivo, navegación y accesibilidad; operación en PC y tres celulares, HTTPS/firewall privados, recursos locales, instalación y actualización seguras con respaldo.

Criterios de aceptación: cuatro sesiones simultáneas en red local sin internet, pantallas adaptables, desconexión/reintento comprensibles y actualización que preserve datos. Revalidar recuperación y condiciones de uso de datos reales.

Pruebas: recorridos completos por rol, tamaños de pantalla, concurrencia, recursos sin internet, permisos de red, interrupciones y recuperación tras actualización.

## Confirmaciones pendientes — única lista vigente

Estas decisiones no bloquean la reorganización documental; deben resolverse antes de implementar el comportamiento afectado.

| ID | Confirmación necesaria | Antes de |
| --- | --- | --- |
| P-01 | Disco externo disponible, pero identidad, NTFS/compatibilidad, capacidad, ACL e independencia física reales pendientes de revisión de sólo lectura y autorización de cambios. Horario, frecuencia cada N días, destino, activación y retención se eligen en pantalla; programación desactivada, sin hora obligatoria. Confirmar objetivos de pérdida/recuperación, responsables de alertas, identidad Windows del servicio y dos custodias separadas de claves; margen de espacio y equipo/disco para ensayo operativo. BitLocker/To Go, preparación de carpeta, migración operativa y registro del servicio no se activan sin autorización específica. | Habilitación operativa de 5.2. |
| P-02 | Campos exactos de la proyección técnica comercial, destinatarios, aprobación/publicación y efecto de corregir o revocar una versión. Nunca incluir historia completa ni permitir editar graduaciones. | Proyección de fase 7 y documentos de fase 8. |
| P-03 | Moneda, escalas/redondeo, descuentos autorizados, excedentes/devoluciones, anulaciones, permisos de pagos y entrega con deuda. | Ventas/pagos de fase 7. |
| P-04 | Matriz multirrol, quién asigna/revoca roles, cambios de permisos propios, último administrador e invalidación de sesiones. | Cambios de cuentas de fase 5. |
| P-05 | Controles esenciales y aplicabilidad por producto/trabajo, transición de restricciones actuales y reglas de entrega parcial. | Adaptación de fase 10. |
| P-06 | Variantes/unidades y datos mínimos del catálogo/proveedor; autorización comercial, recepción parcial, material observado y cancelación del encargo. | Catálogo de fase 6 y encargos de fase 8, según corresponda. |

## Seguimiento de esta entrega y próxima acción

- Reorganización documental completada y verificada; resultados registrados en BITACORA.md. Commit y push documentales se comprueban e informan al cierre, sin un commit adicional para su propio identificador.
- Plan anterior y decisiones D-01–D-36 conservados con su contexto histórico; D-37–D-46 registran la aprobación y sus límites.
- Nueva entrega de planificación: inspeccionados los contratos/cuentas Identity, políticas, pantalla de usuarios, copia manual y pruebas existentes. Se desglosó fase 5 en 5.1, 5.2 y 5.3; sólo se modificó PLANS.md. No se ejecutaron compilación ni pruebas automatizadas nuevas: no hay cambios de aplicación ni commit de implementación en esta entrega.
- El usuario aprobó el desglose y eligió 5.2 primero, condicionada a revisar/aprobar el diseño antes de programar. Se revisaron motor manual, gate compartido, cargas/recuperación/retiro, capacidad, protección Windows y pruebas; se documentaron diseño y parámetros propuestos. Sólo se modificó PLANS.md; revisión documental, sin compilación ni pruebas nuevas.
- El usuario autorizó implementar 5.2 con programación desactivada y destinos aislados, y autorizó sólo el paquete WindowsServices. Desarrollo y 141 pruebas verificados; publicación del avance según política Git. La habilitación de la base/disco/servicio reales y P-01 siguen pendientes; no avanzar a 5.1/5.3.
