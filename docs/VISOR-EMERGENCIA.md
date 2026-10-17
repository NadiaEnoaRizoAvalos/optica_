# Visor de emergencia — 5.2.B

Auxiliar de consulta para Windows 11 x64, sin internet, SDK o instalación de servicios. Sólo habilitado para ensayos con datos ficticios. No reemplaza al sistema principal ni acredita seguridad de producción. Es independiente del kit congelado del simulacro 5.2.A: no modificar ni mezclar sus archivos.

## Inicio sencillo del paquete portátil

1. Antes de extraer/ejecutar, comparar el SHA-256 del ZIP con el recibido por un canal independiente: `Get-FileHash -LiteralPath 'RUTA_DEL_ZIP' -Algorithm SHA256`. No confiar inicialmente en un hash obtenido sólo del mismo disco. Si difiere, detenerse.
2. Extraer en carpeta nueva. Abrir `INICIAR-VISOR.cmd` como usuario normal. Pegar la huella pública SHA-256 del firmante recibida fuera del kit; no se solicita ninguna clave privada o contraseña por consola. El script verifica CMS y todos los archivos antes de ejecutar el runtime privado.
3. Confirmar uso exclusivo de datos ficticios. El paquete incluye un respaldo ficticio firmado; no elegir bases operativas o de pacientes. Seleccionar una carpeta local NTFS **privada ya preparada/autorizada** para crear una subcarpeta nueva. Si Windows indica que los permisos no son privados, detenerse y solicitar preparación autorizada; el lanzador/visor no cambian ACL.
4. Esperar la verificación y abrir **http://127.0.0.1:5092**. No necesita navegador abierto para permanecer activo; sí el proceso de consola del visor. Ctrl+C lo detiene. No instala servicios, migra, cifra, activa respaldos o cambia cuentas.
5. Ingresar con la cuenta individual capturada en la copia. Para el paquete de prueba solamente: `admin.ficticio`, `profesional.ficticio`, `ventas.ficticio` y `mixta1.ficticia` a `mixta7.ficticia`; contraseña exclusivamente ficticia `Ficticia-Para-Pruebas-938!`. Estas cuentas sólo provienen del fixture de pruebas, no son predeterminadas ni se aprovisionan por el producto.

Para seleccionar otro respaldo ficticio, responder NO al uso del incluido y elegir su carpeta. Se solicita por separado la huella pública del firmante de ese respaldo: la confianza en el paquete de software no autoriza automáticamente los respaldos de otra instalación. Sólo el ejemplo incluido usa el mismo firmante ficticio para ambos. No se extrae confianza del certificado contenido en la copia.

Si una política corporativa bloquea scripts, no cambiarla globalmente ni ejecutar como Administrador para eludirla. El lanzador usa RemoteSigned sólo para su proceso. Windows 11 x64, runtime privado 10.0.13 y PowerShell Windows de 64 bits son los requisitos de esta distribución; hace falta espacio para una copia completa más 512 MiB de reserva. Compatibilidad física/offline real en otra PC pendiente del usuario.

## Consultas desde el navegador

- **Clientes:** buscar por nombre/documento/contacto y abrir ficha, visitas e historial. No hay altas o edición.
- **Profesional:** ficha → receta/antecedente → versión histórica. Conserva fechas, matrícula, síntomas, notas, OD/OI, esfera/cilindro/eje/adición/prisma/base y distancia pupilar; vacío se distingue de cero. Historial, versiones y documentos tienen paginación.
- **Documentos:** desde una versión, abrir PNG/JPG/PDF original en nueva pestaña. Hash y tamaño se verifican antes de entregarlo. Probar también una versión anterior para ver sus documentos. El permiso Profesional se exige en el endpoint, no sólo en el enlace.
- **Trabajos y calidad:** Profesional o Atención/Ventas pueden consultar los trabajos actuales; controles técnicos y receta asociada sólo para Profesional. Los controles conservan sus responsables históricos. Pedidos comerciales se incorporarán cuando exista ese módulo; no se inventan ventas.
- **Administrador:** puede consultar clientes y administrar revocaciones **locales** en Accesos locales; sin Profesional no puede leer historia ni documentos clínicos. Los siete conjuntos no vacíos de roles mantienen la misma unión explícita de permisos de 5.1.

## Permisos capturados y revocaciones

El encabezado muestra la fecha de la copia y advierte que permisos/contraseña corresponden a esa fecha. El visor offline no sabe si una cuenta fue revocada después en la PC perdida. Antes de un uso real deben validarse vigencia, custodias y permisos mediante un procedimiento aprobado; no sincroniza ni concede privilegios por custodiar un disco.

Un Administrador puede confirmar la revocación de otra cuenta en **Accesos locales**. Se audita y deniega sesiones en la siguiente solicitud y nuevos ingresos. No cambia roles/contraseñas ni elimina la cuenta del respaldo. No puede revocarse a sí mismo. No revierte documentos ya descargados ni solicita borrar archivos personales. Una carpeta de trabajo existente nunca se reutiliza automáticamente: nuevo arranque/copia requiere una revisión nueva de accesos; la política local no equivale a una revocación global en otras copias.

Cinco contraseñas fallidas bloquean el ingreso de esa cuenta durante 15 minutos en el visor actual; no modifican los contadores de la base. El acceso tiene además límite de solicitudes. Cookies y claves Data Protection son efímeras y propias del visor: reiniciar invalida sus sesiones sin depender de claves de la PC original.

## Integridad y almacenamiento

Se autentica el manifiesto con una huella confiable fuera del respaldo; se copia a carpeta nueva como material todavía no confiable; luego se reutiliza BackupVerifier para verificar hashes, SQLite, relaciones y documentos. No se abre SQLite original. Se exige formato 3 firmado y esquema exactamente compatible; nunca se migra para ocultar diferencias.

La copia verificada usa SQLite ReadOnly/immutable y handles Windows que niegan escritura/reemplazo. Un interceptor permite exclusivamente redirigir las auditorías de las consultas existentes a `viewer-audit.jsonl`, fuera de `snapshot/`; rechaza otros cambios de EF. SQLite también rechaza SQL de escritura. Las revocaciones se guardan fuera del snapshot en `revocations.jsonl`. No hay rutas web para descargar bases, logs, claves o explorar carpetas. La consulta se limita a loopback, con CSRF, no-store, límites de carga y cabeceras de protección.

El almacén del visor rechaza también escrituras internas y no crea carpetas al leer. La entrega inline exige tipos/extensiones admitidos; un formato legado distinto responde 415 y conserva el documento original. Autenticación, CSRF y TempData tienen cookies independientes por instancia. Informe de revisión: docs/SEGURIDAD-FASE-5.md en el proyecto.

Una falla conserva material parcial privado para revisión, sin borrar automáticamente. No equivale a un respaldo recuperable ni debe usarse si la verificación no termina. No hay respaldo alternativo silencioso, edición o restauración sobre instalaciones existentes.

## Desarrollo y verificación

`scripts/New-OpticaViewerKit.ps1` genera exclusivamente un directorio nuevo `data/visor-emergencia-52b-*`: publica el visor, copia los runtimes/licencias ya instalados, crea respaldo ficticio con el fixture aislado, firma CMS y comprime. No descarga paquetes/runtime ni usa bases operativas. Requiere la compilación de desarrollo y las dependencias ya autorizadas; la segunda PC no necesita SDK. El ZIP y las huellas se entregan fuera de Git; `VERSION.json` identifica commit/runtime/estado del árbol. No modificar un paquete firmado: producir otra versión.

Pruebas automatizadas e informe de seguridad se registran en PLANS/BITACORA. Las revisiones visuales y el ensayo físico se registran pendientes hasta que el usuario los confirme. Cifrado real, custodias, vigencia de permisos, ACL operativas, recuperación completa y servicio Windows siguen requiriendo autorización/verificación. No usar información clínica real mientras falten.
