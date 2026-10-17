# Instrucciones permanentes — Óptica Sosa

## Alcance y forma de trabajo

- Mantener dos áreas con igual importancia: gestión clínica e historial visual, y gestión comercial y operativa. El Profesional puede atender y registrar información sin compras.
- Sistema interno para empleados, con base independiente por instalación. Clientes, profesionales externos, proveedores y laboratorios son registros, no cuentas automáticas.
- Trabajar en una funcionalidad por tarea, modificar sólo archivos relacionados y conservar módulos que funcionan. Priorizar simplicidad, seguridad y mantenimiento.
- Pedir autorización antes de instalar dependencias, ejecutar acciones destructivas o reemplazar datos. Aprobar una planificación no autoriza programar otra fase.
- Solicitar aprobación antes de comenzar cada fase, respetando las continuidades explícitas ya autorizadas y sus condiciones de cierre. Informar brevemente cambios, validaciones, problemas y pendientes; actualizar PLANS.md.
- Continuidad de fase 5: 5.2.A → 5.1 → 5.2.B → 5.3. Excepción explícita del 17/10/2026: desarrollar/probar 5.1 en rama separada mientras 5.2.A permanece PENDIENTE DE VERIFICACIÓN OPERATIVA; kit del simulacro congelado. No iniciar 5.2.B ni fase 6. Migraciones operativas nuevas, instalaciones, permisos Windows, servicios, cifrado, archivos del disco externo y activación real requieren sus autorizaciones específicas.
- Cada módulo debe tener pantallas funcionales y un procedimiento práctico de prueba desde el navegador. El rediseño definitivo corresponde a la fase 12.

## Arquitectura y código

- Conservar C#/.NET 10, ASP.NET Core/Blazor, EF Core y SQLite local. No cambiar motor, namespaces ni arquitectura sin justificar y documentar la decisión. La interfaz usa el nombre configurado o “Sistema de Gestión para Ópticas”, sin marca fija.
- Separar Domain (reglas), Application (casos de uso y contratos), Infrastructure (persistencia, identidad y archivos) y Web (interfaz y composición). No acceder a EF ni decidir reglas de negocio desde pantallas.
- Evitar duplicación, archivos gigantes, servicios universales y abstracciones sin necesidad. Reutilizar los módulos clínicos, trabajos y controles existentes.
- Usar operaciones asíncronas, cancelación, contextos breves, validación del servidor y funcionamiento sin servicios/CDN de internet.
- Diseñar claves, relaciones, índices, restricciones y concurrencia explícitamente. Revisar migraciones, compatibilidad y recuperación antes de aplicarlas. SQLite vive en el disco del servidor, nunca en una carpeta compartida para acceso directo desde celulares.

## Integridad clínica y comercial

- Conservar recetas médicas y ópticas, ambos ojos, todas las graduaciones, síntomas, antecedentes, consultas, controles, profesionales/matrículas y documentos. Nunca generar diagnósticos automáticamente.
- Cada receta es un documento independiente. Las correcciones crean versiones con motivo y autor; no sobrescribir ni borrar historia. Evitar eliminaciones en cascada. Paginar sin imponer máximos arbitrarios de registros o adjuntos.
- Distinguir vacío de cero. Respetar precisiones aprobadas, importes exactos y versiones históricas de datos relevantes.
- Separar producto/variante, graduación por ojo y datos de montaje. No esconder atributos críticos en texto libre o JSON genérico. Crear únicamente variantes y presentaciones utilizadas.
- Admitir stock, encargos a laboratorio y pedidos mixtos. Reservar sólo existencias recibidas y utilizables; controlar faltantes, lotes y vencimientos desde su primer uso. No sustituir productos automáticamente.
- Usar transacciones, concurrencia e idempotencia en pagos, reservas, movimientos, recepción y entrega. Una reserva no es salida física; no descontar dos veces ni reponer material consumido al cancelar.
- Diferenciar receta, trabajo, pedido comercial, venta y encargo. No inventar ventas históricas ni incorporar compras agrupadas o estructuras de ERP sin necesidad.
- Preservar los controles de calidad y su historia. Actualmente se exigen las dos etapas: cualquier adaptación requiere matriz aprobada, revisión de reglas de dominio y restricciones SQLite, migración segura y pruebas. “No aplica” nunca evita un control esencial.

## Seguridad y recuperación

- Autorizar en el servidor con mínimo privilegio. Usar Identity, cuentas individuales, protección de sesión y CSRF; nunca contraseñas predeterminadas o criptografía propia.
- Gestión multirrol con Identity: sólo Administrador asigna/revoca roles explícitos; mínimo un rol por cuenta activa, último administrador activo protegido y sin quitarse el propio acceso administrativo. Conservar cuentas, credenciales y permisos anteriores hasta un cambio explícito. Actualizar sello de seguridad, concurrencia y auditoría atómicamente; la siguiente solicitud exige una nueva sesión tras cambios. La transición operativa requiere procedimiento autorizado.
- Administrador no implica acceso clínico. Profesional no implica acceso financiero. Atención/Ventas no accede a historia clínica completa ni modifica graduaciones.
- Una futura proyección técnica comercial será explícita, mínima, de sólo lectura y ligada a una versión autorizada por el Profesional. Resolver sus campos conforme a PLANS.md antes de implementarla.
- Usar exclusivamente datos ficticios en desarrollo y pruebas. No usar datos clínicos reales hasta verificar seguridad, cifrado, autorizaciones y recuperación.
- Guardar adjuntos en almacenamiento privado: validar contenido, tipo y límites configurables; nombres internos, hashes, permisos, espacio libre y recuperación de operaciones interrumpidas.
- Conservar originales. No reducir legibilidad ni comprimir PDF silenciosamente; optimización de imágenes sólo segura y explícita. HEIC/HEIF permanece sin habilitar hasta validar una conversión segura.
- Auditar actor, acción, fecha y referencia del objeto sin copiar datos clínicos, contraseñas o secretos a logs.
- No exponer el servidor a internet. Preparar HTTPS, firewall y acceso Wi-Fi privado antes de habilitar celulares.
- Respaldar SQLite consistentemente junto con todos los documentos, configuración necesaria y estrategia de recuperación de claves. Automatizar, verificar integridad, conservar copias anteriores ante fallos y probar restauración aislada.
- Distinguir integridad de autenticidad: firmas CMS con confianza aprobada fuera del respaldo; no confiar automáticamente en certificados incluidos ni exportar claves privadas. Cambiar políticas/claves operativas requiere autorización.
- Una copia en el mismo disco no protege ante pérdida del equipo. Evaluar almacenamiento externo y cifrado con recuperación de claves; no activarlos ni sobrescribir una instalación operativa sin las autorizaciones y verificaciones correspondientes.

## Pruebas, documentación y GitHub

- Probar reglas importantes, permisos, integridad, concurrencia y regresiones; incluir atención clínica sin compra y recuperación. Usar datos ficticios.
- Antes de cada commit: revisar cambios y archivos innecesarios, compilar la solución completa, ejecutar las pruebas correspondientes y corregir fallos.
- Informar por separado errores/advertencias de compilación y pruebas ejecutadas, aprobadas, fallidas y omitidas. No presentar resultados históricos como nuevas ejecuciones.
- REQUISITOS.md define comportamiento; PLANS.md alcance, progreso y pendientes; DECISIONES.md decisiones y estados; BITACORA.md conserva entregas y verificaciones. Evitar repetir el historial en los documentos vigentes.
- Crear un commit coherente por avance significativo, funcional y verificado, incluyendo reorganizaciones documentales. No hacer commits vacíos, temporales, por archivo o por formato.
- Excluir y revisar bases locales, datos de pacientes, adjuntos, logos cargados, secretos, respaldos, logs y archivos generados, tanto en el diff como en el historial que se publique.
- Verificar destino y privacidad de GitHub. Mantener la autorización de publicación existente; al terminar una fase evaluar automáticamente commit y push, verificar resultado e informar identificador y rama.
- No usar force push, reescribir historia ni ejecutar comandos destructivos sin autorización. Ante secretos o destino incorrecto detener la publicación y resolverlo.
