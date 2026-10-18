# Coordinación de fase 10

Rama exclusiva: `equipo/fase10-calidad`. Base compartida: `3b16707c7d8fb6cb045c8a5e58da71f9be7043f5`, código de fase 9 `a907b95`. Fase 11 pertenece al colaborador; no modificar su rama ni implementar caja.

## Incremento 10.1 — matriz de calidad

Trabajo preliminar preservado en la copia trasladada y recuperado sólo después de publicar/verificar las tres ramas comunes. No incluido en `equipo/base-backend-fase9` ni `equipo/fase11-caja`.

| Archivo compartido | Cambio necesario y responsable |
| --- | --- |
| Access | Integrador de fase 10 permite invocar control técnico a Atención/Ventas; servicio y dominio restringen los trabajos clínicos al Profesional. No ampliar acceso clínico general ni financiero. |
| WorkConfiguration | Integrador agrega tipo de trabajo al control y relación compuesta que impide falsificar su tipo para omitir el rol Profesional; amplía tipos sin reinterpretar 0–2 históricos. |
| Snapshot/migración EF | Integrador genera ProductQualityMatrix y ensaya conservación histórica. No es una migración operativa ni una migración de caja. El colaborador debe proponer sus cambios de esquema antes de generar otro snapshot simultáneamente. |
| Documentación común | Integrador registra progreso/evidencia de fase 10. El colaborador usa su propia carpeta de documentación para propuestas, sin editar estos archivos simultáneamente. |

Sin cambios a OpticaDbContext, DI, políticas globales de registro, layout o estilos en 10.1. Contratos de clientes, productos, ventas e inventario conservados. Reutilizados WorkService, OpticalWork, QualityControl, plantillas y pantallas de calidad; permisos por tipo comprobados también en servidor.

La migración reconstruye OpticalWorks/QualityControls, conserva datos y captura el tipo histórico antes de exigir la relación nueva. Se revisó el SQL de EF y se recrean sus triggers clínicos dentro de la misma transacción, con guard de FK/pertenencia antes de confirmar; el retroceso aislado compatible también los conserva. Ensayos sólo ficticios; requiere autorización operativa antes de cualquier aplicación real. No fusionar su snapshot con el futuro de caja automáticamente.

## Integración de entrega pendiente

Implementar trabajos por conjunto confirmados en D-72, vínculos explícitos con líneas/técnica, comprobación de materiales y entrega completa, auditada e idempotente, con saldo cero. No modificar registros de caja ni duplicar pagos. Registrar aquí cualquier cambio adicional a componentes compartidos antes de aplicarlo.
