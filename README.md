# Sistema de Gestión para Ópticas

Aplicación web local para Windows. Fase 3 implementada y pendiente de revisión: clientes y visitas administrativas, además de acceso, usuarios y auditoría. Recetas, antecedentes visuales, inventario y ventas todavía no están implementados. Durante desarrollo se usan sólo datos ficticios.

La tarea adicional previa a fase 4 incorpora configuración independiente del negocio y versionado con Git. La marca se configura desde la aplicación; los nombres internos OpticaSosa de proyectos y namespaces se conservan por compatibilidad. No hay multitenencia: cada instalación utiliza su propia base local.

## Estructura

- `src/OpticaSosa.Domain`: clientes, visitas e invariantes; sin dependencias externas.
- `src/OpticaSosa.Application`: contratos de casos de uso, formularios y permisos; referencia a Dominio.
- `src/OpticaSosa.Infrastructure`: EF Core/SQLite, almacenamiento Identity, auditoría y aprovisionamiento local; referencia a Aplicación.
- `src/OpticaSosa.Web`: ASP.NET Core y Blazor; páginas Razor para operaciones HTTP de identidad, composición de dependencias e interfaz.
- `tests/OpticaSosa.Infrastructure.Tests`: persistencia, rutas y ACL de Windows.
- `tests/OpticaSosa.Web.Tests`: autenticación y autorización mediante HTTP y base temporal aislada.

Los módulos se incorporan por fase, manteniendo la persistencia fuera de componentes y reglas del negocio en Dominio y casos de uso autorizados.

## Compilación y ejecución previstas

Requiere SDK de .NET 10. Se instaló con autorización el SDK 10.0.402 y se fijó en `global.json`, admitiendo actualizaciones de parche. Paquetes autorizados: EF Core SQLite, Identity.EntityFrameworkCore, EF Core Design y Mvc.Testing 10.0.12; xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.0.1 y xunit.runner.visualstudio 3.1.5. La herramienta dotnet-ef 10.0.12 es local, fijada en `.config/dotnet-tools.json`; no se instaló globalmente.

La validación de logos incorpora SkiaSharp 4.153.1 y sus dependencias nativas, instaladas con autorización. Referencias del paquete y licencia: [SkiaSharp](https://www.nuget.org/packages/SkiaSharp/4.153.1), [licencia del proyecto](https://github.com/mono/SkiaSharp/blob/main/LICENSE.txt). El procesamiento se realiza en el servidor, sin servicios externos.

```powershell
dotnet build OpticaSosa.sln
dotnet run --project src/OpticaSosa.Web
```

Abrir `http://localhost:5080`. Detener con `Ctrl+C`. El perfil `OpticaSosa.Local` configura el entorno Development para ejecutar desde fuentes. Sólo se configura acceso local; el inicio automático y el acceso móvil quedan pendientes. La pantalla inicial utiliza recursos locales. Para una entrega de producción se debe publicar la aplicación y validar esa salida; no ejecutar los binarios de desarrollo como si estuvieran publicados.

Blazor tiene habilitado el soporte interactivo en servidor, pero la pantalla inicial es estática porque aún no necesita interactividad. La reconexión se implementará y verificará cuando exista una operación interactiva.

## Almacenamiento y migraciones

En desarrollo el arranque crea `data/attachments/` en la raíz, fuera de `wwwroot` y excluido del control de versiones. La base es `data/optica.db`. Están aplicadas InitialIdentity y CustomersAndVisits; se conserva la cuenta creada por el usuario y los roles existentes. No se cargaron datos de clientes de ejemplo ni contraseñas predeterminadas. No existen tablas clínicas o comerciales. El arranque web no ejecuta migraciones.

La fábrica de contextos crea instancias independientes por operación y activa claves foráneas. No compartir un contexto durante un circuito de Blazor. Se deshabilitó el pool de conexiones inicialmente para simplificar cierre de archivos y pruebas. SQLite utiliza un tiempo de espera de cinco segundos ante bloqueos; las reglas de conflictos y reintentos se implementarán con cada módulo.

En producción se exige configurar `Storage:DataDirectory` (o la variable `Storage__DataDirectory`) con ruta local absoluta. Se rechazan rutas relativas, superposición con la carpeta pública, rutas UNC, unidades de red y ancestros enlazados. La ruta debe quedar fuera de los binarios; permisos de Windows, cifrado y carpetas sincronizadas requieren revisión operativa y no quedan resueltos sólo por esta validación.

Las migraciones versionadas viven en `src/OpticaSosa.Infrastructure/Persistence/Migrations`; la inicial se llama `InitialIdentity`. No se usa EnsureCreated. Antes de nuevas migraciones: revisar SQL y pérdidas potenciales, respaldar y verificar restauración si hay datos, probar en una copia y aplicar explícitamente. Cualquier cambio con pérdida de datos requiere autorización específica. Nunca ejecutar una reversión ni eliminar una base para resolver un error de migración sin autorización.

## Primer administrador y recuperación

La base de esta PC está inicializada y ya tiene una cuenta. Ingresar con ella; no volver a crear el administrador. En una instalación sin cuentas, para elegir la primera cuenta y contraseña se ejecuta en una terminal PowerShell local:

```powershell
dotnet run --project "G:\WINDOWS\Desktop\optica\src\OpticaSosa.Web\OpticaSosa.Web.csproj" --launch-profile OpticaSosa.Local --no-build --no-restore -- --create-admin
```

Ingresar el nombre y repetir la contraseña en la terminal. No se muestra la contraseña ni se acepta mediante argumentos o entrada redirigida. Requiere 12 caracteres como mínimo, mayúscula, minúscula, número y símbolo. No compartirla en el chat. La creación inicial se rechaza si ya existe cualquier cuenta. El servidor puede permanecer funcionando durante este comando.

Después abrir `http://localhost:5080/cuenta/ingresar`. Desde Usuarios, el administrador puede crear cuentas con rol e inactivarlas. No puede inactivar su propia cuenta ni dejar el sistema sin administrador activo. No hay registro público. Cada usuario puede cambiar su contraseña; debe volver a ingresar después de hacerlo.

Para recuperar un administrador activo con acceso autorizado al usuario de Windows del servidor, usar el mismo comando reemplazando `--create-admin` por `--recover-admin`. Solicita una nueva contraseña oculta, levanta el bloqueo y revoca sesiones anteriores, sin borrar historia ni exponer tokens.

En una instalación nueva, antes de crear el primer administrador, se ejecuta `--initialize-database` con el mismo prefijo. Este comando aplica migraciones pendientes y crea roles; no es parte del arranque habitual. En una base con información, exige la revisión y los respaldos indicados arriba. No ejecutarlo por rutina como forma de reparar errores.

## Permisos y seguridad de fase 2

| Operación | Administrador | Atención/ventas | Profesional |
| --- | --- | --- | --- |
| Ingresar, salir y cambiar contraseña propia | Sí | Sí | Sí |
| Crear/inactivar cuentas y consultar auditoría | Sí | No | No |
| Políticas de lectura/escritura clínica | No | No | Sí |

Los permisos clínicos están definidos y probados como políticas; todavía no existen módulos clínicos. El rol administrador no recibe acceso clínico automáticamente. En esta entrega se asigna un rol por cuenta al crearla; no hay edición libre de permisos ni borrado de usuarios.

La autorización se verifica tanto en endpoints como en casos de uso administrativos. Las páginas de cuenta usan HTTP tradicional con antifalsificación; no se invoca SignInManager desde componentes interactivos. Antes de agregar componentes interactivos con información protegida será necesario incorporar revalidación de autenticación en el circuito.

Identity administra el hash y la validación de contraseñas. Cinco intentos fallidos bloquean la cuenta durante 15 minutos. La cookie es HttpOnly, SameSite Strict, de sesión con ticket de 30 minutos y renovación por actividad. Se revalida el sello de seguridad y estado activo en cada solicitud: inactivación, cambio o recuperación de contraseña revocan sesiones anteriores. HTTP se limita al desarrollo en localhost; antes de acceso por red se exigirá HTTPS y cookies Secure.

La inicialización restringió `data/` al usuario de Windows que la ejecutó, SYSTEM y administradores de Windows. Los nuevos archivos heredan esas reglas. Esto no cifra SQLite ni sustituye el cifrado de disco y respaldos requerido antes de usar información real. Ejecutar el servidor con el mismo usuario de Windows que inicializó la carpeta.

La auditoría conserva fecha UTC, acción e identificadores internos de actor/destino; registra ingresos, fallos, salidas, rechazos por permisos, cambios de cuentas y contraseñas, y consulta de usuarios/auditoría. No incluye contraseñas, tokens, nombres introducidos en intentos fallidos ni contenido médico. La pantalla muestra los últimos 100 eventos. Los logs técnicos no registran parámetros sensibles y se suprimen comandos SQL rutinarios.

## Verificación actual

La solución compila con `dotnet build OpticaSosa.sln --no-restore`: cero errores y advertencias. Se comprobó por HTTP el inicio en localhost, `/app.css`, `/_framework/blazor.web.js` y `/error` (200), y una ruta inexistente (404). El HTML inicial no referencia recursos externos. Se corrigió el mapeo de recursos con `MapStaticAssets` y se agregó el perfil local de desarrollo.

`dotnet test OpticaSosa.sln --no-build --no-restore`: 44 pruebas aprobadas (15 Infraestructura, 29 Web). Incluyen las verificaciones previas de acceso, persistencia y ACL, más clientes, documento obligatorio/único, coincidencias, reenvíos, edición con versión, búsqueda, visitas y clasificación, historial paginado, permisos, CSRF, formularios SSR y migración sin pérdida de cuentas, auditoría ni adjuntos. Las pruebas usan archivos temporales exclusivos con datos ficticios y limpian sólo su carpeta GUID validada. No abren ni borran la base de uso local.

La ampliación de configuración agrega 10 casos: decodificación/reencodificación de imágenes válidas, límites y contenido inválido, permisos HTTP/Aplicación, CSRF, conflictos, retiro de logo y persistencia en una instancia nueva tras detener la anterior. Total actualizado: 54 pruebas (20 Infraestructura, 34 Web). Los archivos de logo se generan en memoria con colores sintéticos.

`dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/OpticaSosa.Infrastructure --startup-project src/OpticaSosa.Web --no-build` confirmó que el modelo coincide con la migración. La última comprobación de Kestrel real devolvió 200 en ingreso y CSS, y redirigió al ingreso las solicitudes anónimas al inicio y a administración.

No se realizó inspección visual con navegador ni prueba con internet físicamente desconectado. Las reglas de clientes y visitas se verificaron automáticamente; módulos clínicos y comerciales aún no existen. En el entorno restringido de Codex se necesitó autorización de ejecución para compilar, guardar claves de ASP.NET en el perfil de Windows y consultar localhost. ASP.NET informó protección de sus claves con Windows DPAPI.

## Probar clientes y visitas — fase 3

1. Con el servidor en ejecución, abrir `http://localhost:5080/clientes` e ingresar con tu cuenta. También está el enlace Clientes desde el inicio.
2. Elegir Registrar cliente. Nombre, apellido y documento son obligatorios; teléfono y correo son opcionales. Usar, por ejemplo, nombre Cliente ficticio, apellido Prueba y documento FIC-PRUEBA-001. Fecha de nacimiento y domicilio no se solicitan.
3. Guardar, consultar la ficha, editar un contacto y buscar por nombre, apellido, documento o contacto. La búsqueda no depende de tildes o mayúsculas y se envía por POST para no exponer el texto en las URL/logs. Si hay más de 100 coincidencias, refinar la consulta.
4. Intentar registrar el mismo documento con puntos, guiones o espacios diferentes: se rechaza, incluso si la ficha está inactiva. Coincidencias de nombre o teléfono muestran fichas para revisar; sólo se permite confirmar personas distintas con documentos diferentes. No hay fusión automática.
5. Desde la ficha, iniciar una visita con motivo administrativo ficticio y completarla. La primera conserva clasificación Nuevo; una siguiente visita iniciada después de completarla será Recurrente. Cancelar una visita no cuenta como atención completada. La ficha/listado muestran la clasificación para la próxima atención; el historial conserva la clasificación al inicio de cada visita.
6. Como administrador, probar Inactivar cliente y confirmar. Hay que finalizar/cancelar primero cualquier visita en curso. La ficha y el historial se conservan y se encuentran marcando Incluir clientes inactivos; no admiten nuevas visitas. Las visitas se muestran de 25 en 25, con enlaces al historial anterior y fechas argentinas.

Administrador, AtenciónVentas y Profesional pueden consultar, registrar/editar clientes y gestionar visitas administrativas. Sólo Administrador puede inactivar clientes. Todos los cambios se autorizan en servidor; la ficha incluye versiones para evitar sobrescribir cambios de otra ventana. Los IDs de cliente y visita son estables; la clave compuesta cliente/visita prepara futuras referencias clínicas sin permitir cruces de cliente. No se implementaron recetas ni historial visual en esta fase.

Las operaciones importantes y las consultas se auditan mediante acciones e IDs, sin copiar datos de la ficha ni notas. No registrar contenido médico en motivos/observaciones administrativas.

La actualización local contó con copia consistente de SQLite y adjuntos, reabierta y verificada, en `data/migration-backups/pre-fase3-2026-10-15-bfa993fad551434e9de75193a68c179f`. Se compararon las ocho tablas previas antes/después, sin cambios de registros. Es una copia local para esta migración; respaldos operativos y restauración integral siguen siendo fase 7. No se aplicaron reversiones ni borrados.

## Configuración del negocio — tarea previa a fase 4

1. Ingresar con Administrador y abrir `http://localhost:5080/admin/negocio`, también disponible desde el inicio y la navegación administrativa.
2. Completar los datos deseados: nombre comercial, dirección, provincia, localidad, teléfono, WhatsApp, correo, CUIT y datos adicionales. Todos son opcionales. El CUIT se valida sólo por formato de 11 dígitos; no se consulta una autoridad fiscal.
3. Elegir un PNG/JPEG estático de hasta 2 MB y 2048 × 2048 píxeles y guardar. El formulario muestra el nombre/logo guardados y la cabecera se actualiza en páginas de cuentas y Blazor. La copia normalizada queda limitada a 512 píxeles por lado y no conserva el original ni sus metadatos.
4. Cerrar y volver a abrir el sistema: la configuración permanece en SQLite. Para volver al identificador genérico, vaciar el nombre y marcar Quitar el logo actual. No elegir un archivo nuevo y quitarlo al mismo tiempo.
5. Probar con AtenciónVentas o Profesional: ven la marca de la instalación, pero no pueden abrir ni enviar cambios al formulario administrativo. Dirección, contactos y CUIT no se publican en la pantalla de ingreso.

La tabla BusinessSettings es independiente de clientes/visitas, admite una única fila y guarda el logo como BLOB junto con una versión. Cambios y auditoría son atómicos; versiones de concurrencia evitan sobrescrituras. La URL de logo devuelve únicamente PNG validado, nunca archivos elegidos por ruta. El contrato administrativo podrá reutilizarse en comprobantes futuros, que aún no se implementaron.

La copia privada previa a esta migración está en `data/migration-backups/pre-negocio-2026-10-15-54128ddd3dce4dc781c428fd527ccb62`. Sigue siendo una copia local para actualización, no el módulo de respaldos operativos.

## GitHub y entregas

Remoto origin: `https://github.com/NadiaEnoaRizoAvalos/optica_.git`, comprobado privado y con permiso de escritura. El remoto estaba vacío; el primer commit local incorpora el proyecto existente junto con esta ampliación. No se recrea historial anterior ni se fuerza la publicación.

El usuario aprobó expresamente la primera publicación del inventario en ARCHIVOS_PARA_GITHUB.md. Se conserva el commit inicial b640541 y se agrupa la consolidación de política solicitada en una actualización documental coherente, sin inventar historial anterior. Rigen las reglas de commits/pushes automáticos de AGENTS.md para avances satisfactorios en el mismo remoto privado; esto no autoriza avanzar a otra fase. Cada publicación debe verificarse comparando el SHA local y remoto y se informa la rama utilizada.

.gitignore excluye datos de cada instalación, bases/auxiliares, imágenes/documentos de usuarios, secretos, .env, respaldos, logs y binarios. Código, pruebas, migraciones y documentación técnica sí se versionan. Las credenciales GitHub permanecen en el gestor de credenciales de Windows; no se incorporan a configuración fuente ni documentación. Nunca compartir contraseñas o tokens en el chat.

Licencias comerciales del producto, pruebas gratuitas y panel de ventas permanecen pospuestos. Fase 4 fue autorizada y está en desarrollo; fase 5 requiere aprobación posterior.

## Recetas y antecedentes — módulo inicial de fase 4

1. Ingresar con rol **Profesional**. Si sólo existe Administrador, crear una cuenta individual de rol Profesional desde Usuarios, eligiendo su contraseña localmente. Administrador y AtenciónVentas no reciben acceso clínico por esos roles.
2. Abrir **Profesionales informantes** desde el inicio (`http://localhost:5080/clinica/profesionales`). Registrar un nombre ficticio y, si figuran, matrícula y ámbito. Esto no crea una cuenta de acceso. Es posible consultar, buscar, editar e inactivar sin borrar la ficha.
3. Abrir Clientes, elegir un cliente ficticio y entrar en **Recetas y antecedentes visuales**. Crear una receta médica, un registro de la óptica o un antecedente informado. Elegir profesional, fecha y procedencia; la visita es opcional y debe pertenecer al mismo cliente.
4. En recetas, transcribir OD/OI por separado: esfera, cilindro, adición y prisma hasta 3 decimales; eje entero de 0 a 180° y base textual. Distancia pupilar binocular en mm con 2 decimales. Todos estos valores son opcionales: vacío y cero son distintos. Para antecedentes, escribir lo informado por el profesional y dejar vacías las graduaciones.
5. Consultar el registro y usar **Crear corrección desde la última versión**, con motivo obligatorio. Se conservan las versiones anteriores, usuario, fecha de registro y datos del profesional que constaban en cada versión. Ante un conflicto, recargar antes de corregir; no se sobrescribe silenciosamente.

La aplicación no genera diagnósticos ni recetas automáticamente. Las consultas y cambios se auditan sin copiar el contenido clínico. El módulo conserva las capas y SQLite, con referencias restrictivas y sin borrar historial. La suite actual tiene 74 pruebas: 26 Infraestructura y 48 Web; los informes de próximas entregas separarán errores/advertencias de compilación y pruebas ejecutadas/aprobadas/fallidas/omitidas.

**Adjuntos todavía pendientes:** su almacenamiento interno ya tiene escritura atómica privada probada, pero no se habilitaron formularios ni descargas. Se envió una consulta para acordar PNG/JPEG/PDF, hasta 10 MB y 10 por registro, y mantener acceso sólo Profesional. Esta entrega no cierra la fase 4 ni inicia inventario.
