# Prueba de usuarios con múltiples roles — 5.1

Desarrollo separado de la instalación operativa y del kit de recuperación 5.2.A. No reutilizar bases, contraseñas o personas reales. No comienza 5.2.B y no cierra la seguridad de fase 5.

## Demostración preparada en esta computadora

- Abrir **http://localhost:5081/admin/usuarios**, preferentemente en una ventana privada. El servidor operativo de 5080 permanece pausado; no iniciar la rama de desarrollo contra su base.
- Usuario exclusivamente ficticio: `admin.multirrol.ficticio`. Contraseña exclusivamente ficticia: `Ficticia-Para-Pruebas-938!`. No son credenciales operativas ni valores predeterminados del producto; fueron creadas explícitamente sólo en esta demostración.
- Directorio aislado: `G:\WINDOWS\Desktop\optica\data\preview-51-ficticia-20261017`. Contiene una base nueva; no se copió la base operativa. Programación, escritura externa, creación de carpetas externas y firmas operativas desactivadas. Sin servicio Windows.
- El nombre del negocio y los datos pueden permanecer vacíos. No cargar información real. Logs y resultados quedan en `data/`, excluidos de Git.

Si se cerró esta demostración, el siguiente comando PowerShell inicia **esa misma base ficticia ya preparada**, desde cualquier directorio. No inicializa ni migra automáticamente. No sustituir el directorio por el operativo:

```powershell
& 'G:\WINDOWS\Desktop\optica\src\OpticaSosa.Web\bin\Debug\net10.0\OpticaSosa.Web.exe' --urls http://localhost:5081 --environment Development --contentRoot 'G:\WINDOWS\Desktop\optica\src\OpticaSosa.Web' --Storage:DataDirectory 'G:\WINDOWS\Desktop\optica\data\preview-51-ficticia-20261017' --Backups:WorkerEnabled false --Backups:ExternalWritesAuthorized false --Backups:FolderCreationAuthorized false --Backups:SigningEnabled false --Backups:RequireSignature false
```

## Pasos desde el navegador

1. Ir a **Administración → Usuarios**. En Crear cuenta, escribir un nombre y contraseña de prueba propios; marcar **Atención/Ventas** y **Profesional**. Crear. La cuenta debe aparecer en una sola fila, con ambos roles.
2. Abrir otra sesión de navegador (otro perfil/navegador para no compartir cookies). Ingresar con la cuenta creada. Comprobar **Registros clínicos** y **Pedidos** (`/clinica/registros` y `/pedidos`). No debería poder entrar a `/admin/usuarios`.
3. Volver a la sesión administradora. Abrir **Editar roles de…** en la fila de esa cuenta. Quitar Profesional, conservar Atención/Ventas, leer la advertencia, marcar la confirmación y pulsar **Guardar roles**.
4. En la sesión de la cuenta modificada, abrir de nuevo una página protegida: debe pedir ingreso. Tras volver a ingresar con la misma contraseña, Pedidos sigue disponible; `/clinica/registros` y las descargas clínicas se rechazan.
5. El Administrador ficticio sin Profesional debe ser rechazado al abrir `/clinica/registros`. Ser Administrador no concede acceso clínico. Para comprobar una combinación administrativa y clínica, crear otra cuenta ficticia con ambos roles explícitos.
6. Intentar guardar sin roles o quitar Administrador de la única cuenta administradora: debe mostrar error y conservar sus asignaciones. Tampoco se permite quitarse el propio acceso administrativo aunque exista otra administradora.
7. Revisar **Administración → Auditoría** (`/admin/auditoria`): `user.roles.changed`, `user.role.added.<rol>` y `user.role.removed.<rol>` identifican actor y cuenta objetivo, sin contraseñas ni información clínica. La creación también registra los roles asignados.
8. Abrir la pantalla en dos sesiones administradoras antes de editar una misma cuenta. Después del primer cambio, el segundo envío debe pedir recargar; no sobrescribe los permisos nuevos. Se cierran las sesiones sólo si hubo cambios efectivos.

## Transición y reversión operativa pendientes

No requiere migración estructural: Identity ya dispone de `AspNetUserRoles` muchos a muchos. Al arrancar no se agregan roles a cuentas existentes ni se cambian IDs/contraseñas. Sólo una acción administrativa explícita modifica asignaciones.

Antes de adoptar esta rama operativamente: aprobar copia consistente/recuperable, inventario privado de cuentas y permisos, ventana de cambio, versión desplegable y plan de reversión. Una vuelta a la interfaz antigua de un solo rol no es segura si ya existen cuentas combinadas: no borrar membresías ni restaurar destructivamente para resolverlo. Ante un fallo, detener cambios y conservar estado para una recuperación autorizada. El kit 5.2.A anterior permanece congelado y conserva su versión propia.

Las 261 pruebas automatizadas de la entrega incluyen autenticación, clínica sin compra, documentos, recetas/versiones y ambas etapas de calidad. Las comprobaciones HTTP adicionales de la demostración verifican ingreso, casillas y denegación clínica del Administrador. La prueba física en la segunda PC sigue siendo un procedimiento distinto y requiere confirmación del usuario. Fase 10 reutilizará los controles de fase 4.1.
