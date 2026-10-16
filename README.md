# Sistema de Gestión para Ópticas

Aplicación web local para Windows. Clientes, visitas, recetas/historia clínica, adjuntos, profesionales y trabajos/controles de calidad implementados hasta fase 4.1. Fase 5.2.A en curso: migraciones de respaldos aplicadas y configuración habilitada; programación y escritura externa bloqueadas. Multirrol, visor de emergencia y módulos comerciales futuros no implementados. Durante desarrollo se usan sólo datos ficticios.

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

En desarrollo el arranque crea `data/attachments/` en la raíz, fuera de `wwwroot` y excluido del control de versiones. La base es `data/optica.db`. La instalación actual tiene aplicadas las migraciones hasta BackupDestinationBinding, conservando cuentas y datos previos. No se cargaron datos de clientes de ejemplo ni contraseñas predeterminadas. El arranque web no ejecuta migraciones.

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

La actualización local contó con copia consistente de SQLite y adjuntos, reabierta y verificada, en `data/migration-backups/pre-fase3-2026-10-15-bfa993fad551434e9de75193a68c179f`. Se compararon las ocho tablas previas antes/después, sin cambios de registros. Es una copia local para esa migración; el alcance vigente de respaldos operativos está en la subtarea 5.2 de PLANS.md. No se aplicaron reversiones ni borrados.

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

Licencias comerciales del producto, pruebas gratuitas y panel de ventas permanecen pospuestos. Consultar aceptación y estado actual de fases/subtareas en PLANS.md.

## Recetas y antecedentes — módulo inicial de fase 4

1. Ingresar con rol **Profesional**. Si sólo existe Administrador, crear una cuenta individual de rol Profesional desde Usuarios, eligiendo su contraseña localmente. Administrador y AtenciónVentas no reciben acceso clínico por esos roles.
2. Abrir **Profesionales informantes** desde el inicio (`http://localhost:5080/clinica/profesionales`). Registrar un nombre ficticio y, si figuran, matrícula y ámbito. Esto no crea una cuenta de acceso. Es posible consultar, buscar, editar e inactivar sin borrar la ficha.
3. Abrir Clientes, elegir un cliente ficticio y entrar en **Recetas y antecedentes visuales**. Crear una receta médica, un registro de la óptica o un antecedente informado. Elegir profesional, fecha y procedencia; la visita es opcional y debe pertenecer al mismo cliente.
4. En recetas, transcribir OD/OI por separado: esfera, cilindro, adición y prisma hasta 3 decimales; eje entero de 0 a 180° y base textual. Distancia pupilar binocular en mm con 2 decimales. Todos estos valores son opcionales: vacío y cero son distintos. Para antecedentes, escribir lo informado por el profesional y dejar vacías las graduaciones.
5. Consultar el registro y usar **Crear corrección desde la última versión**, con motivo obligatorio. Se conservan las versiones anteriores, usuario, fecha de registro y datos del profesional que constaban en cada versión. Ante un conflicto, recargar antes de corregir; no se sobrescribe silenciosamente.

La aplicación no genera diagnósticos ni recetas automáticamente. Las consultas y cambios se auditan sin copiar el contenido clínico. El módulo conserva las capas y SQLite, con referencias restrictivas y sin borrar historial. La suite actual tiene 94 pruebas: 26 Infraestructura y 68 Web; los informes separan errores/advertencias de compilación y pruebas ejecutadas/aprobadas/fallidas/omitidas.

## Documentos clínicos y conservación del historial

El historial no tiene cupos fijos de recetas, consultas, controles, antecedentes o adjuntos. Las listas se consultan de a 25 elementos para evitar cargar todo el historial; esto no reduce los registros almacenados. Cada nueva receta se crea desde el historial como un nuevo documento. Las correcciones crean versiones y conservan sus graduaciones, informante y documentos anteriores.

Con rol **Profesional**, abrir una receta o antecedente y seleccionar **Consultar o adjuntar documentos de esta versión**. Elegir JPG/JPEG, PNG o PDF y pulsar **Guardar original**. Se carga de a un archivo y se pueden repetir cargas tantas veces como sea necesario, sin límite fijo por receta o cliente. Los valores iniciales son 25 MB para fotos y 50 MB para PDF. Las descargas requieren permiso clínico y verifican los bytes originales mediante SHA-256; se entregan como adjuntos privados sin caché.

Si el archivo es demasiado grande, la pantalla indica el límite. Para PNG ofrece una copia optimizada sin pérdida, descargable para revisar y seleccionar; no la adjunta ni reemplaza automáticamente. Conserva píxeles filtrados, dimensiones y metadatos; no reduce resolución. El archivo seleccionado en la PC/celular no se modifica. Si se quiere archivar ese original por encima del límite, solicitar un aumento administrativo. JPEG y PDF no se recomprimen automáticamente. Si la optimización no reduce el tamaño o no es viable, elegir otra copia o ajustar el límite.

HEIC/HEIF se evaluó y no está habilitado: la existencia del identificador HEIF en una biblioteca no demuestra que el decodificador/conversor esté disponible y verificado en este Windows. Hasta validar un conversor que preserve el original y su orientación/color, exportar una copia JPG/PNG desde el celular sin borrar el HEIC original. No se instalaron codecs de conversión adicionales.

Una carga interrumpida aparece como pendiente/fallida. **Verificar y recuperar carga interrumpida** publica el documento únicamente si el archivo completo coincide con su hash; de lo contrario debe volver a adjuntarse. Marcar como retirado exige motivo y conserva el original para consulta histórica. Ni retiradas ni correcciones borran archivos.

Con rol **Administrador**, abrir [Almacenamiento clínico](http://localhost:5080/admin/archivos-clinicos) para consultar espacio libre/reserva y modificar tamaños. Los cambios requieren sesión, CSRF y versión vigente; se auditan. Disminuir un límite no impide descargar documentos anteriores. Este permiso no habilita al Administrador a leer documentos clínicos por navegador.

Configuración técnica opcional en el servidor, sección `ClinicalFiles` de appsettings o variables de entorno equivalentes (requiere reiniciar el servidor al cambiarla):

```json
"ClinicalFiles": {
  "RequestMaxMiB": 128,
  "MaximumImagePixels": 40000000,
  "ReserveDiskMiB": 512
}
```

El techo de solicitud incluye el formulario: los límites administrables de cada archivo deben ser menores que ese techo. MB de la interfaz significa 1024 × 1024 bytes. Los defaults y validaciones técnicas están centralizados en ClinicalFileOptions; tamaños de fotos/PDF en ClinicalFilePolicy. No hay cupos de cantidad. Las descargas usan streaming y no dependen del límite de cargas actual ni del anterior límite interno de 32 MB.

**Crear copia local verificada**, en la misma página administrativa, genera una carpeta nueva `data/clinical-backups/<GUID>` con SQLite consistente y todos los adjuntos. Incluye históricos y retirados; verifica integridad SQLite, claves foráneas y hashes de originales. Sólo una carpeta con `complete.json` y verificaciones satisfactorias es una copia completa. Una copia fallida no sobrescribe la anterior. Esto no reemplaza la protección ante fallas del disco: ver desarrollo y habilitación pendiente de 5.2 debajo. La restauración se probó en un servidor de pruebas separado, sin sobrescribir datos activos.

## Probar la fase 4.1 desde el navegador

Abrir [el sistema local](http://localhost:5080). Usar sólo datos ficticios durante la revisión. Si faltan cuentas, un Administrador puede crearlas en Configuración y administración → Usuarios; no hay contraseñas predeterminadas. Administrador configura el negocio y requisitos de calidad, pero no consulta documentos clínicos ni aprueba controles operativos.

1. Como **Profesional**, abrir Clientes, buscar o registrar una persona y consultar su ficha. El alta no exige receta. La ficha enlaza visitas, documentos, profesionales, pedidos y entregas según permisos.
2. En Profesionales y médicos, registrar un emisor/informante; su ficha es independiente de una cuenta de usuario. Desde la ficha del cliente o Recetas y antecedentes, registrar un documento, motivo declarado, profesional y graduaciones informadas. Seleccionar varios archivos para revisar sus vistas previas; quitar una selección no modifica el archivo original. Al guardar se vuelve al historial y se informa cuántos adjuntos se guardaron. Para un fallo parcial, conservar los originales y completar desde Adjuntos del documento.
3. En la ficha del cliente → Pedidos → Registrar pedido, indicar trabajo y armazón. Profesional puede vincular una receta; se fija su versión. No copiar graduaciones ni motivos clínicos en especificaciones operativas visibles para Ventas.
4. En el pedido, Comenzar preparación → Enviar a control. Completar el **Control técnico**; la aprobación está deshabilitada mientras falten obligatorios, existan observados o no se justifiquen los No aplica admitidos. Guardar un observado exige Iniciar corrección y una nueva ronda antes de aprobar. El backend vuelve a validar todo.
5. Como **Atención/Ventas** o Profesional, completar y aprobar **Control previo a entrega**. Ventas no ve ni modifica el control técnico. Sólo ambas aprobaciones habilitan Registrar entrega. Consultar historial muestra responsables, roles, fechas, resultados y correcciones permitidos.
6. Cambiar especificaciones o receta de un pedido no entregado exige nuevos controles. Rectificar un control aprobado requiere motivo y agrega un registro; no elimina el anterior. Administrador modifica obligatoriedad por tipo/etapa en Configuración y administración → Requisitos de calidad; un cambio efectivo reabre controles de los pedidos no entregados afectados.

No hay precios, cobros ni movimientos de stock en este módulo mínimo: se incorporarán al aprobar sus fases. El Administrador conserva sus permisos originales, sin acceso clínico implícito. Diseño revisado en navegador de PC y tamaño móvil; la habilitación real por Wi-Fi corresponde ahora a fase 12.

## Subtarea 5.2 — respaldos, revisión y habilitación pendiente

Abrir [Respaldos](http://localhost:5080/admin/respaldos) como Administrador, desde Configuración y administración. El resumen distingue última copia exitosa y verificada, último intento incluso fallido, próxima ejecución, disco configurado y conexión consultada en sólo lectura; debajo se conservan alertas e historial. Conectado no significa autorizado para escribir ni cifrado. Cambiar hora con programación activa recalcula la próxima ejecución; guardar cambios con programación desactivada la mantiene así. Cada instalación conserva su configuración independiente.

La base operativa ya tiene AutomaticBackups y BackupDestinationBinding, aplicadas con autorización y copia final verificada. La pantalla permite inspeccionar discos y guardar la configuración; todavía no se eligieron destino ni horario, y no existe próxima ejecución. El servidor mantiene Backups:ExternalWritesAuthorized=false y Backups:WorkerEnabled=false. Vincular/enviar copias al externo o activar la programación siguen rechazados por el servidor mientras falte autorización, aunque sus botones estén visibles. La copia manual local anterior continúa disponible y no se agrega automáticamente al historial nuevo. En instalaciones con esquema anterior se muestran controles deshabilitados y aviso; el arranque nunca migra automáticamente. No ejecutar inicialización como reparación de rutina.

Acceso: ingresar como Administrador → menú lateral **Respaldos y recuperación**, o **Configuración y administración → Respaldos y recuperación**. Almacenamiento clínico incluye el mismo enlace y conserva sus límites y botón de copia local. La dirección de la pantalla nueva es /admin/respaldos; la antigua es /admin/archivos-clinicos.

Para revisar la pantalla con una base ficticia aislada, ejecutar estos comandos en PowerShell. El directorio debe ser exclusivo y nuevo; no apuntar a data/optica.db ni copiar pacientes reales. La cuenta de esta instancia se elige interactivamente en la terminal, usando un nombre ficticio; no hay contraseña predeterminada.

```powershell
$opticaProject = 'G:\WINDOWS\Desktop\optica\src\OpticaSosa.Web\OpticaSosa.Web.csproj'
$opticaPreview = 'G:\WINDOWS\Desktop\optica\data\preview-backups'
if (Test-Path -LiteralPath (Join-Path $opticaPreview 'optica.db')) { throw 'Ya existe una base de preview. No reinicializar ni reemplazarla.' }
dotnet run --project $opticaProject --no-launch-profile --no-build -- --environment Development --Storage:DataDirectory $opticaPreview --initialize-database
dotnet run --project $opticaProject --no-launch-profile --no-build -- --environment Development --Storage:DataDirectory $opticaPreview --create-admin
dotnet run --project $opticaProject --no-launch-profile --no-build -- --environment Development --Storage:DataDirectory $opticaPreview --Backups:ExternalWritesAuthorized false --urls http://localhost:5081
```

Ingresar en [la instancia aislada](http://localhost:5081/cuenta/ingresar), luego /admin/respaldos. Inspeccionar discos es una operación de sólo lectura que muestra identidad, sistema de archivos, disco físico, capacidad y espacio. Cada óptica elige su propio volumen y una carpeta relativa dentro de él, por ejemplo Optica/Respaldos, sin D: ni otra letra en ese campo. Guardar destino, horario, frecuencia cada N días, zona y retención no escribe en el disco. No existe una hora obligatoria inicial. La interfaz conserva volumen/carpeta y permite desactivar el cronograma aunque esté desconectado. La copia manual local es independiente de la programación.

Escritura externa está bloqueada por Backups:ExternalWritesAuthorized=false (valor predeterminado), independiente del checkbox de programación. No cambiarlo en una instalación real sin revisar identidad, carpeta elegida, permisos, espacio y autorización. La carpeta base debe existir con ACL privadas; el programa no cambia permisos ni prepara arbitrariamente otra carpeta. Con autorización explícita y cronograma desactivado, “Vincular carpeta guardada” crea sólo un marcador privado de esta instalación; no copia datos clínicos ni activa el cronograma. No se sobrescriben marcadores inválidos/ajenos. Antes de escribir se comprueban volumen, NTFS, separación física, ACL y marcador esperado; si falta alguno, se rechaza sin usar otro destino. Un GUID genérico por sí solo no autoriza escribir. Las copias se guardan en una subcarpeta de la instalación; las ejecuciones antiguas conservan su carpeta/vinculación aunque se cambie el destino actual.

Tras autorizar migraciones y uso del destino reales, el Administrador guarda disco/carpeta, vincula y después configura/activa hora, zona, frecuencia y conservación. Cambiar disco o carpeta exige nueva vinculación; no se permite activarla automáticamente. “Encolar copia manual” funciona con cronograma desactivado y continúa al cerrar la pestaña; el servidor debe seguir activo. La pantalla distingue estado, última copia exitosa, próxima ejecución/reintento, inicio/fin, errores y períodos omitidos, en la zona elegida. Una copia recuperada tras corte conserva la fecha del manifiesto, no se presenta como creada al reiniciar. Se realiza una copia actual para recuperar vencimientos, nunca copias históricas inventadas. No se elimina la última copia válida para liberar espacio.

En esta PC se autorizó exclusivamente preparar D:\Optica\Respaldos. Quedó vacía, con herencia bloqueada: cuenta ejecutora con Modificar, SYSTEM/Administradores con Control total y sin acceso asignado a otros usuarios. Se comprobó escritura/lectura/hash y eliminación sólo del archivo ficticio propio; la cuenta del proceso web coincide con la probada. Se preservaron el ACL y otras entradas de D:\Optica. No se copió la base al disco externo ni se vinculó ese disco en la base operativa. Esta preparación no es una configuración fija del producto ni autoriza programación, migraciones, cifrado o registro de servicio. Si cambia la identidad Windows del servicio, revisar/autorizar sus ACL antes de usarlo.

“Verificar restauración aislada” verifica y copia a data/restore-tests/<GUID> de esa instancia, sin sobrescribir la base activa, arrancar otra aplicación ni habilitar tareas restauradas. Las pruebas automatizadas usan exclusivamente destinos y datos ficticios; el ensayo real de pérdida del equipo/claves sigue pendiente. Los directorios de ensayo se conservan privados para revisión y requieren limpieza controlada si se repiten.

WindowsServices 10.0.12 está agregado con autorización, pero no se registró ningún servicio ni se cambiaron cuentas Windows. El despliegue como servicio requiere autorización, rutas absolutas, identidad/ACL y recuperación de claves revisadas; AddWindowsService no registra el servicio por sí mismo. Las pruebas del hospedado no equivalen a validar un servicio registrado en esta PC. El programador funciona dentro del mismo servidor, sin sesión de navegador, mientras PC/proceso estén activos.

BitLocker/BitLocker To Go sólo están propuestos: antes de activarlos se requieren dos custodias separadas de recuperación, validación de claves y prueba en equipo independiente. No guardar recuperación con el mismo respaldo ni publicar claves. Administrar copias no concede lectura clínica en la aplicación; proteger además acceso físico y administrativo Windows. La subtarea 5.2 no habilita uso con datos clínicos reales; 5.1 y 5.3 continúan pendientes.

### Migraciones de respaldos — aplicación autorizada y configuración habilitada

La instalación se actualizó desde QualityWorkflow aplicando exclusivamente estas dos migraciones, revisadas en C# y SQL y ensayadas con recuperación ficticia:

| Migración | Cambios hacia adelante |
| --- | --- |
| 20261016055719_AutomaticBackups | Crea BackupSettings (instalación, habilitación, frecuencia/hora/zona, volumen, conservación, versión y próxima ejecución), BackupRuns (identidad, tiempos, disparador, estado/error, directorio/volumen, archivos, períodos perdidos y actor) e índice StartedUtc. |
| 20261016161235_BackupDestinationBinding | Añade BindingId y RelativeFolder a ambas tablas. El valor heredado de carpeta es OpticaSosaBackups; no prepara esa carpeta ni selecciona un disco. Si ya hubiera configuración, establece Enabled=0 y NextUtc=NULL. |

Son aditivas para el esquema y no modifican tablas de clientes, clínica, profesionales, documentos, pedidos/calidad o Identity. También registran las versiones en __EFMigrationsHistory. La segunda contiene una actualización de seguridad exclusivamente sobre BackupSettings: no es una migración sin ningún UPDATE. Los métodos Down sí eliminan tablas/columnas y no forman parte del procedimiento aprobado ni de los ensayos de recuperación.

Se corrigió el terminador de la sentencia UPDATE para que el script generado sea ejecutable junto con la siguiente sentencia; no cambia su efecto. Las pruebas aplican el migrador y el script completo en bases ficticias, simulan una detención entre migraciones y restauran el punto anterior en otro directorio privado, sin Down, sobrescritura o cambios al respaldo original. Se comparan todas las tablas previas y hashes de SQLite, con PNG/PDF relacionados.

Aplicación realizada el 16/10/2026: servidor detenido, copia final completa y verificada mediante el motor existente, configuración fuente y recuperación incluidas, y cada migración aplicada/verificada por separado. Copia: data/migration-backups/pre-fase52-20261016/b4f5443d22994a32a19b7741ae96e718. Las 23 tablas previas, sus datos y las ocho auditorías posteriores a la copia antigua permanecieron intactas; también todas las copias anteriores. Configuración nueva sin valores elegidos y programación desactivada. En futuras instalaciones/actualizaciones repetir autorización, pausa, copia final y comparación de datos previos, integridad/FKs, cuentas y originales. Si falla, detener la habilitación y preservar los archivos; recuperar/reemplazar la base operativa requiere autorización específica. No ejecutar Down por rutina.

La copia local existente es un punto de recuperación verificable, no protección ante rotura del disco del servidor ni evidencia de recuperación de cifrado/claves en otra PC. Su vigencia debe revisarse justo antes de migrar; las operaciones posteriores, incluida auditoría, pueden no estar incluidas.

Autorizaciones separadas: **migraciones/configuración** (ya autorizadas y habilitadas en esta instalación), **escritura externa y vinculación del marcador**, **copias al disco real**, **activación del programador**, **cifrado y recuperación de claves**, y **registro/identidad del servicio Windows**. Aprobar una no aprueba las demás. Guardar disco/carpeta y horario no concede escritura externa ni activa programación.

Con el esquema actualizado, en /admin/respaldos: inspeccionar discos (sólo lectura), seleccionar el volumen verificado y escribir Optica/Respaldos como carpeta relativa para el destino previamente preparado. Elegir hora local, zona, frecuencia y conservación; guardar con Activar programación desmarcado. La letra actual no es la identidad persistida. No vincular ni encolar copias hasta autorizar la escritura externa; no activar el cronograma hasta completar y aprobar su procedimiento. Si se cambia la cuenta Windows del futuro servicio, verificar sus permisos antes de escribir.
