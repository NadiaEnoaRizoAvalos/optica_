# Simulacro de recuperación 5.2.A — Windows 11 x64

Sólo contiene datos inventados. No es una versión operativa, un instalador o el visor de emergencia. La prueba física permanece pendiente hasta que el usuario la ejecute y confirme. No usar datos clínicos reales ni considerar aprobados cifrado, custodias o servicio por completar este ensayo.

## Qué contiene

- `runtime/`: host y runtimes privados Microsoft.NETCore.App y Microsoft.AspNetCore.App 10.0.13, licencia y avisos. Sin SDK; no instala .NET ni usa internet para recuperar.
- `tool/`: herramienta de recuperación y bibliotecas de la compilación identificada en `VERSION.json`.
- `backup/`: SQLite y originales PNG/PDF ficticios, configuración permitida, manifiesto y firma CMS. Dos clientes, un profesional, recetas médica y óptica, antecedente/control, graduaciones OD/OI, corrección histórica, tres cuentas ficticias y trabajo con ambos controles de calidad. Los conteos exactos de todas las tablas están en `resultado-esperado.json`.
- `kit-manifest.json` y `.p7s`: hashes de todos los archivos y firma de un certificado exclusivamente ficticio. No hay claves privadas. La huella recibida en el chat es la referencia independiente; no confiar inicialmente en un archivo obtenido del propio disco.
- `INICIAR-PRUEBA.cmd` y `.ps1`: procedimiento guiado; `firmante-publico.txt` sólo repite una huella pública. `VERSION.json` identifica fecha, runtime y commit base; el código del avance se identifica también en GitHub.

## Requisitos de la segunda PC

Windows 11 x64, PowerShell Windows de 64 bits y disco local NTFS con al menos 1 GiB libre para los resultados. Usar una cuenta normal y una carpeta nueva propia en un volumen distinto del carry disk. No hacen falta Visual Studio, SDK, .NET instalado ni internet. No ejecutar como Administrador. Si una política de la organización bloquea scripts o falta un componente de Windows, detenerse e informar el error; no cambiar políticas globales ni instalar nada para ocultarlo. La comprobación física decide compatibilidad real de esta PC.

## Antes de ejecutar cualquier archivo del kit

1. Conectar el carry disk. La copia al disco requiere autorización previa para una carpeta de pruebas nueva; nunca reemplazar respaldos existentes. La letra puede cambiar.
2. Localizar el ZIP y abrir **PowerShell** desde Inicio. Ejecutar `Get-FileHash -LiteralPath 'E:\RUTA\NOMBRE.zip' -Algorithm SHA256`, reemplazando ruta y letra. Comparar los 64 caracteres con el hash entregado por Codex en el chat, no con un valor leído únicamente del carry disk. Si no coincide, no extraer/ejecutar.
3. Extraer con el Explorador a una carpeta nueva dentro de la carpeta de pruebas del carry disk. No mezclar con archivos personales. No modificar/agregar archivos al kit firmado.
4. Tener disponible la huella pública SHA-256 entregada en el chat. No es una contraseña ni clave secreta. El kit verifica su firma y todos sus hashes antes de ejecutar el runtime y vuelve a comprobarlos al finalizar.

## Ejecución guiada

1. Desconectar Wi-Fi y cable Ethernet manualmente. No se modifican firewall, servicios ni red mediante scripts. El resultado registra la confirmación del usuario; la comprobación de recuperación offline requiere esta acción física.
2. Abrir `INICIAR-PRUEBA.cmd`. Usa RemoteSigned sólo para ese proceso PowerShell, sin cambiar la política permanente ni instalar certificados. Una política corporativa prevalece: si lo impide, detenerse y avisar. Primero debe haberse comparado el hash del ZIP.
3. Pegar la huella pública recibida y confirmar la desconexión escribiendo `SI`.
4. Elegir una ruta **nueva**, por ejemplo `C:\Users\TU_USUARIO\AppData\Local\OpticaSimulacro-01`. Su carpeta padre debe existir. No elegir una instalación actual, una carpeta personal existente ni el disco del kit. No sobrescribe ni elimina directorios existentes.
5. El procedimiento crea/protege sólo esa carpeta nueva para la cuenta ejecutora, SYSTEM y Administradores. Verifica que el runtime privado no incluya SDK; valida firma, hashes, SQLite, relaciones y originales; restaura en un subdirectorio privado nuevo y vuelve a verificar. No inicia una aplicación/servidor ni ejecuta migraciones.
6. Compara todas las tablas y el hash íntegro de SQLite con la copia ficticia verificada al generar el kit. Esto conserva cada columna, graduación, versión, relación y hash/rol de cuenta. La comprobación de contraseñas de las tres cuentas con PasswordHasher se ensayó sobre la restauración aislada al preparar el kit. No se prueba una sesión web en la segunda PC ni se cambia ninguna cuenta; eso no se debe informar como realizado.
7. Crea una **segunda copia ficticia** dentro de la carpeta nueva, altera sólo esa copia y comprueba que el verificador la rechaza. No altera el respaldo original. Verifica otra vez firma/hashes del kit original.
8. Si todo pasa, muestra `RECUPERACION FICTICIA CORRECTA` y la ruta de `RESULTADO.json`. Los logs, inventario y copia corrupta quedan sólo en la carpeta nueva, sin borrar nada automáticamente. Los documentos restaurados conservan nombres internos `.bin`; sus hashes/MIME originales se verifican sin abrir programas ni modificar asociaciones de archivos.

## Qué informar

Confirmar que se ejecutó en la segunda PC física Windows 11 x64 con red desconectada. Informar el mensaje final y los campos de `RESULTADO.json`: NoSdkInPortableRuntime, TablesMatch, OriginalUnmodified, CorruptionRejected, PrivateNewWorkspace y OfflineDeclaredByUser. No enviar contraseñas, claves ni bases. El JSON no incluye identificadores del equipo, contenido clínico, secretos o nombres de pacientes; si hubiera un error, compartir sólo su mensaje seguro.

Una ejecución local automatizada puede registrar OfflineDeclaredByUser=false y no es prueba física. PhysicalTestRequiresUserConfirmation permanece true: Codex sólo registrará el ensayo físico cuando el usuario lo confirme. Ante cualquier fallo, conservar los archivos para revisar y no reintentar sobre bases existentes.

## Pendientes posteriores

Este respaldo ficticio puede permanecer sin cifrado conforme a la autorización del ensayo. No se activan BitLocker, servicios, claves operativas o programación. Repetir recuperación con el mecanismo de cifrado aprobado, dos custodias físicas independientes, desbloqueo recuperable y reinicio del servicio autorizados antes de cerrar seguridad de 5.2.A. No iniciar 5.1 ni 5.2.B.
