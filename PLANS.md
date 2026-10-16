# Plan vigente — sistema Óptica

## Estado y límites de esta entrega

Reorganización documental autorizada el 16/10/2026. Las fases 0–4.1 conservan su estado completado según las verificaciones existentes. Las fases 5–12 están definidas y pendientes: aprobar este plan no autoriza comenzar su programación.

Esta tarea modifica exclusivamente AGENTS.md, REQUISITOS.md, PLANS.md, BITACORA.md y DECISIONES.md. No modifica código, base operativa, migraciones, cuentas, contraseñas ni dependencias.

[REQUISITOS.md](REQUISITOS.md) define comportamiento; [AGENTS.md](AGENTS.md), reglas permanentes; [DECISIONES.md](DECISIONES.md), decisiones y estados; [BITACORA.md](BITACORA.md), historial y evidencia de verificaciones. El plan anterior se conserva allí como histórico, sin continuar sus numeraciones caducas.

## Fases completadas

| Fase | Entrega conservada | Evidencia histórica |
| --- | --- | --- |
| 0 | Alcance y planificación inicial | Documentos y decisiones iniciales. |
| 1 | Base técnica por capas y aplicación local | Compilación y 8 pruebas aprobadas en esa entrega. |
| 2 | Usuarios, permisos y auditoría | Compilación y 23 pruebas aprobadas en esa entrega. |
| 3 | Clientes, documento obligatorio y visitas | Compilación y 44 pruebas aprobadas en esa entrega. |
| 4 | Recetas, antecedentes, versiones y adjuntos privados | Cierre registrado con 94 pruebas aprobadas; entregas intermedias en BITACORA.md. |
| 4.1 | Reorganización funcional, trabajos y controles de calidad | 113 pruebas aprobadas y 16 comprobaciones de navegador registradas por separado. |

Estos conteos son históricos, no nuevas ejecuciones ni sumandos de una única batería. La configuración del negocio y los avances de GitHub también quedan conservados en la bitácora. La verificación de esta reorganización se registra allí antes del commit.

Estado actual que condiciona las próximas fases: gestión de cuentas con un rol por cuenta; respaldo manual consistente; sin catálogo, ventas/pagos, existencias ni encargos comerciales implementados. El trabajo óptico actual exige dos aprobaciones en dominio y SQLite y debe conservarse.

## Reglas para las próximas fases

- Área clínica y comercial conservan igual importancia. La atención clínica existente sigue disponible sin compra y no depende de completar las fases comerciales.
- Trabajar por funcionalidades pequeñas dentro de la fase aprobada; no saltar a otra fase sin autorización.
- Cada módulo incluye pantallas funcionales, permisos, auditoría, pruebas pertinentes y pasos para comprobarlo en el navegador.
- No usar datos clínicos reales hasta aceptar seguridad, cifrado, autorizaciones y recuperación. La primera prioridad operativa es resolver esta protección en fase 5.
- El inventario mínimo necesario para vender se implementa en fase 7; fase 9 lo amplía. Lotes/vencimientos aplican desde el primer manejo físico de productos que los requieran.
- La adaptación del circuito de calidad se realiza en fase 10. Hasta entonces se mantienen las reglas actuales y no se presentan pedidos nuevos como un circuito de entrega completo.

## Fases pendientes de implementación

### Fase 5 — Multirrol, seguridad y recuperación prioritaria

**Estado: pendiente. No comenzar sin aprobación.**

Tareas: revisar matriz de permisos y compatibilidad; habilitar asignaciones múltiples preservando cuentas/contraseñas; proteger último administrador y revocar sesiones cuando corresponda; automatizar respaldos consistentes, copia externa, retención, integridad y alertas; definir cifrado y recuperación de claves; documentar y practicar restauración aislada.

Criterios de aceptación: interfaz administrativa utilizable; cuentas anteriores conservadas; combinaciones de roles e inactivos sin escalada de privilegios; auditoría y concurrencia verificadas; administrador sin rol clínico no accede a clínica. Restauración verificable de base, todos los adjuntos/versiones y configuración, incluida pérdida del equipo; fallo o desconexión del destino informado sin eliminar copias buenas. Parámetros P-01 y matriz P-04 confirmados antes de implementar sus respectivas partes.

Pruebas: combinaciones de roles, último administrador, cambios simultáneos, sesiones revocadas, permisos clínicos/comerciales; respaldo interrumpido, disco insuficiente, destino ausente, hashes, recuperación aislada y disponibilidad de claves. No activar cifrado ni restaurar sobre datos operativos sin autorización específica.

### Fase 6 — Catálogo y proveedores prácticos

**Estado: pendiente.**

Tareas: pantallas de armazones, cristales, contactos y accesorios; códigos, variantes tipadas realmente utilizadas, unidades, precios y estado; proveedores/laboratorios y contactos. Separar atributos de producto de graduaciones individuales y montaje.

Criterios de aceptación: alta, edición, búsqueda e inactivación desde navegador, permisos y auditoría; sin duplicados ni existencias ficticias; variantes y unidades comprensibles. Resolver P-06 en lo aplicable. No implementar ofertas ni compras agrupadas.

Pruebas: códigos/variantes duplicados, precios exactos, parámetros pertinentes, permisos y conservación de referencias al inactivar.

### Fase 7 — Pedidos, ventas, señas y stock mínimo seguro

**Estado: pendiente.**

Tareas: pedidos comerciales y vínculos con trabajos/versiones técnicas; materiales de stock, de laboratorio y mixtos; precios, descuentos, moneda, pagos parciales, saldo y fechas prometidas. Implementar existencias iniciales trazables, entradas mínimas, reservas/liberaciones y faltantes desde esta fase. Proyección técnica mínima de sólo lectura para operación autorizada.

Criterios de aceptación: circuito práctico en navegador para pedido, seña, saldo, reserva y faltante; disponibilidad real, sin negativos ni doble reserva; material encargado no disponible antes de recibirlo. Publicación técnica autorizada y versionada sin acceso comercial a historia completa. Resolver P-02 y P-03 antes de sus implementaciones. No inventar ventas históricas ni habilitar entrega nueva completa antes de fase 10.

Pruebas: importes/redondeo, pago repetido o anulado, reserva concurrente de última unidad, cancelaciones, pedido mixto/por ojo, material no recibido o vencido, separación de permisos y atención clínica sin compra.

### Fase 8 — Encargos simples y recepción de laboratorio

**Estado: pendiente.**

Tareas: laboratorio, materiales/cantidades, autorización técnica y comercial, fechas estimadas/reales y estados del encargo; demoras, observaciones/correcciones y recepción por material. Documentación mínima exportable sin mensajería automática.

Criterios de aceptación: seguir y recibir un encargo desde navegador; cantidad recibida válida incorporada una sola vez al inventario existente; recepción no equivale a calidad ni entrega. Exportar no marca enviado. Resolver reglas de recepción/cancelación y datos de P-06; no introducir asignaciones de compras agrupadas.

Pruebas: permisos por proyección, cambios técnicos, envíos registrados, demora, recepción parcial, material observado, cancelación y reintento sin duplicar existencias.

### Fase 9 — Inventario ampliado

**Estado: pendiente.**

Tareas: ampliar el núcleo existente con movimientos, ajustes justificados, bajas, devoluciones, alertas y vistas reconciliables; lotes, vencimientos y conversiones exactas de presentaciones cuando correspondan.

Criterios de aceptación: administración práctica desde navegador; separar físico/reservado/disponible/no utilizable/pendiente; conservar trazabilidad sin editar saldos directamente ni crear un segundo libro de inventario. No postergar hasta aquí validaciones indispensables para fase 7.

Pruebas: reconciliación, ajuste/compensación, reintentos, última unidad concurrente, vencimiento posterior a reserva, paquetes/unidades, cancelación sin reponer consumidos y devolución revisada.

### Fase 10 — Calidad y entrega según producto/trabajo

**Estado: pendiente.**

Tareas: aprobar matriz de controles aplicables y roles; adaptar circuito existente sin duplicarlo; revisar restricciones de dominio y SQLite, compatibilidad y migración; preparación y entrega con materiales válidos y regla de pago acordada.

Criterios de aceptación: pruebas desde navegador por tipo de trabajo/producto; historia anterior conservada; ningún “no aplica” evita controles esenciales ni obliga a aprobaciones ficticias. Entregar sólo con controles aplicables aprobados y materiales disponibles/no vencidos; entrega auditada e idempotente. Resolver P-05 antes de alterar reglas existentes.

Pruebas: trabajos y aprobaciones históricos, estados listos/entregados, observaciones y cambios que invalidan aprobación, permisos, contactos vencidos, faltantes, deuda y entregas parciales si se aprueban.

### Fase 11 — Caja básica e informes operativos

**Estado: pendiente.**

Tareas: caja, ingresos por medio de pago, cierres y reportes básicos conciliados con pedidos, pagos y anulaciones, con permisos explícitos.

Criterios de aceptación: consulta y cierre desde navegador con cifras reconciliables y trazabilidad; sin contabilidad integral ni reportes que filtren información clínica o financiera fuera de permiso.

Pruebas: cierre, anulaciones, medios de pago, moneda, saldos y diferencias justificadas; regresiones del circuito de pagos.

### Fase 12 — Experiencia final y operación Windows/Wi-Fi

**Estado: pendiente.**

Tareas: rediseño visual definitivo, navegación y accesibilidad; operación en PC y tres celulares, HTTPS/firewall privados, recursos locales, instalación y actualización seguras con respaldo.

Criterios de aceptación: cuatro sesiones simultáneas en red local sin internet, pantallas adaptables, desconexión/reintento comprensibles y actualización que preserve datos. Revalidar recuperación y condiciones de uso de datos reales.

Pruebas: recorridos completos por rol, tamaños de pantalla, concurrencia, recursos sin internet, permisos de red, interrupciones y recuperación tras actualización.

## Confirmaciones pendientes — única lista vigente

Estas decisiones no bloquean la reorganización documental; deben resolverse antes de implementar el comportamiento afectado.

| ID | Confirmación necesaria | Antes de |
| --- | --- | --- |
| P-01 | Destino externo y dispositivo, frecuencia, retención, pérdida/tiempo de recuperación aceptables, responsables, cifrado compatible y custodia/recuperación de claves. | Automatización/cifrado de fase 5. |
| P-02 | Campos exactos de la proyección técnica comercial, destinatarios, aprobación/publicación y efecto de corregir o revocar una versión. Nunca incluir historia completa ni permitir editar graduaciones. | Proyección de fase 7 y documentos de fase 8. |
| P-03 | Moneda, escalas/redondeo, descuentos autorizados, excedentes/devoluciones, anulaciones, permisos de pagos y entrega con deuda. | Ventas/pagos de fase 7. |
| P-04 | Matriz multirrol, quién asigna/revoca roles, cambios de permisos propios, último administrador e invalidación de sesiones. | Cambios de cuentas de fase 5. |
| P-05 | Controles esenciales y aplicabilidad por producto/trabajo, transición de restricciones actuales y reglas de entrega parcial. | Adaptación de fase 10. |
| P-06 | Variantes/unidades y datos mínimos del catálogo/proveedor; autorización comercial, recepción parcial, material observado y cancelación del encargo. | Catálogo de fase 6 y encargos de fase 8, según corresponda. |

## Seguimiento de esta entrega y próxima acción

- Reorganización documental completada y verificada; resultados registrados en BITACORA.md. Commit y push documentales se comprueban e informan al cierre, sin un commit adicional para su propio identificador.
- Plan anterior y decisiones D-01–D-36 conservados con su contexto histórico; D-37–D-46 registran la aprobación y sus límites.
- Siguiente paso: presentar documentos, verificación y commit/push; esperar aprobación explícita para iniciar fase 5. No hay tareas de programación iniciadas por esta entrega.
