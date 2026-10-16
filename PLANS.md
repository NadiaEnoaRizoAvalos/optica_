# Plan de trabajo — Sistema de Gestión para Ópticas

## Estado actual y reglas de avance

- Fase 0: documentación inicial aprobada por el usuario.
- Fase 1: aceptada por el usuario tras visualizar la aplicación; autorizó fase 2. No se afirma haber realizado una prueba con internet físicamente desconectado.
- Fase 2: aceptada al autorizar expresamente fase 3; existe una cuenta de acceso, conservada durante la actualización.
- Fase 3 y tarea adicional de configuración/GitHub: aceptadas al pedir expresamente continuar con fase 4. Primer push realizado y verificado en main, commit 2c4d495.
- Fase 4: recetas, antecedentes y adjuntos implementados con conservación de originales, sin cupos de cantidad y con tamaños administrables. Verificación automatizada y actualización local completadas; pendiente aceptación visual del usuario. Fase 5 no iniciada.
- Última suite: 94 pruebas ejecutadas y aprobadas (26 Infraestructura y 68 Web), 0 fallidas, 0 omitidas; 20 pruebas nuevas respecto de la entrega de 74. Compilación: 0 errores y 0 advertencias. Todos los datos de pruebas son ficticios y están aislados de la base local.
- Trabajar una funcionalidad por tarea; marcar cada tarea y registrar su validación al terminar.
- El cierre de cada fase requiere cumplir sus criterios y aprobación explícita para avanzar. No encadenar fases automáticamente.
- Solicitar autorización específica para instalaciones y cambios destructivos.

## Fase 0 — Planificación inicial

- [x] Verificar ubicación del proyecto: `G:\WINDOWS\Desktop\optica`.
- [x] Crear AGENTS.md con reglas permanentes.
- [x] Crear REQUISITOS.md con alcance, modelo conceptual y requisitos.
- [x] Crear PLANS.md con fases y criterios de aceptación.
- [x] Obtener aprobación para comenzar fase 1.

Criterio de aceptación: sólo los tres Markdown solicitados, arquitectura explicada y alcance pendiente de aprobación. Validación: revisión documental y comprobación de archivos; no corresponde ejecutar pruebas de aplicación.

## Fase 1 — Validación técnica y base de arquitectura

- [x] Confirmar Windows, recursos, herramientas existentes y forma de ejecución del servidor mediante inspección no destructiva. Windows 11 Pro x64, aproximadamente 15 GiB utilizables; ejecución manual en localhost.
- [x] Confirmar versiones compatibles de .NET 10, EF Core y SQLite; SDK 10.0.402 y proveedor EF Core SQLite 10.0.12 instalados con autorización y verificados mediante compilación y pruebas.
- [x] Validar alcance mínimo, roles y regla de cliente nuevo/recurrente con la óptica. Usuario confirmó roles y criterio de visita previa; matriz detallada se define en fase 2.
- [x] Registrar decisión del modo Blazor: soporte interactivo en servidor; pantalla inicial estática. Reconexión pendiente para la primera operación interactiva.
- [x] Definir estructura Dominio, Aplicación, Infraestructura y Web y proyecto de pruebas de Infraestructura. No crear contratos de negocio vacíos; se definirán junto con sus casos de uso.
- [x] Crear la solución mínima, configuración por entorno y una pantalla de comprobación, después de las autorizaciones correspondientes. Compilación y HTTP local verificados.
- [x] Definir carpetas de datos y adjuntos, contexto inicial sin entidades, fábrica por operación y procedimiento de migraciones documentado en README. No se creó base operativa ni esquema de negocio.

Criterios de aceptación: solución compila en la PC; interfaz mínima abre en localhost sin internet; dependencias entre capas respetadas; sin secretos; decisiones y verificaciones registradas. Presentar resultado y pedir aprobación de fase 2.

## Fase 2 — Acceso y protección básica

- [x] Implementar alta controlada del primer administrador y usuarios individuales. Comando local interactivo sin claves predeterminadas; creación inicial bloqueada al existir cuentas. El usuario debe ejecutarlo para elegir su contraseña.
- [x] Implementar inicio/cierre, cambio de contraseña propia, bloqueo, recuperación local de administrador, roles y políticas. Matriz concreta documentada en README para aceptación de la entrega: administración de cuentas/auditoría sólo Administrador y permisos clínicos sólo Profesional.
- [x] Incorporar auditoría mínima y logs sin datos sensibles. Acciones e IDs internos; últimos 100 eventos visibles sólo a administradores.
- [x] Definir y probar ACL de Windows. Aplicadas a data: usuario ejecutor, SYSTEM y administradores; herencia amplia deshabilitada. Cifrado y respaldos siguen pendientes antes de datos reales.
- [x] Probar rechazo de acceso anónimo, CSRF y operaciones sin permiso en endpoints y Aplicación. Verificar cuentas inactivas y revocación de sesiones.

Entrega aceptada al autorizar fase 3. Existe una cuenta elegida por el usuario; no se generaron credenciales en el chat ni cuentas de prueba en la base de uso local.

Criterios de aceptación: permisos comprobados por rol; cuentas inactivas sin acceso; sin credenciales predeterminadas ni datos reales. Aprobar antes de fase 3.

## Fase 3 — Clientes y visitas

- [x] Diseñar y migrar Customer y Visit con claves estables, documento normalizado único, índices, versiones de concurrencia, claves foráneas restrictivas y una sola visita en curso por cliente. Preparada clave compuesta cliente/visita para futuras referencias clínicas coherentes.
- [x] Implementar alta, consulta, edición y búsqueda por nombres sin depender de tildes/orden, documento, teléfono y correo. Documento obligatorio, único incluso inactivo. Coincidencias de nombre/teléfono requieren confirmación explícita, sin fusionar fichas. Máximo 100 resultados por búsqueda, con aviso para refinar.
- [x] Implementar inactivación con confirmación sólo por Administrador, conservando historial; no se permite mientras haya una visita en curso. Visitas administrativas con inicio, finalización/cancelación, autoría y paginación de 25 en 25, sin contenido clínico.
- [x] Aplicar clasificación: ficha para próxima atención según existencia de visitas completadas; visita histórica conserva clasificación calculada al inicio, contando sólo finalizaciones anteriores. Canceladas no convierten al cliente en recurrente.
- [x] Probar validaciones, SQLite, duplicados y reenvíos, búsqueda, conflictos de edición, clasificación, historial, claves foráneas, permisos en servidor/Aplicación, CSRF y formularios Blazor SSR.

Pendiente de aceptación: ingresar con la cuenta existente en `http://localhost:5080/clientes` y recorrer el módulo con datos ficticios. Recetas, antecedentes y adjuntos médicos permanecen en fase 4 y no se implementaron.

Criterios de aceptación: recorrido completo de atención con datos ficticios, historial conservado y clasificación coherente. Aprobar antes de fase 4.

## Fase 4 — Recetas, antecedentes y adjuntos

Antes de iniciar esta fase se realizará la tarea adicional autorizada de GitHub y configuración del negocio. No se modifica el alcance clínico ni se adelanta su implementación.

### Tarea adicional autorizada: GitHub y personalización

- [x] Revisar estado inicial: Git instalado, sin repositorio local ni remoto configurado. Inicializado main y configurado origin con la URL proporcionada por el usuario, que termina en optica_.git.
- [x] Autenticar mediante Git Credential Manager y verificar por API GitHub que NadiaEnoaRizoAvalos/optica_ es privado y permite push. Remoto vacío: no hay historial ajeno para fusionar ni sobrescribir.
- [x] Extender .gitignore, conservar código/pruebas/documentación y crear ARCHIVOS_PARA_GITHUB.md con los 117 archivos de la primera publicación. Revisados patrones de secretos y literales de correo; sin hallazgos de credenciales reales ni archivos binarios inesperados. Datos y respaldos quedan excluidos.
- [x] Incorporar reglas permanentes en AGENTS.md: revisión, build/tests, progreso, comprobación de privacidad, commits locales coherentes y pushes posteriores automáticos al mismo remoto privado una vez aprobado el primero. Sin autorizaciones implícitas para fase 4 ni cambios destructivos.
- [x] Eliminar la marca comercial fija de títulos y cabeceras, sin renombrar proyectos/namespaces. Nombre genérico: Sistema de Gestión para Ópticas.
- [x] Implementar configuración administrativa separada en SQLite: nombre/logo, ubicación, contactos, CUIT y datos adicionales, todos opcionales; fallback genérico y vista de la identidad guardada. Control de concurrencia y auditoría sin copiar datos del establecimiento.
- [x] Validar PNG/JPEG por bytes/extensión/MIME, tamaños y decodificación; reencodificar como PNG sin metadatos originales, máximo 512 px, y guardar BLOB privado. SkiaSharp y dependencias nativas instaladas con autorización explícita.
- [x] Verificar persistencia tras cierre/reinicio de dos hosts de prueba, permisos HTTP/Aplicación, CSRF, cambios obsoletos, retiro de logo y continuidad de autenticación/clientes.
- [x] Compilar y ejecutar suite completa: 0 errores/advertencias, 54 aprobadas (20 Infraestructura, 34 Web), 0 fallidas/omitidas. Modelo sin diferencias respecto a migración.
- [x] Crear copia SQLite y adjuntos antes de migrar, verificarla y aplicar BusinessConfiguration explícitamente; Up sólo crea una tabla, sin borrar registros. Comparación posterior: diez tablas previas intactas e integridad correcta. No se guardó una marca ficticia en la base local.
- [x] Preparar entrega verificada e inventario para el commit local y revisión del primer push. El identificador del commit se informa al entregar y se consulta con git log -1; no se incluye un hash autorreferencial en estos archivos.
- [x] Obtener aprobación del inventario para el primer push al remoto privado. El usuario aprobó el commit inicial b640541 y solicitó consolidar la política de versionado; se publica junto con esa actualización documental coherente, sin reescribir el commit inicial. Verificar main local contra origin/main al entregar.
- [x] Obtener aceptación de esta tarea y autorización separada antes de fase 4: el usuario pidió expresamente continuar con fase 4.

Verificación local: servidor dejado activo en localhost:5080; ingreso 200 con nombre genérico y sin marca fija, CSS 200, configuración/clientes anónimos 302 al ingreso, stderr vacío. La interfaz administrativa y la persistencia se comprobaron automáticamente en bases ficticias; la revisión visual manual corresponde al usuario.

Configuración Git/GitHub — 2026-10-15: revisados Git, autor existente, origin y rama main; GitHub confirmó nuevamente repositorio privado y permiso de escritura. El remoto seguía vacío antes de publicar. Revisión del historial inicial: 117 archivos, inventario coincidente, sin patrones de secretos, correos personales en archivos ni rutas de datos privados; credenciales de tests ficticias. No se modifica código de aplicación en esta tarea. La política permanente agrega exclusión explícita de commits vacíos/temporales, verificación de continuidad funcional y confirmación de publicación por SHA remoto. La compilación inicial encontró DLL bloqueadas por el servidor abierto; se detuvo sólo esa instancia. La repetición de `dotnet build OpticaSosa.sln --no-restore` terminó con 0 errores y 0 advertencias; `dotnet test OpticaSosa.sln --no-build --no-restore --verbosity minimal` aprobó las 54 pruebas (20 Infraestructura y 34 Web), sin fallidas ni omitidas. Se reinicia el servidor al finalizar. No se borra ni sobrescribe código ni historial.

Copia previa privada: data/migration-backups/pre-negocio-2026-10-15-54128ddd3dce4dc781c428fd527ccb62. Es una copia para esta migración, no reemplaza los respaldos operativos de fase 7. Licencias comerciales del producto, pruebas gratuitas y panel de ventas siguen pospuestos.

### Alcance clínico de fase 4 — implementación verificada

- [x] Completar validación de alcance: graduaciones con 3 decimales y distancia pupilar binocular en mm con 2 decimales. El usuario reemplazó la propuesta anterior: sin máximo fijo de recetas, consultas, antecedentes ni adjuntos; fotos 25 MB y PDF 50 MB por archivo, configurables; acceso documental sólo Profesional. Evaluar HEIC/HEIF sin prometer conversión no verificada.
- [x] Implementar profesionales, recetas médicas y registros de la óptica; búsqueda, consulta, edición e inactivación de profesionales, separadas de cuentas de acceso.
- [x] Implementar graduaciones por ojo y antecedentes con procedencia y autoría. Enteros escalados conservan precisión exacta, lateralidad y diferencia entre cero y ausente.
- [x] Implementar historial de correcciones y acceso restringido. Versiones append-only con motivo, profesional informante histórico y usuario; cliente/tipo/visita no cambian en una corrección. Lectura y escritura autorizadas en servidor y casos de uso; auditoría sin contenido clínico.
- [x] Implementar carga, consulta paginada, descarga verificada, retiro histórico y recuperación privada de JPG/JPEG/PNG/PDF. Tamaños centralizados en SQLite, configurables sólo por Administrador; almacenamiento original con SHA-256, control de espacio, estados durables y auditoría sin contenido clínico.
- [x] Completar pruebas de adjuntos y regresiones: más de diez documentos en una receta, historiales extensos, límite configurable, PDF de más de 32 MB, formatos inválidos, permisos HTTP/Aplicación, CSRF, vínculos entre clientes, fallos de disco/confirmación y optimización PNG sin pérdida. Copia con documentos históricos/retirados y restauración aislada verificadas; copia fallida no sobrescribe la anterior.
- [x] Completar actualización local conservadora y verificación de arranque sin alterar datos anteriores.
- [ ] Obtener revisión y aceptación del usuario. No avanzar a fase 5 sin autorización expresa.

Entrega funcional parcial de fase 4 — 2026-10-15:

- Alcance cotejado antes de modificar código. No se instalaron dependencias nuevas ni se avanzó a inventario.
- Formularios Razor HTTP para el módulo clínico, integrados con las fichas Blazor existentes y el estilo adaptable del sistema. POST evita poner términos de búsqueda profesionales en URLs; listas e historial acotados con refinamiento/paginación.
- Migración ClinicalRecords aditiva: crea Professionals, ClinicalRecords, ClinicalRevisions y EyeMeasurements, con referencias restrictivas a usuario/cliente/profesional y FK compuesta que impide asociar una visita de otro cliente. No se ejecuta Down ni se borra historial.
- `dotnet build OpticaSosa.sln --no-restore -m:1 -nr:false`: 0 errores y 0 advertencias. `dotnet test OpticaSosa.sln --no-build --no-restore --verbosity minimal -m:1 -nr:false`: 74 ejecutadas, 74 aprobadas, 0 fallidas, 0 omitidas. No se suman las reejecuciones como pruebas nuevas. `dotnet-ef migrations has-pending-model-changes`: sin diferencias.
- Se corrigieron errores iniciales de plantilla y la simulación del envío de decimales: las pruebas ahora envían los marcadores invariantes que genera Razor, igual que el navegador. Se detuvo sólo el servidor de esta aplicación por bloqueo de DLL. Ante un fallo de memoria de MSBuild, se cerraron sus servidores de compilación y se verificó con un solo nodo; no se atribuyó ese intento fallido a pruebas aprobadas.
- Almacenamiento interno privado probado: identificadores GUID, archivos fuera de wwwroot, escritura transitoria con publicación atómica sin sobrescritura, rechazo de enlaces y limpieza de sus propios temporales ante fallos/cancelación. Todavía no se habilitan cargas ni descargas clínicas; falta el caso de uso con metadatos y validación acordada.
- Copia previa consistente: `data/migration-backups/pre-fase4-2026-10-15-a3f655349bba4e84bd519e557762d28e`, con SQLite y adjuntos; integrity_check y foreign_key_check correctos, 11 tablas previas registradas y 0 archivos existentes. Aplicación explícita con `--initialize-database`: migración completada; comparación por hash confirmó las 11 tablas previas intactas y las cuatro nuevas vacías. No se crearon cuentas, contraseñas ni pacientes de prueba en la base local.
- Servidor reiniciado en segundo plano en localhost:5080: ingreso y CSS 200, rutas clínicas anónimas 302 al ingreso, stderr vacío. Formularios autenticados comprobados automáticamente en hosts aislados; la revisión visual manual queda para el usuario. Remoto confirmado privado y sincronizado antes de publicar el módulo; informar SHA/resultado del push al entregar.
- Para probar: con una cuenta de rol Profesional, abrir Clientes → ficha → Recetas y antecedentes visuales. Registrar un profesional desde `/clinica/profesionales`, crear una receta y consultar/corregir sus versiones. Un administrador puede crear la cuenta Profesional desde Usuarios; no obtiene acceso clínico por ser Administrador. Usar sólo datos ficticios.
- Pendientes de esa entrega parcial resueltos por la modificación de requisitos y la entrega de adjuntos que se describe a continuación. No se interpretó una aprobación de fase 5.

Entrega de adjuntos y conservación del historial:

- Se actualizaron AGENTS.md, REQUISITOS.md y README con conservación de originales, ausencia de cupos por cliente/receta/rol y tamaños configurables. El modelo es cliente 1:N registros, registro 1:N versiones, versión 1:N adjuntos; ningún índice impone un máximo de documentos. Las páginas de 25 sólo acotan cada consulta. Se retiraron topes arbitrarios de navegación de visitas y registros.
- Límites iniciales de carga: foto 25 MB, PDF 50 MB; Administrador cambia la política con concurrencia y auditoría en `/admin/archivos-clinicos`. El Profesional conserva acceso clínico exclusivo. Reducir límites no bloquea descargas anteriores, que usan streaming y verifican tamaño/hash.
- PNG: optimización explícita de IDAT sin cambiar los datos filtrados ni demás chunks; descarga de copia para revisión, sin reemplazo ni carga automática. No se recomprimen JPEG ni PDF. HEIC/HEIF evaluado y no habilitado por falta de conversor verificado en esta instalación; se informa alternativa de exportar copia conservando original.
- Metadatos Pending se guardan antes de publicar bytes; archivos GUID privados, movimiento sin overwrite, reintento idempotente y recuperación explícita tras fallos. No se borran originales ni versiones. Cargas y copias respetan reserva de espacio. Acceso y operaciones se auditan con acciones/IDs.
- Copia manual completa en directorio nuevo privado, SQLite por BackupDatabase, todos los adjuntos y manifiesto final con hashes; comprobación de integridad, relaciones y documentos. Prueba de restauración en otro host aislado y prueba de fallo preservando respaldo anterior. No se implementó programación, cifrado ni retención de fase 7.
- PdfPig 0.1.16 y dependencias incorporados con autorización mediante la solicitud de instalación; lectura estructural estricta, sin extracción de datos clínicos, ejecución, modificación ni compresión del PDF. Se reutilizó SkiaSharp ya autorizado para validar imágenes. No se instalaron codecs HEIC.
- `dotnet build OpticaSosa.sln --no-restore -m:1 -nr:false`: 0 errores, 0 advertencias. `dotnet test OpticaSosa.sln --no-build --no-restore --verbosity minimal -m:1 -nr:false`: 94 ejecutadas, 94 aprobadas, 0 fallidas, 0 omitidas (26 Infraestructura, 68 Web). Las reejecuciones no se contabilizan como pruebas nuevas. Modelo/migración sin diferencias. Se corrigió una comprobación nula del codificador en una prueba antes de validar la entrega.
- Migración ClinicalAttachments revisada y aplicada explícitamente: Up sólo crea ClinicalAttachments y ClinicalFilePolicies e índices/FK restrictivas. Copia consistente previa en `data/migration-backups/pre-adjuntos-ed7fcf36fe9e4e6da967b466a92ffbb7`; integrity_check y foreign_key_check correctos. Comparación por hash confirmó las 15 tablas anteriores intactas; 0 documentos locales, sin insertar pacientes ni documentos ficticios. La migración no borra ni modifica historial previo.
- Servidor reiniciado en segundo plano en localhost:5080. Ingreso/CSS 200; adjuntos y administración anónimos 302 al ingreso; stderr vacío. Pruebas autenticadas completas en host aislado; revisión visual manual pendiente. Remoto confirmado privado y sin divergencia antes de publicar; el commit y la verificación de SHA del push se informan en la entrega.

Criterios de aceptación: historial clínico trazable sin generación automática de diagnósticos; adjuntos inaccesibles sin permiso; ausencia de datos sensibles en logs. Aprobar antes de fase 5.

## Fase 5 — Inventario

- [ ] Validar catálogo, variantes, cristales por encargo y regla de reservas.
- [ ] Implementar productos, entradas, salidas y ajustes con motivos.
- [ ] Implementar control de existencias y concurrencia.
- [ ] Probar movimientos, compensaciones y prevención de stock negativo.

Criterios de aceptación: existencias reproducibles desde movimientos; cambios simultáneos no producen stock inválido; ajustes auditados. Aprobar antes de fase 6.

## Fase 6 — Pedidos y ventas

- [ ] Acordar estados, precios, descuentos y alcance de pagos/señas.
- [ ] Implementar pedidos y transiciones permitidas.
- [ ] Implementar ventas con detalles e importes históricos.
- [ ] Integrar stock mediante transacciones y prevención de duplicados.
- [ ] Implementar cancelaciones/anulaciones con compensación y motivo.
- [ ] Probar totales, transacciones fallidas, doble envío, concurrencia y permisos.

Criterios de aceptación: circuito de pedido a entrega/venta verificable, sin duplicación de movimientos ni pérdida de importes históricos; anulaciones trazables. Aprobar antes de fase 7.

## Fase 7 — Respaldos y entrega operativa en una PC

- [ ] Acordar pérdida máxima tolerada, tiempo de recuperación, retención, destino y custodia de claves.
- [ ] Implementar respaldos consistentes manuales y programados con manifiesto, adjuntos y configuración necesaria.
- [ ] Probar fallos de respaldo y conservación de la última copia válida.
- [ ] Restaurar en un directorio separado y verificar base, relaciones, acceso y adjuntos.
- [ ] Documentar arranque, apagado, actualización, respaldo y recuperación.
- [ ] Validar protección del disco, permisos y requisitos de privacidad antes de autorizar datos reales.
- [ ] Realizar aceptación integral en localhost sin internet con datos sintéticos.

Criterios de aceptación: recuperación completa demostrada, medidas de protección acordadas y recorrido operativo aprobado en una PC. El uso de datos reales requiere aceptación expresa. Aprobar antes de fase 8.

## Fase 8 — Acceso por Wi-Fi desde tres celulares

- [ ] Configurar dirección local estable, HTTPS confiable y firewall limitado a red privada; sin apertura de puertos hacia internet.
- [ ] Verificar interfaz y formularios en los tres celulares.
- [ ] Probar PC y tres celulares simultáneos, conflictos de edición, doble envío y recuperación de conexión.
- [ ] Verificar funcionamiento sin internet, recursos locales y mensajes ante servidor inaccesible.
- [ ] Repetir aceptación de permisos y adjuntos a través de la red.

Criterios de aceptación: cuatro sesiones operan sobre una única base; se cumple el objetivo de respuesta acordado; sin confirmaciones falsas al perder conexión; recuperación y respaldos siguen funcionando. Cerrar con aprobación del usuario.

## Arquitectura y decisiones iniciales

| ID | Decisión propuesta | Motivo / condición de revisión |
| --- | --- | --- |
| D-01 | Monolito modular por capas | Instalación local sencilla y separación de responsabilidades. |
| D-02 | ASP.NET Core + Blazor adaptable | Una interfaz para navegador de PC y celulares; modo definitivo en fase 1. |
| D-03 | SQLite + EF Core en el servidor | Base inicial local; revisar si aparecen contención sostenida, mayor concurrencia o nuevas necesidades operativas. |
| D-04 | Adjuntos privados en disco y metadatos en SQLite | Evitar acceso público y permitir respaldo conjunto; coordinar consistencia entre archivos y base. |
| D-05 | Primero localhost, después red privada | Validar operación en una PC antes de sumar certificados y conectividad móvil. |
| D-06 | Sin servicios externos obligatorios | Mantener operación sin internet. |
| D-07 | Sin automatización clínica | Conservar registros profesionales y evitar inferencias médicas. |
| D-08 | Soporte Blazor interactivo en servidor, interactividad por componente cuando se necesite | Evitar activar circuitos para una pantalla informativa; probar reconexión antes de incorporar formularios interactivos. |
| D-09 | Arranque manual en localhost:5080 durante desarrollo | No configurar servicios de Windows, certificados ni red antes de validar el arranque básico. |
| D-10 | EF Core y proveedor oficial SQLite 10.0.12 | Instalación autorizada, compatibilidad verificada mediante compilación y pruebas. Fuentes: [proveedores](https://learn.microsoft.com/en-us/ef/core/providers/), [SQLite](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/) y [paquete](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.12). |
| D-11 | Contexto inicial sin entidades ni migración vacía; fábrica por operación | Incorporar modelos al implementar cada módulo, sin inventar tablas de negocio durante fase 1. Primera migración junto con la primera entidad real; revisión y aplicación explícitas. |
| D-12 | Directorio privado configurable y validado; SQLite con claves foráneas y sin pool inicial | Separar archivos públicos y privados, facilitar cierre de archivos y recuperación. Validaciones de rutas no reemplazan permisos/cifrado ni revisión del equipo. |
| D-13 | ASP.NET Core Identity y páginas Razor HTTP para cuentas; Blazor para interfaz del sistema | Cookies y operaciones de identidad requieren ciclo HTTP. No usar SignInManager en componentes interactivos. Fuentes: [Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0) y [configuración de cookies y sello](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0). |
| D-14 | Primer administrador y recuperación sólo por consola local | Sin registro público, credenciales por defecto ni entrega de contraseñas por chat. El acceso depende del usuario autorizado de Windows. |
| D-15 | Revalidación de cookie en cada solicitud; pantallas protegidas estáticas en esta fase | Revocar acceso inmediatamente al inactivar o cambiar contraseña. Antes de introducir circuitos con datos protegidos, incorporar revalidación del circuito. |
| D-16 | Roles cerrados y un rol por cuenta en esta entrega | Administrador gestiona usuarios/auditoría; Profesional habilita políticas clínicas; AtenciónVentas no recibe permisos administrativos o clínicos. No hay módulos clínicos todavía. |
| D-17 | Documento obligatorio, normalizado y único incluso en clientes inactivos | Confirmado por el usuario en fase 3. Contactos opcionales; nacimiento y domicilio excluidos inicialmente. Nombres/teléfonos coincidentes advierten, pero permiten confirmar personas distintas con documentos diferentes. |
| D-18 | Clientes y visitas mediante componentes Blazor con renderizado estático y formularios POST protegidos | Mantener contexto por operación y evitar circuitos de autenticación persistentes. Búsquedas por POST para no incluir datos personales en URL/logs de solicitudes. Sin paquetes nuevos. |
| D-19 | Clasificación histórica en cada visita, calculada desde visitas completadas previas | No reescribir el pasado al sumar nuevas atenciones. La clasificación de ficha/listado corresponde a la próxima atención; no equivale a compras previas. Una visita en curso por cliente. |
| D-20 | Concurrencia con token Guid y restricción única de documento en SQLite | Evitar sobrescrituras y duplicados aun entre operaciones simultáneas. Alta y visita incluyen ID de envío para no repetir la operación ante reenvíos. Cambios y auditoría se guardan atómicamente. |
| D-21 | Visitas administrativas; claves estables y vínculo restrictivo cliente/visita | En fase 4 se podrán vincular recetas y antecedentes con cliente y visita sin eliminar historial ni aceptar vínculos cruzados. No anticipar tablas ni campos médicos. |
| D-22 | Una configuración por instalación y contrato separado de los clientes | SQLite conserva una fila BusinessSettings; campos opcionales, versiones de concurrencia y marca genérica por defecto. Misma cabecera para Blazor y páginas de cuentas; namespaces internos sin cambios. |
| D-23 | Logo normalizado en BLOB, sin archivo original ni ruta de usuario | Atomicidad con configuración y respaldo incluido en SQLite. PNG/JPEG máximo 2 MB/2048 px, decodificación real y salida PNG estática hasta 512 px. [SkiaSharp 4.153.1](https://www.nuget.org/packages/SkiaSharp/4.153.1) y [licencia](https://github.com/mono/SkiaSharp/blob/main/LICENSE.txt). |
| D-24 | Publicar sólo nombre y logo para presentación | Datos completos y edición requieren business.manage (Administrador). Futuras salidas de documentos podrán reutilizar el perfil con permisos definidos en su fase; no se implementan comprobantes todavía. |
| D-25 | Primer commit integra la base existente y la ampliación verificada | El proyecto no tenía Git ni historial y el remoto está vacío. No se inventan commits de fases anteriores ni se fuerza el remoto. Primer push requiere revisión del inventario; luego rige autorización de pushes automáticos satisfactorios. |
| D-26 | Originales inmutables y metadatos de adjuntos por versión, sin cupos de cantidad | SHA-256 antes de descarga, estados Pending/Available/Failed/Retired, FK restrictivas y paginación. Los límites administrables sólo afectan cargas nuevas; los documentos previos permanecen accesibles. |
| D-27 | Validación PDF con PdfPig 0.1.16, sin reescritura | Biblioteca mantenida incorporada con autorización. Apertura estructural estricta y profundidad acotada; no se ejecutan ni previsualizan PDF dentro de la app. Fuentes: [paquete](https://www.nuget.org/packages/PdfPig/0.1.16), [opciones oficiales](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig/ParsingOptions.cs). No es una transformación del documento ni evaluación médica de su contenido. |
| D-28 | Optimización PNG sin pérdida y HEIC/HEIF pendiente de conversor verificado | Recompresión IDAT sin cambiar píxeles, orientación, profundidad ni metadatos; opción explícita y copia descargable. No recomprimir PDF/JPEG. El enum HEIF no demuestra que un decoder esté disponible: [API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skencodedimageformat), [seguimiento del proyecto](https://github.com/mono/SkiaSharp/issues/2887). No habilitar conversiones sin pruebas de conservación. |

Dirección de dependencias: Aplicación depende de Dominio; Infraestructura implementa contratos de Aplicación; Web invoca Aplicación y compone Infraestructura. Las reglas no dependen de la interfaz ni de SQLite. No se requiere crear una API separada para los celulares en el alcance inicial.

## Registro de progreso

| Fecha | Tarea | Resultado / verificación | Pendiente |
| --- | --- | --- | --- |
| 2026-10-15 | Planificación inicial | Ubicación confirmada; tres documentos creados. Sin código ni instalaciones. | Revisión y aprobación de fase 1. |
| 2026-10-15 | Base de fase 1 autorizada | Solución con cuatro capas, pantalla inicial, configuración local, estilos locales, exclusiones y README. `dotnet sln add` completado. | Compilar y arrancar con SDK 10; proyectos de pruebas y persistencia pendientes. |
| 2026-10-15 | Verificación del entorno | `dotnet --info` confirmó Windows x64 y único SDK 8.0.100. `dotnet build OpticaSosa.sln --no-restore` falló con NETSDK1045. Sin paquetes instalados. Consulta de memoria por CIM denegada; disco G con aproximadamente 765 GiB libres. | Autorizar instalación de SDK .NET 10. Confirmar RAM, versiones EF/SQLite y reglas funcionales antes de sus módulos. |
| 2026-10-15 | Instalación y validación de base | SDK 10.0.402 instalado mediante winget oficial con autorización. Consulta CIM autorizada confirmó Windows 11 Pro x64 y aproximadamente 15 GiB utilizables. `dotnet build OpticaSosa.sln --no-restore`: 0 errores y 0 advertencias. | Persistencia y proyectos de pruebas sin paquetes instalados. |
| 2026-10-15 | Arranque y recursos locales | Se corrigió mapeo con MapStaticAssets y perfil Development. HTTP 200 en inicio, CSS, Blazor JS y página de error; ruta inexistente 404. HTML sin recursos externos. SDK fijado en global.json. | Validación visual y prueba sin internet desconectado pendientes; no se afirma haberlas ejecutado. Fase 2 no iniciada. |
| 2026-10-15 | Persistencia base y pruebas autorizadas | Instalados EF Core SQLite 10.0.12, Microsoft.NET.Test.Sdk 18.0.1, xUnit 2.9.3 y runner 3.1.5. Fábrica SQLite, contexto vacío y rutas privadas. `dotnet build OpticaSosa.sln --no-restore`: 0 errores/advertencias; `dotnet test OpticaSosa.sln --no-build --no-restore`: 8 aprobadas, 0 fallidas/omitidas. | Aceptación de fase 1; ningún módulo funcional implementado aún. |
| 2026-10-15 | Integración de persistencia con Web | Arranque sin migraciones ni base operativa; creada carpeta privada de adjuntos. Inicio, CSS y JS respondieron 200 tras el cambio. Sin paquetes adicionales fuera de lo autorizado. | Inspección visual y prueba con desconexión física de internet no ejecutadas. |
| 2026-10-15 | Fase 1 revisada y fase 2 autorizada | Usuario visualizó la instancia local y pidió continuar con fase 2; luego autorizó Identity EF, Design, dotnet-ef y Mvc.Testing. | Aceptación de la nueva interfaz de cuentas tras crear administrador. |
| 2026-10-15 | Usuarios, permisos y auditoría | Casos de uso autorizados, Identity, páginas HTTP con CSRF, cookies, bloqueo, inactivación, cambio de contraseña, recuperación local y auditoría. Migración InitialIdentity generada y revisada: Up crea tablas/índices; no se ejecutó Down. | Sin funciones de clientes, clínicas ni comerciales; fase 3 requiere aprobación. |
| 2026-10-15 | Verificación final fase 2 | `dotnet build OpticaSosa.sln --no-restore`: 0 errores/advertencias. `dotnet test OpticaSosa.sln --no-build --no-restore`: 23 aprobadas (9 Infraestructura, 14 Web). `dotnet-ef migrations has-pending-model-changes`: sin cambios pendientes. | Primera cuenta real de acceso a elegir por el usuario, sin pacientes. |
| 2026-10-15 | Preparación local de fase 2 | Se confirmó ausencia de optica.db, se restringió data con ACL Windows y se aplicó explícitamente InitialIdentity y roles sobre base nueva. ACL protegida con 3 reglas. Servidor reiniciado en segundo plano: localhost:5080; ingreso/CSS 200, inicio/usuarios anónimos 302 a ingreso; stderr vacío. | Servidor activo para revisión. Crear primer administrador con --create-admin en terminal local. |

En tareas futuras registrar aquí el alcance concreto, comandos/pruebas ejecutados y resultados reales. Si una verificación no pudo ejecutarse, indicar motivo y riesgo pendiente.

### Entrega de fase 3 — 2026-10-15

- Alcance cotejado antes de modificar código: fase 3 es Clientes y visitas; se explicó al usuario la inclusión de inactivación e historial. Confirmó documento obligatorio, nombre y apellido obligatorios y contactos opcionales.
- Implementados Dominio, contratos/casos de uso, persistencia EF Core y componentes Blazor SSR del módulo. Permisos nuevos para lectura/escritura administrativa en los tres roles e inactivación sólo para Administrador. Auditoría guarda acciones e IDs, sin copiar nombres, documentos, contactos ni notas.
- `dotnet build OpticaSosa.sln --no-restore`: 0 errores y 0 advertencias. `dotnet test OpticaSosa.sln --no-build --no-restore`: 44 aprobadas (15 Infraestructura, 29 Web), ninguna fallida/omitida. Modelo y migración coinciden según `dotnet-ef migrations has-pending-model-changes`.
- Prueba de actualización desde InitialIdentity preservó usuarios, hashes, roles, auditoría y adjuntos ficticios. La migración CustomersAndVisits sólo crea tablas, índices y restricciones en Up; no se ejecutó Down ni se eliminaron registros.
- Antes de aplicar: servidor anterior detenido; copia mediante API de respaldo de SQLite en `data/migration-backups/pre-fase3-2026-10-15-bfa993fad551434e9de75193a68c179f`, reabierta y verificada con integrity_check y foreign_key_check. Adjuntos incluidos (0 archivos existentes). Es una copia local de seguridad para esta actualización, no el módulo de respaldos de fase 7.
- Aplicación explícita con `--initialize-database` completada. Comparación posterior confirmó las 8 tablas previas sin cambios de registros, una cuenta conservada, integridad correcta y tablas nuevas vacías. No se cargaron clientes ficticios en la base local.
- Servidor reiniciado en segundo plano en localhost:5080. Ingreso, CSS y Blazor JS responden 200; clientes/nuevo y listado anónimos redirigen al ingreso (302); stderr vacío. Pruebas autenticadas de formularios completas en host aislado. Revisión visual manual pendiente; no se afirma haberla realizado.
- Falta aceptación del usuario y autorización explícita antes de fase 4.

### Confirmaciones de alcance

El usuario confirmó los roles administrador, atención/ventas y profesional con acceso clínico restringido, y la clasificación de recurrente basada en una visita completada anterior. Autorizó SDK, EF Core/SQLite y pruebas; aprobó fase 2 y sus paquetes adicionales y herramienta local EF. Posteriormente aprobó fase 3, solicitó cotejar su alcance y confirmó documento obligatorio. Tras publicar el avance en GitHub aprobó fase 4, pidió informar cantidades de pruebas en las próximas entregas y confirmó 3 decimales en dioptrías y 2 en distancia pupilar binocular. La fase 5 requiere nueva aprobación explícita.
