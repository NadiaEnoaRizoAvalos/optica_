# Verificación de seguridad e integración — 5.3

Fecha: 17/10/2026. Alcance: código integrado de 5.1 y 5.2.B, respaldos/recuperación existentes y regresiones, exclusivamente con datos ficticios. No es una autorización de producción ni una prueba de seguridad del equipo real. Fase 5 permanece abierta.

## Hallazgos de código y correcciones

| Hallazgo reproducido por pruebas | Corrección | Evidencia |
| --- | --- | --- |
| El almacén operativo reutilizado permitía crear archivos si otro componente lo invocaba directamente, aunque las rutas del visor rechazaban modificaciones. | Almacén específico ViewerAttachmentStore: escritura rechazada y lecturas sin crear directorios; permanecen guardas EF/SQLite y handles de archivos. | SecurityIntegrationTests comprueba rechazo directo y hashes sin cambios. |
| Metadatos de una copia legada firmada podían indicar MIME activos/no admitidos para entrega inline. | Reutilizar validación de extensión/MIME, cerrar stream, auditar rechazo y responder 415 conservando originales. | HTML/SVG de una base ficticia modificada y firmada se rechazan; PNG/PDF originales siguen byte a byte. |
| Dos visores tenían nombres iguales de cookies CSRF/TempData, con riesgo de colisiones y fallos de formularios. | Nombres propios por instancia para autenticación, antiforgery y TempData; claves efímeras independientes. | Rechazo de cookie ajena, nombres distintos y consulta correcta en directorio con espacios/Unicode. |

La matriz específica corregida pasó **20/20 casos** (seguridad nueva y regresiones HTTP del visor). Los cuatro fallos iniciales corresponden a los tres hallazgos anteriores y fueron corregidos; no se cuentan como pruebas aprobadas ni se suman repeticiones.

## Controles revisados

| Área | Evidencia automatizada | Límite de la conclusión |
| --- | --- | --- |
| Identity, contraseñas y sesiones | AuthenticationTests, AuthorizationTests, MultiRoleTests/MultiRoleHttpTests; sellos, cuentas activas, CSRF y credenciales conservadas. | No implica aprobación del procedimiento sobre cuentas operativas. |
| Multirrol y clínica | Siete combinaciones en aplicación/HTTP/visor; Administrador solo y Atención/Ventas sin clínica; Profesional explícito. | Permisos del visor son una captura, no sincronización de revocaciones posteriores sin conexión. |
| Revocación local | Backend comprueba rol real capturado y estado local; pruebas de revocaciones opuestas concurrentes y claims administrativos obsoletos. | No revoca otras copias ni documentos ya descargados. Abrir nueva copia exige revisar nuevamente vigencia de accesos. |
| Documentos y endpoints | Autorización por cliente/registro, hashes/bytes, tipos admitidos, CSRF/verbos, protección de rutas privadas y webroot. | Legibilidad y visualización real en navegador/otra PC pendientes. |
| Auditoría | Operaciones importantes, roles y consultas; visor registra fuera del snapshot, sin contraseñas/contenido clínico. | Protección/custodia operativas de logs y retención requieren aceptación de instalación. |
| Integridad | Cliente/recetas/versiones/profesionales/adjuntos/trabajos/calidad, historial paginado, esquema incompatible rechazado, original intacto. | No se aplica migración ni restauración sobre datos operativos. |
| Respaldos y autenticidad | BackupSignatureTests, pruebas de verificación/restauración aislada, rechazo de corrupción/confianza incorrecta, manifest/hashes. | Firmas/hashes no cifran; claves de prueba no son custodias operativas. |
| Cifrado y recuperación de claves | Pruebas ficticias existentes de guardas de protección, PFX recuperable/contraseña/MAC y carga efímera. | BitLocker real, desbloqueo independiente, custodias y pérdida de equipo siguen sin aprobar/verificar operativamente. |
| Integración entre módulos | Servidor principal conserva rutas/cuentas, visor no incorpora escrituras; recetas y calidad históricas preservadas. | No se fusiona a main ni se adopta la rama en la base de uso. |
| Dependencias | Auditoría NuGet con transitivas de los 8 proyectos: **0 paquetes con vulnerabilidades conocidas informadas** en esta ejecución. | Es la información del registro consultado, no prueba de ausencia de vulnerabilidades desconocidas o del sistema operativo. |

Verificación definitiva: **compilación completa 0 errores/0 advertencias; 291 pruebas ejecutadas, 291 aprobadas, 0 fallidas, 0 omitidas** (49 Infraestructura, 242 Web). Son 10 casos nuevos de integración/seguridad sobre los 281 de 5.2.B; no sumar repeticiones. Resultados TRX e informe NuGet quedan privados bajo data/. El intento inicial de consulta NuGet falló por DNS del entorno restringido; se consultó nuevamente con acceso autorizado y ese resultado es el informado.

Comprobación nativa adicional, fuera del conteo xUnit: paquete con runtime privado sin SDK, CMS/hashes verificados con Windows PowerShell, arranque desde system32, ingreso con cuentas ficticias, versiones y precisiones, dos originales byte a byte, CSS, Administrador sin clínica, escucha sólo 127.0.0.1 y endpoint heredado ignorado. Se corrigió carga de módulos bajo un PSModulePath heredado de PowerShell 7: importa explícitamente módulos integrados de Windows PowerShell en ese proceso, sin instalar ni cambiar perfiles o políticas globales. La red de esta PC no fue desconectada y no es un ensayo físico de segunda PC. El paquete final identifica el commit de la rama validada; archivos/ZIP anteriores se conservan.

## Pendientes manuales y operativos

| Subfase | Verificación pendiente |
| --- | --- |
| 5.1 | Revisión visual, casillas/avisos/edición desde navegador y aceptación del usuario; adopción operativa con copia/inventario/transición/reversión aprobados. |
| 5.2.A | Confirmación de prueba física en segunda PC, custodias definitivas, cifrado real autorizado y desbloqueo/recuperación independiente, servicio Windows/identidad/permisos, activación y fallos/reinicios de respaldos reales. Estado: PENDIENTE DE VERIFICACIÓN OPERATIVA. |
| 5.2.B | Ensayo físico de su paquete separado en Windows 11 x64 sin red/SDK, UI/zoom/PDF/fotografías, carpeta privada autorizada y procedimiento de vigencia de permisos capturados. El kit 5.2.A anterior permanece congelado. |
| 5.3 | Aceptación de evidencias del equipo real, cifrado/custodias, recuperación completa, ACL/servicio y pruebas manuales anteriores antes de producción. |

Ninguna prueba automatizada se atribuye a una prueba física del usuario. No se manipularon datos clínicos reales, aplicaron migraciones operativas, instalaron servicios o activaron respaldos/cifrado. Los únicos permisos de prueba corresponden a directorios temporales ficticios; no se modifican ACL globales ni destinos reales. El visor nunca cambia permisos Windows: exige un padre privado existente. El código evaluado pasó la matriz de pruebas; la revisión automática no sustituye pentest, revisión humana o controles de la instalación real.

## Continuidad recomendada

Se puede preparar/desarrollar fase 6 con datos ficticios tras su autorización explícita, manteniendo estos pendientes abiertos. No requiere declarar fase 5 completa ni usar datos reales. Antes de comenzar, resolver P-06 para el catálogo práctico; no reconstruir clínica o calidad, ni anticipar ventas/ERP. El uso operativo sigue bloqueado por las verificaciones manuales/operativas indicadas.
