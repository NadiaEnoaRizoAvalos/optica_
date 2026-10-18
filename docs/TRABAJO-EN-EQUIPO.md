# Desarrollo paralelo — fases 10 y 11

## Base común y ramas

El código de partida es `a907b95451becf4e91fa565087b649c5f66e7f7d`: backend hasta fase 9. Incluye 5.1, 5.2.B, 5.3 y 6–9 en una sola cadena de commits. 5.2.A permanece **PENDIENTE DE VERIFICACIÓN OPERATIVA**. Este avance de coordinación sólo agrega documentación a ese código; las tres ramas de equipo parten del mismo commit documental, informado al publicar.

| Rama | Responsabilidad |
| --- | --- |
| `equipo/base-backend-fase9` | Referencia común congelada después de publicarse. |
| `equipo/fase10-calidad` | Responsable de calidad/trabajos/entregas; este Codex continúa exclusivamente aquí. |
| `equipo/fase11-caja` | Colaborador: caja e informes; este Codex no implementa ni fusiona su trabajo mientras esté en desarrollo. |

`main` no se modifica. No usar force push ni reescribir historia. Conservar ramas anteriores como evidencia. Cada incremento funcional verificado se compila, prueba, revisa por secretos y publica con un commit coherente en su rama. Ninguna publicación incluye datos, ZIP, respaldos, claves ni archivos generados.

Cada persona usa su propia copia o worktree. El colaborador puede comenzar con:

```powershell
git clone --branch equipo/fase11-caja --single-branch https://github.com/NadiaEnoaRizoAvalos/optica_.git optica-caja
Set-Location optica-caja
```

La autenticación privada de GitHub es individual; no compartir contraseñas/tokens. No copiar la carpeta `data` de otra instalación. Usar bases y directorios ficticios propios; restaurar únicamente las dependencias del proyecto ya aprobadas. Ninguna instrucción de esta guía autoriza migraciones operativas, servicios Windows, cifrado, discos reales o activación de respaldos.

## Propiedad de módulos

| Responsable | Archivos y componentes propios | Dependencias que conserva |
| --- | --- | --- |
| Fase 10 | Domain/Application/Infrastructure de Work y nuevos componentes de calidad/entrega; configuración EF específica de sus entidades; pruebas propias y páginas mínimas de calidad/trabajos. | OpticalWork, QualityControl, fases 4.1 y 9, publicación técnica y disponibilidad. Matriz D-67, dos etapas/historia, Profesional explícito, entrega completa, saldo cero y sin doble consumo. |
| Fase 11 | Nuevos módulos Cash/Reports en Domain/Application/Infrastructure; configuración EF específica, pruebas propias y pantallas mínimas de caja/informes. | Pedidos/pagos existentes, dinero exacto y D-71. Apertura Administrador/Atención, cierre Administrador, motivo ante diferencias, efectivo con caja abierta, otros medios aparte; no incorporar cobros anteriores automáticamente. |

No modificar módulos del otro responsable ni crear autenticación, inventario, libro financiero o calidad paralelos. Mantener estables contratos de clientes, catálogo, ventas e inventario; cualquier cambio necesario se registra primero como solicitud de integración. La fase 10 consulta el saldo existente y no altera pagos/caja. La fase 11 no aprueba calidad ni entrega productos.

## Archivos compartidos y coordinación

La persona responsable de fase 10 actuará como integradora de archivos compartidos. Antes de que el colaborador necesite uno, debe registrar su propuesta en `docs/fase11/INTEGRACION.md`; fase 10 usa `docs/fase10/INTEGRACION.md`. Esos documentos pertenecen a cada rama y no se pisan. Indicar ruta, motivo, contrato/entidad afectada, cambio esperado, migración y pruebas. El cambio compartido lo aplica una sola persona, después de revisar ambas propuestas; no editarlo simultáneamente por cuenta propia.

| Archivo o grupo compartido | Tratamiento |
| --- | --- |
| OpticaDbContext y configuración global EF | Registrar entidades/relaciones nuevas. Integrador aplica el registro global; configuración específica permanece en su módulo. |
| DependencyInjection | Proponer una extensión de registro del módulo; coordinar una única llamada global. |
| Access y IdentitySecurityRegistration/políticas | Registrar permisos/roles requeridos; Administrador no implica Profesional ni acceso clínico. |
| Snapshot y lista de migraciones EF | Integrador coordina generación y orden. No generar/editar el snapshot simultáneamente; un borrador no autoriza aplicarlo. |
| Layout, navegación y estilos globales | Congelados para rediseño posterior con Figma; interfaz mínima sólo en el área propia. |
| AGENTS, PLANS, REQUISITOS, DECISIONES, BITACORA y contratos generales | Integrador actualiza documentación común. Cada rama registra sus detalles y verificaciones en su carpeta de documentación. |
| Fixtures comunes y proyectos/paquetes | Reutilizar sin edición concurrente; proponer extensiones específicas. Las dependencias nuevas requieren autorización. |

La matriz de fase 10 ya tiene trabajo local preliminar conservado fuera de la base compartida: no está incluido en la base de fase 9. Sus cambios globales se registrarán antes de recuperarlos en la rama de equipo. La publicación de ramas no prueba que el colaborador haya clonado, ejecutado o aprobado nada; su prueba local queda pendiente.

## Integración posterior

1. Ambos entregan commits, cambios de contratos, esquema y resultados reales de sus pruebas. No fusionar una rama activa del colaborador.
2. Integrador revisa el diff conjunto en una rama de integración nueva. Resolver archivos compartidos manualmente y conservar decisiones aprobadas.
3. Reconciliar migraciones con el modelo combinado. No resolver snapshots con «usar una versión» ni ejecutar migraciones de ambas ramas sin verificar orden, relaciones y conservación de datos.
4. Ensayar instalación limpia y actualización/restauración de una copia ficticia del esquema base; comprobar clínica, calidad, pagos, saldos, stock, caja y auditoría.
5. Compilar la solución, ejecutar pruebas de ambos módulos y regresión completa; verificar entrega con deuda, revocación técnica, concurrencia, idempotencia y cierres.
6. Integrar a `main` sólo cuando los cambios sean compatibles y estén verificados. Nunca aplicar migraciones sobre una base operativa sin autorización específica.

## Frontend y pendientes operativos

No hacer el frontend definitivo antes del Figma aprobado. Después, el usuario conserva clientes/clínica/recetas/trabajos/calidad/entregas; el colaborador, catálogo/ventas/laboratorios/inventario/caja/informes. Componentes visuales compartidos se coordinan con un sistema de diseño común.

Conservar pendientes de 5.2.A: simulacro físico, custodia de claves, cifrado autorizado, instalación/verificación del servicio Windows y activación/ensayos reales. Las pruebas automatizadas de backend no equivalen a producción ni a aceptación manual.
