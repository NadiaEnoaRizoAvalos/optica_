# Instrucciones permanentes — Sistema de Gestión para Ópticas

## Versionado y entregas

- Al terminar una funcionalidad importante, corrección significativa o fase: revisar el diff, compilar, ejecutar pruebas pertinentes, corregir errores y actualizar PLANS.md antes de crear un commit local descriptivo.
- Agrupar avances coherentes y comprobados; no crear commits por cada línea ni presentar entregas con pruebas fallidas como terminadas.
- Evaluar al cerrar cada fase si existe un avance significativo y funcional para versionar. No generar commits vacíos, por archivo, sólo de formato, de pruebas temporales, de código incompleto ni de cambios sin verificar. Una actualización de política solicitada expresamente se agrupa con su documentación de entrega en un único commit coherente.
- Antes de cada commit comprobar también organización del código, ausencia de archivos innecesarios y continuidad de funcionalidades anteriores. Compilar la solución completa y ejecutar las pruebas correspondientes; no considerar terminado el avance si una verificación falla.
- Antes de agregar archivos y antes de publicar, revisar archivos incluidos e historial a enviar: excluir bases y auxiliares SQLite, datos médicos/personales, logos cargados, secretos, .env, respaldos, logs y artefactos locales. Los ejemplos de pruebas deben ser explícitamente ficticios.
- Si aparece un secreto o dato privado, detener el push e informar cómo corregirlo. No reescribir historial ni borrar archivos del usuario por iniciativa propia.
- En la primera vinculación, mostrar el inventario de archivos incluidos y pedir aprobación explícita antes del primer push. Un commit local no constituye autorización de publicación.
- El usuario autorizó commits y pushes posteriores automáticos una vez aprobado el primer push. Después de esa aprobación, publicar cada avance satisfactorio en el mismo remoto privado si las herramientas/permisos lo permiten, sin pedir nuevamente autorización general. Confirmar visibilidad privada y destino antes de publicar. Esto no autoriza avanzar de fase ni operaciones destructivas.
- Nunca usar force push, git reset --hard, eliminar historial ni sobrescribir trabajo remoto sin autorización expresa. Inspeccionar y preservar cualquier historial remoto preexistente.
- Comprobar el resultado de cada push: código de salida exitoso y SHA de la rama remota igual al commit local esperado. Si falla autenticación, red o aparece historial divergente, informar el bloqueo y resolver sin force push ni sobrescrituras; no afirmar que se publicó.
- Informar brevemente funcionalidades completadas, pruebas, pendientes, identificador del commit y rama utilizada; señalar si el push sigue pendiente. No crear commits adicionales únicamente para registrar su propio hash.
- Conservar los nombres internos de proyectos/namespaces salvo necesidad técnica. La interfaz debe usar el nombre configurado del negocio o "Sistema de Gestión para Ópticas", sin fijar una marca comercial.

## Alcance y forma de trabajo

- Desarrollar un sistema funcional, seguro y mantenible para una óptica; evitar funcionalidades innecesarias.
- Trabajar en una sola funcionalidad por tarea. Leer estos lineamientos y la sección pertinente de REQUISITOS.md y PLANS.md; no volver a analizar todo el proyecto sin necesidad.
- Modificar únicamente archivos relacionados con la tarea. Conservar los módulos que funcionan y los cambios del usuario.
- No programar ni instalar dependencias durante la fase de planificación inicial.
- Pedir autorización antes de instalar herramientas o dependencias y antes de ejecutar cambios destructivos, incluyendo eliminación de datos, reinicios de base, migraciones con pérdida de información o sobrescritura de respaldos.
- Completar los criterios de aceptación y solicitar aprobación explícita antes de comenzar otra fase. Una aprobación de fase no autoriza instalaciones ni operaciones destructivas.
- Al terminar cada tarea, actualizar PLANS.md con progreso, validaciones, pendientes y bloqueos. Informar brevemente qué cambió y qué falta.
- Dar explicaciones breves y concretas. No afirmar que una prueba pasó si no se ejecutó.

## Arquitectura y código

- Usar C# y .NET 10, ASP.NET Core, Blazor, EF Core y SQLite como base propuesta. Confirmar versiones compatibles y entorno instalado en fase 1, antes de adoptar paquetes.
- Mantener un monolito modular por capas: Dominio, Aplicación, Infraestructura y Web. No introducir microservicios ni servicios externos para el funcionamiento normal.
- Dominio contiene entidades, objetos de valor e invariantes, sin dependencias de interfaz, EF Core o archivos.
- Aplicación contiene casos de uso, validaciones, contratos y autorización de operaciones. Depende de Dominio.
- Infraestructura implementa persistencia, archivos, identidad y respaldos mediante contratos de Aplicación.
- Web contiene interfaz adaptable, autenticación y composición de dependencias. No consultar EF Core desde componentes ni ubicar reglas de negocio en ellos.
- Preferir clases y métodos pequeños, nombres claros y organización por funcionalidad dentro de cada capa. Dividir archivos cuando mezclen responsabilidades; evitar abstracciones sin utilidad y duplicación.
- Usar programación asincrónica para E/S, cancelación cuando corresponda, errores controlados y registros sin información sensible.
- Documentar decisiones relevantes y alternativas en la sección de decisiones de PLANS.md. Crear documentación adicional únicamente cuando la tarea lo requiera.

## Datos y reglas del negocio

- Diseñar entidades y relaciones antes de implementar cada módulo; usar claves, restricciones, índices y migraciones versionadas.
- Mantener un único archivo de base de datos en el disco local del servidor. Ningún celular debe abrir SQLite directamente ni existir una base separada por dispositivo.
- Acceder a SQLite únicamente desde el servidor; no alojar el archivo activo en una carpeta compartida o sincronizada.
- Usar contextos de datos de vida corta por operación; no compartir DbContext entre usuarios o durante toda la sesión de Blazor.
- Aplicar transacciones a operaciones que cambien ventas, pedidos y stock. Manejar concurrencia y reintentos de envío sin duplicar operaciones.
- Evitar borrados en cascada que eliminen historial clínico o comercial. Definir anulación, inactivación y retención de cada entidad.
- No inventar datos ausentes ni interpretar un valor no informado como cero. Guardar dinero con precisión explícita y conservar importes históricos de ventas.
- No generar diagnósticos, graduaciones, recetas ni recomendaciones médicas automáticamente. Registrar únicamente lo informado por profesionales, con autoría y procedencia.

## Seguridad y privacidad

- Usar únicamente datos ficticios en desarrollo, pruebas, capturas y ejemplos. No copiar bases, fotografías o documentos reales de pacientes.
- Autenticar a cada usuario y autorizar cada caso de uso en el servidor. Ocultar un botón no constituye control de acceso.
- Aplicar mínimo privilegio; no incorporar cuentas compartidas, contraseñas por defecto ni secretos en el repositorio.
- Usar mecanismos mantenidos de identidad y hash de contraseñas; no implementar criptografía propia.
- Proteger base, adjuntos y respaldos con permisos del sistema operativo y una estrategia documentada de cifrado y recuperación de claves.
- No servir adjuntos como archivos públicos. Validar tamaño, extensión y contenido, generar nombres internos y evitar rutas elegidas por el usuario o ejecución de archivos.
- No registrar recetas, documentos, identificadores personales ni credenciales en logs técnicos. Mantener auditoría de accesos y cambios sensibles con información mínima.
- No exponer el servidor a internet. Antes de habilitar celulares, definir HTTPS confiable, firewall y acceso limitado a la red privada.
- Los respaldos deben incluir datos y adjuntos de manera consistente. Verificar restauraciones; no tratar la mera copia del archivo SQLite abierto como respaldo válido.

## Verificación

- Crear pruebas relevantes de invariantes, permisos, persistencia, concurrencia y operaciones críticas; evitar pruebas que sólo repitan la implementación.
- Ejecutar las pruebas necesarias para el cambio, incluyendo regresiones relacionadas. Registrar comandos y resultados en PLANS.md.
- Antes de liberar datos reales, verificar autorización, protección de adjuntos, ausencia de información sensible en logs y restauración completa en un entorno separado.
- No agregar herramientas, dependencias ni ejecutar operaciones destructivas sin la autorización requerida arriba.
