# Pruebas

`OpticaSosa.Infrastructure.Tests` incluye quince casos de las fases anteriores: rutas privadas, SQLite real, ACL de Windows e invariantes de clientes/visitas. Verifica persistencia entre contextos, transacciones, claves foráneas, campos obligatorios, documento válido y cierre coherente de visitas. La limpieza valida el destino y sólo elimina la carpeta GUID creada por la prueba.

`OpticaSosa.Web.Tests` incluye veintinueve casos de las fases anteriores con WebApplicationFactory, migraciones reales y base exclusiva por prueba. Mantiene acceso, clientes/visitas, unicidad de documento incluso en inactivos, coincidencias, búsquedas normalizadas, edición obsoleta, reenvíos, clasificación histórica, paginación, relaciones restrictivas, auditoría sin datos personales, formularios SSR/CSRF y permisos en Aplicación/HTTP. Una prueba migra una base ficticia desde fase 2 y verifica cuentas, hashes, roles, auditoría y adjuntos preservados. Los fixtures comprueban la ruta aislada antes de migrar.

Ejecutar desde la raíz con `dotnet test OpticaSosa.sln`. Después de compilar y restaurar, usar `dotnet test OpticaSosa.sln --no-build --no-restore` para repetir sin descargas.

La clasificación se prueba con un reloj controlado, sin esperas reales. No existen aún reglas clínicas, de stock ni de ventas para probar. No utilizar bases o adjuntos reales. Las verificaciones de ACL requieren Windows.

Configuración del negocio agrega cinco casos en Infraestructura (logos) y cinco en Web (persistencia tras reinicio del host, datos privados, permisos, CSRF, conflictos y marca genérica). Totales actuales: 20 Infraestructura + 34 Web = 54. Los logos de pruebas se dibujan en memoria con SkiaSharp; no se versionan archivos de clientes ni fotografías. La prueba de reinicio conserva únicamente su carpeta temporal GUID entre dos hosts y la elimina al finalizar el segundo, tras validar la ruta.
