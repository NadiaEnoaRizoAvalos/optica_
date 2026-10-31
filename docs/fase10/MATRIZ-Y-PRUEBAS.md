# Incremento 10.1 — controles según producto

Este documento conserva la entrega 10.1. El cierre conjunto posterior de 10.1/10.2 está en [CIERRE-BACKEND.md](CIERRE-BACKEND.md) y los contratos de entrega en [CONTRATOS-ENTREGA.md](CONTRATOS-ENTREGA.md).

Se reutilizan OpticalWork, QualityControl, WorkService, plantillas, rondas y pantallas existentes. No se crea un segundo motor de calidad. Este incremento adapta la matriz y los permisos; la agrupación de pedidos, los materiales y la entrega comercial completa pertenecen al incremento siguiente.

## Matriz

| Tipo | Control técnico | Responsable técnico |
| --- | --- | --- |
| Cristales comerciales / reparación óptica | Autorización vigente, graduación por ojo, material/tratamiento, montaje/centrado, integridad y trabajo realizado. | Profesional. |
| Contactos | Producto/parámetros autorizados, presentación, lote, vencimiento, envase cerrado e íntegro y estado. | Profesional. |
| Armazón comercial / accesorio / reparación mecánica | Correspondencia con pedido, integridad y trabajo realizado. | Atención/Ventas o Profesional. |

Todos conservan segunda etapa: identidad, pedido completo, estado/envase, documentación/instrucciones, saldo cero y materiales/controles vigentes. Todos los ítems nuevos son esenciales; no admiten «No aplica» ni desactivación por plantilla. Las verificaciones automáticas contra ventas/stock se integran en 10.2: una casilla por sí sola no habilitará una entrega comercial.

Los tipos históricos 0–2 conservan sus listas y sus snapshots. Se agregan tipos explícitos para la matriz nueva; la reparación anterior no se reclasifica automáticamente como mecánica. Las aprobaciones, fechas, responsables y rondas históricas no se reinterpretan. Administrador sin Atención/Profesional no aprueba controles; Atención sin Profesional no accede a clínica completa ni aprueba un técnico clínico. Los controles técnicos no clínicos pueden consultarse por Atención.

## Migración aislada

`20261018040139_ProductQualityMatrix` agrega el tipo capturado al control y una relación compuesta con el trabajo. SQLite reconstruye las tablas de trabajos/controles; la migración prepara el tipo histórico y la unicidad antes de copiar, para que la nueva relación sea válida durante el procedimiento. No modifica usuarios/contraseñas, clientes, recetas, adjuntos o movimientos comerciales. No aplicar sobre datos operativos sin autorización y procedimiento de respaldo/recuperación específico.

El primer ensayo preliminar detectó orden incorrecto de reconstrucción. La regresión completa detectó además que el generador no conservaba los triggers de pertenencia de recetas. La migración usa el SQL revisado del esquema y recrea esos triggers dentro de la misma transacción, con comprobación de FK antes de confirmar. La vuelta aislada al esquema anterior también los conserva. Los ensayos verifican tipos históricos, checklists, rondas, roles, fechas/versiones, credenciales, auditoría, rechazo de vínculos clínicos cruzados e integridad/FK. Los resultados reales se registran antes del commit; no publicar con un fallo pendiente.

## Comprobación desde el navegador

Verificación automática final nueva en la rama de equipo: **433 ejecutadas/aprobadas, 0 fallidas/omitidas** (83 Infraestructura, 350 Web), 11 casos nuevos respecto de la base de 422. Compilación Release completa: **0 errores y 0 advertencias**. Informes privados en data/verification-equipo-fase10-matriz-final. La tanda específica corregida 30/30 no se suma otra vez. La primera regresión de equipo tuvo 431 ejecutadas, 430 aprobadas y 1 fallida por pérdida del trigger; se corrigió antes de esta ejecución final y de publicar.

Sólo en una instalación ficticia con el esquema actualizado por su procedimiento de prueba:

1. Ingresar con Profesional, abrir un cliente ficticio y **Registrar pedido/trabajo** (`/pedidos/nuevo?cliente=GUID`). Elegir contactos, accesorio o reparación según el caso. No se exige armazón para contactos/accesorios.
2. Guardar y pasar por **Comenzar preparación → Enviar a control**. Abrir **Control técnico**, completar sus verificaciones y aprobar.
3. Para contacto/cristal/reparación óptica, comprobar que Atención no pueda abrir ni invocar el control técnico. Para un accesorio/armazón, comprobar que Atención sí pueda hacerlo.
4. Completar el control previo a entrega. La ficha conserva ambos controles, autores, estados y rondas; rechazar controles incompletos o «No aplica» en verificaciones esenciales.
5. Consultar trabajos históricos: listas y aprobaciones anteriores siguen disponibles según el permiso correspondiente.

Estas instrucciones no declaran probada la interfaz definitiva ni la entrega de un pedido comercial. No iniciar el servidor operativo, aplicar su migración ni usar datos reales como parte de este ensayo. Figma, aceptación manual y pendientes de 5.2.A se conservan.
