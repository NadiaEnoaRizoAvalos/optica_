# Decisiones arquitectónicas

Fuente vigente de decisiones; seguimiento y confirmaciones pendientes exclusivamente en [PLANS.md](PLANS.md). El historial de entregas está en [BITACORA.md](BITACORA.md).

Estado interpretado según evidencia: «implementada» refiere a funciones existentes, «histórica» conserva el contexto original, «aprobada para planificar» no autoriza programar ni migrar y «propuesta» requiere confirmación. La aprobación original de reorganización fue exclusivamente documental; las autorizaciones posteriores se registran en las decisiones correspondientes. D-01–D-28 se contrastan con las entregas existentes; D-29–D-36 provenían de una propuesta comercial, no de módulos implementados. Los motivos originales conservan el contexto de su fecha; Estado y Consecuencias explicitan su vigencia actual.

### D-56 — Multirrol aislado en paralelo con el simulacro operativo pendiente

- **Autorización:** el 17/10/2026 el usuario permitió desarrollar/probar 5.1 en rama separada mientras realiza el ensayo físico de 5.2.A. Excepción explícita al orden condicionado de D-52; no cierra 5.2.A ni autoriza 5.2.B. Copia al carry disk exclusivamente del ZIP ficticio intacto en la carpeta nueva indicada por el usuario; sin tocar copias existentes. Kit, instrucciones, manifiestos y claves del simulacro congelados.
- **Decisión implementada:** reutilizar la relación muchos a muchos y UserManager de Identity. Sólo Administrador asigna/revoca roles explícitos con confirmación; mínimo un rol válido no repetido, cuentas inactivas sin edición, protección del último administrador activo y prohibición de quitarse el propio acceso administrativo. Permitir las siete combinaciones sin ampliar implícitamente permisos clínicos. Lista agrupada y casillas en alta/edición.
- **Consistencia y sesiones:** transacción SQLite para membresías, auditoría de cada alta/baja y sellos. Revalidar el administrador desde la base dentro de la transacción; comparar ConcurrencyStamp con el formulario. Mantener la validación de SecurityStamp en cada solicitud existente: cambios efectivos invalidan todas las sesiones de esa cuenta; una edición sin cambios no lo hace. No modificar contraseñas ni responsables históricos. No interrumpir/retroceder solicitudes ya completadas.
- **Estado:** desarrollo y 30 casos nuevos verificados en entorno ficticio; suite completa 261 aprobadas. Sin cambios estructurales o migraciones. Adopción operativa/aceptación pendientes: copia, inventario de permisos, transición y reversión aprobados antes de modificar cuentas existentes. No volver a la interfaz antigua sobre cuentas multirrol borrando membresías o datos.
- **Conservación:** políticas actuales y HttpCurrentUser ya combinan roles; se conservaron. Los controles guardan el responsable de su fecha, sin recalcularlos por roles nuevos. Fase 10 reutilizará el control de calidad de 4.1. No se modificaron artefactos del kit ni se regeneraron para esta rama.

### D-57 — Visor aislado y continuidad automática de desarrollo

- **Autorización vigente:** desarrollar 5.2.B desde 5.1 y luego 5.3 tras las pruebas automatizadas, sin esperar ensayos visuales/físicos. Sustituye los bloqueos de continuidad de D-52/D-56 exclusivamente para código/pruebas; no habilita fase 6, datos operativos, servicios, cifrado, ACL operativas ni copias reales. 5.2.A permanece pendiente de verificación operativa; kit original congelado.
- **Alojamiento:** auxiliar ASP.NET Core/Razor Pages en loopback IPv4. Reutilizar contratos/consultas existentes y extraer configuración Identity/políticas a Infrastructure, preservando adaptadores Web y permisos. El namespace nuevo de los componentes compartidos expresa su ubicación; no cambia namespaces del dominio ni motor. La fábrica de pruebas apunta al componente raíz de la aplicación para evitar ambigüedad entre entrypoints auxiliares.
- **Inmutabilidad:** autenticar manifiesto externo antes de copiar; abrir/verificar SQLite sólo en área nueva y privada. Exigir esquema compatible, sin migración, conexión ReadOnly/immutable y handles Windows que impiden escritura/reemplazo. Interceptor permite sólo auditoría independiente; no registrar trabajadores o contratos de escritura en la interfaz del visor. No usar immutable sobre una base operativa cambiante.
- **Acceso:** Identity individual, multirrol y mismo mínimo privilegio. Contraseñas verificadas con PasswordHasher sin rehash/escrituras; bloqueo y auditoría locales, cookies/claves efímeras, sin recuperar cookies de otra PC. Captura de roles no prueba vigencia actual: advertir y requerir procedimiento operativo antes de información real. Revocaciones locales no afectan otras copias ni documentos descargados.
- **Distribución:** kit nuevo ficticio con runtime privado/licencias, versión, respaldo de pruebas, CMS/hashes y lanzador guiado. Confianza recibida por canal externo; no incluir claves privadas. No se descarga SDK ni se toca el kit 5.2.A; no se cambian permisos Windows desde el visor. Ensayo físico y cifrado real siguen pendientes.
- **Fuentes técnicas:** [URI/immutable de SQLite](https://www.sqlite.org/uri.html), [cadenas Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings). Sus condiciones se respaldan con prueba de rechazo de escrituras y conservación de hashes; no equivalen a verificar hardware o custodia real.

### D-58 — Cierre automático diferenciado del cierre operativo de seguridad

- **Estado:** revisión/correcciones de código 5.3 verificadas con datos ficticios; verificaciones manuales/operativas pendientes. No equivale a producción ni a cierre global de fase 5.
- **Correcciones:** almacén de adjuntos exclusivo de lectura; extensión/MIME validado también antes de entrega inline de legado firmado, con rechazo auditado; cookies separadas por instancia. Backend comprueba rol capturado/estado local al administrar revocaciones. La interfaz principal mantiene su comportamiento y cuentas.
- **Evidencia:** solución 0 errores/advertencias, 291/291 pruebas, NuGet con transitivas sin paquetes vulnerables informados. Arranque portátil local desde system32 con Windows PowerShell y runtime privado sin SDK; credenciales/versiones/documentos/permisos/loopback comprobados. Importar módulos integrados de ese proceso resuelve PSModulePath heredado sin instalar/cambiar perfiles. No afirmar desconexión física u otra PC por ese ensayo local.
- **Continuidad:** usuario permite avanzar código con revisiones visuales pendientes. Conservar 5.1/5.2.B/5.3 en ramas encadenadas, sin fusionar a main ni modificar base operativa. Recomendar fase 6 ficticia, con autorización explícita y P-06 aplicable, mientras permanecen abiertos custodia, cifrado, servicio, respaldos reales y ensayos manuales.

### D-59 — Continuidad de backend comercial y preparación de Figma

- **Estado:** autorizada la programación no sensible pendiente de fase 5 y fases 6–11, por incrementos implementados, integrados y verificados; no pedir nueva aprobación para cada subfase pequeña. Sustituye el límite de inicio de fase 6 de D-57/D-58 exclusivamente para backend/pruebas aisladas.
- **Límites:** consultar reglas importantes no resueltas; mantener autorizaciones específicas para dependencias, migraciones operativas, discos/ACL/servicios Windows reales, cifrado, claves y copias reales. 5.2.A conserva estado operativo pendiente y kit congelado. Ninguna aceptación manual o física se presume realizada.
- **Integración:** mismo repositorio y capas; contratos de aplicación desde Blazor y endpoints sólo por necesidad. Documentar progresivamente pantallas/datos/acciones en docs/CONTRATOS-Y-PANTALLAS.md. Esperar Figma aprobado antes del diseño definitivo.

### D-60 — Catálogo práctico y reglas monetarias confirmadas

- **Aprobación:** P-06 de catálogo y P-03 monetaria confirmadas por el usuario: categorías/unidades/atributos, perfiles de acceso, datos mínimos del proveedor, moneda inicialmente ARS configurable, 2 decimales y redondeo de empates alejándose de cero. Reglas de pagos/descuentos/anulaciones confirmadas en REQUISITOS.md; su implementación corresponde a fase 7.
- **Implementación:** producto separado de variante/SKU y proveedor; asociación muchos a muchos sin duplicar artículos. Proveedor/laboratorio puede cumplir ambas funciones. Nombres de proveedores normalizados e identificador fiscal informado únicos; producto único por categoría/nombre/marca/modelo; SKU único incluso inactivo. Sin cuentas automáticas, existencias inventadas o eliminación funcional.
- **Precisión y permisos:** atributos en columnas explícitas; valores escalados enteros, conservando vacío/cero; precios en centavos con moneda explícita. Profesional usa un contrato técnico sin importes. Administrador administra, Atención/Ventas consulta. Cambios concurrentes usan versiones también de producto/moneda/proveedor para no introducir asociaciones o precios obsoletos.
- **Seguridad de cambios:** no cambiar automáticamente categoría, moneda de precios existentes o especificaciones bloqueadas por uso. Guardar futuras operaciones con sus valores históricos. Migraciones aditivas nuevas sólo ensayadas en bases ficticias; habilitación operativa y frontend pendientes.

### D-61 — Publicación técnica comercial mínima

- **Estado:** reglas confirmadas por el usuario para implementar en fase 7; no implementadas todavía en esta entrega de catálogo.
- **Alcance:** graduaciones OD/OI, distancia pupilar, producto/material/tratamiento y montaje vinculados a una versión autorizada por Profesional. Excluir antecedentes, síntomas, notas y documentos clínicos originales; no permitir editar graduaciones desde ventas.
- **Versionado:** conservar versiones publicadas; corregir/revocar no reescribe historia y bloquea nuevos pasos técnicos hasta nueva publicación válida. Sin envío automático a laboratorio.
- **Montaje confirmado:** alturas OD/OI y DP monoculares opcionales en mm con 2 decimales, referencia a armazón e instrucciones. No derivar monoculares desde la binocular ni inventar valores. Seleccionar ojos incluidos y exigir esfera explícita, eje con cilindro distinto de cero y base con prisma distinto de cero al autorizar fabricación; no endurecer por ello el registro clínico independiente.
- **Ventas confirmadas:** cristales y contactos necesitan autorización técnica incluso si hay stock. En contactos el Profesional confirma producto exacto, graduación, curva base y diámetro por ojo; no convertir automáticamente recetas de anteojos. Armazones/accesorios pueden venderse sin receta.

### D-62 — Implementación de publicaciones técnicas versionadas

- **Estado:** implementada en incremento 7.1; reglas aprobadas en D-61, regresión final documentada en BITACORA.md. Sin aplicación a la base operativa ni aceptación manual.
- **Modelo:** publicación independiente ligada por FK restrictiva a una revisión clínica; copia únicamente valores técnicos autorizados. Montaje y selección de contactos en columnas tipadas, sin notas, síntomas ni adjuntos. No crea revisiones clínicas, ventas o controles de calidad artificiales.
- **Vigencia:** revisar revisión actual, cliente activo, revocación y sustitución. Una corrección sustituye expresamente una publicación mediante identificadores/versiones y motivo; publicaciones independientes para la misma receta no se anulan entre sí. Una revisión clínica posterior invalida las publicaciones de la revisión anterior para nuevos pasos. Los módulos comerciales deberán revalidar dentro de sus transacciones.
- **Seguridad:** servicio de publicación/consulta exclusivamente Profesional; no concederlo a Ventas. El futuro pedido expondrá sólo la proyección de su trabajo, sin motivos clínicos de corrección/revocación ni acceso general al historial. Confirmación explícita, idempotencia, auditoría, concurrencia y bloqueo de especificaciones de variantes usadas.

### D-63 — Circuito inicial de pedidos y vencimientos

- **Estado:** confirmado por el usuario para implementar en 7.2; no se presenta como lógica ya disponible.
- **Pedidos:** confirmar y recibir seña con técnica pendiente; reservar sin seña obligatoria. Líneas técnicas bloqueadas para reserva/encargo hasta autorización vigente. Líneas y precios se editan sólo en borrador; tras confirmar se cancela y registra otro pedido, sin trasladar dinero. Si hubo pagos sólo Administrador cancela, con devolución explícita antes del cierre; lo entregado se devuelve. Liberación de reservas al cancelar explícita, sin caducidad automática acordada.
- **Contactos:** lote obligatorio y fecha de envase inclusiva; desde el día siguiente no reservar/entregar. Mes/año conserva esa precisión y su último día válido. No inferir un vencimiento ausente. La regla se aplica desde el primer manejo físico, sin esperar fase 9.
- **Pendientes independientes:** recepción/cancelación de encargos de laboratorio (P-06) y matriz de calidad/entregas parciales (P-05) no se consideran confirmadas por estas respuestas.

### D-64 — Pedidos comerciales e historial monetario separados de los trabajos

- **Estado:** backend implementado en 7.2, verificación final registrada en BITACORA.md. No sustituye los trabajos ópticos históricos ni habilita entrega comercial.
- **Modelo:** pedido propio con líneas por revisión de borrador, moneda/importes en enteros y precios/descripciones acordados. Conservar borradores anteriores, no editar líneas confirmadas ni revalorizar historia desde el catálogo. Variantes usadas mantienen sus especificaciones bloqueadas. No crear ventas a partir de trabajos anteriores.
- **Dinero:** cobros positivos hasta saldo y devoluciones positivas referidas a un cobro original del mismo pedido; compensar sin borrar. Administrador devuelve y cancela pedidos que tuvieron cobros; Atención también cancela los que nunca cobraron. Devolver todo antes de cerrar no transfiere fondos al nuevo pedido.
- **Atomicidad:** transacción SQLite, versión del pedido, recibo de comando con identificador/actor/acción/hash y auditoría; reintento idéntico no repite cobros ni otros efectos. No guardar formularios o datos clínicos como JSON genérico. Medio de pago explícito y referencia opcional, sin conciliación bancaria o envío externo.
- **Integración siguiente:** confirmar/cobrar no reserva ni encarga automáticamente; esas acciones revalidarán técnica y stock en 7.3/8. Asignación OD/OI/ambos confirmada por el usuario: dos cristales para ambos ojos; caja cerrada compartida sólo con producto/parámetros idénticos autorizados en ambos. Inventario mínimo sigue perteneciendo a fase 7.

### D-65 — Encargos de laboratorio: recepción y cancelación

- **Estado:** circuito inicial confirmado por el usuario durante 7.2; implementación pendiente de fase 8.
- **Reglas:** autorización técnica Profesional y comercial Administrador/Atención; registrar envío expresamente, sin mensajes automáticos. Recepción parcial permitida; observado queda en cuarentena sin disponibilidad. Cancelar sólo cantidades no recibidas y exigir confirmación registrada del laboratorio si ya se enviaron. Lo recibido conserva movimientos; material y dinero se devuelven mediante operaciones separadas.
- **Integración:** reutilizar stock/recepción idempotente del núcleo de fase 7 y proyección técnica vigente. No crear un segundo inventario ni reinterpretar recibir como aprobar calidad o entregar.
- **Confirmación posterior:** corrección/revocación tras envío bloquea nueva fabricación y recepción utilizable. Se permite registrar llegada sólo en cuarentena, conservando encargo/versión originales; nuevas especificaciones requieren autorización y nuevo encargo, con cancelación confirmada de pendientes del anterior cuando corresponda. Sin actualización silenciosa de encargos enviados.

### D-66 — Stock mínimo y asignación técnica por trabajo

- **Estado:** implementado en 7.3; evidencia de verificación en BITACORA.md. Sin migraciones operativas, cambios al kit o entregas comerciales habilitadas.
- **Stock:** batch por entrada trazable, variante exacta y presentación aprobada; movimientos separados de físico, reservado y cuarentena. Contactos necesitan lote/vencimiento; precisión día/mes conservada, último día inclusive según fecha local. Un lote de una variante no puede registrarse con vencimientos contradictorios. Expirar no libera reservas silenciosamente. No reservar cuarentena ni material no recibido; no hay sustitución automática ni salida física al reservar.
- **Técnica:** Profesional vincula explícitamente publicación/versión, línea y ojos; su cola no expone precios. Atención/Admin comerciales reciben únicamente proyección del trabajo asociado, con vigencia, sin motivos clínicos o historia general. Una caja cerrada compatible puede cubrir ambos ojos; cristales individuales requieren dos unidades para ambos y coincidencia de graduaciones. Para stock no inferir equivalencia entre vacío/cero ni convertir graduaciones; el SKU actual no describe prisma, por lo que prisma distinto de cero requiere fabricación validada o ampliar atributos antes de admitirlo como stock.
- **Concurrencia e historia:** transacciones SQLite y versiones de pedido, batch y reserva; reintentos no duplican entradas/reservas/liberaciones. Cambiar vinculación técnica exige liberar reservas anteriores y conserva asignaciones históricas. Cancelar exige liberación expresa y devoluciones monetarias según 7.2; un consumo posterior no se revierte con cancelación. Fase 9 amplía el mismo libro, no crea otro inventario.
- **Recuperación y alcance:** usar el mecanismo de respaldo existente para copiar conjuntamente relaciones de catálogo, clínica, técnica, pedidos, stock y movimientos. Comandos de consumo/entrega siguen sujetos a fase 10 y sus controles. Encargos/recepción de laboratorio se incorporan en fase 8 sobre este núcleo.

### D-67 — Matriz inicial de calidad y entrega completa

- **Estado:** aprobada por el usuario durante 7.3 para implementar en fase 10; controles actuales aún conservados, sin cambios operativos.
- **Aplicabilidad:** conservar dos etapas e historia. Cristales: receta vigente, graduación, material, montaje/centrado y estado. Contactos: producto/parámetros autorizados, lote, vencimiento y envase íntegro. Armazones/accesorios/reparación mecánica: correspondencia, integridad y trabajo realizado. Reparación con efecto en graduación/montaje usa controles de cristales. No «No aplica» para un control esencial.
- **Roles:** técnico Profesional obligatorio en cristales/contactos/reparación óptica; Atención/Ventas o Profesional en técnico no clínico y entrega. Administrador necesita los roles explícitos correspondientes, sin acceso clínico implícito.
- **Entrega:** inicialmente completa, sin parciales; identidad, pedido completo, estado, documentación/instrucciones y saldo cero. Reutilizar motor de fase 4.1 y conservar aprobaciones/versiones anteriores; revisar restricciones de dominio/SQLite y ensayar migración/reversión aisladas antes de adopción operativa.

## Decisiones anteriores D-01 a D-36

### D-01 — Monolito modular por capas

- **Descripción:** Monolito modular por capas.
- **Motivo:** Instalación local sencilla y separación de responsabilidades.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar los cuatro proyectos y responsabilidades; no reconstruir módulos.

### D-02 — ASP.NET Core + Blazor adaptable

- **Descripción:** ASP.NET Core + Blazor adaptable.
- **Motivo:** Una interfaz para navegador de PC y celulares; modo definitivo en fase 1.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Pantallas funcionales por módulo; diseño definitivo en fase 12.

### D-03 — SQLite + EF Core en el servidor

- **Descripción:** SQLite + EF Core en el servidor.
- **Motivo:** Base inicial local; revisar si aparecen contención sostenida, mayor concurrencia o nuevas necesidades operativas.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Acceso exclusivo desde el servidor, transacciones e índices; revisar contención antes de cambiar motor.

### D-04 — Adjuntos privados en disco y metadatos en SQLite

- **Descripción:** Adjuntos privados en disco y metadatos en SQLite.
- **Motivo:** Evitar acceso público y permitir respaldo conjunto; coordinar consistencia entre archivos y base.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Mantener originales fuera de carpetas públicas e incluirlos en respaldos.

### D-05 — Primero localhost, después red privada

- **Descripción:** Primero localhost, después red privada.
- **Motivo:** Validar operación en una PC antes de sumar certificados y conectividad móvil.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No habilitar red ni exposición pública sin aceptación de seguridad.

### D-06 — Sin servicios externos obligatorios

- **Descripción:** Sin servicios externos obligatorios.
- **Motivo:** Mantener operación sin internet.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Recursos y operación habitual locales; integraciones futuras opcionales.

### D-07 — Sin automatización clínica

- **Descripción:** Sin automatización clínica.
- **Motivo:** Conservar registros profesionales y evitar inferencias médicas.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Registrar información profesional; nunca generar diagnósticos o prescripciones.

### D-08 — Soporte Blazor interactivo en servidor, interactividad por componente cuando se necesite

- **Descripción:** Soporte Blazor interactivo en servidor, interactividad por componente cuando se necesite.
- **Motivo:** Evitar activar circuitos para una pantalla informativa; probar reconexión antes de incorporar formularios interactivos.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Mantener SSR actual; no introducir circuitos protegidos sin revalidación apropiada.

### D-09 — Arranque manual en localhost:5080 durante desarrollo

- **Descripción:** Arranque manual en localhost:5080 durante desarrollo.
- **Motivo:** No configurar servicios de Windows, certificados ni red antes de validar el arranque básico.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar perfil local; instalador/servicio y actualizaciones se validan en fase 12.

### D-10 — EF Core y proveedor oficial SQLite 10.0.12

- **Descripción:** EF Core y proveedor oficial SQLite 10.0.12.
- **Motivo:** Instalación autorizada, compatibilidad verificada mediante compilación y pruebas. Fuentes: [proveedores](https://learn.microsoft.com/en-us/ef/core/providers/), [SQLite](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/) y [paquete](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.12).
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No cambiar versiones ni instalar paquetes sin autorización y verificación.

### D-11 — Contexto inicial sin entidades ni migración vacía; fábrica por operación

- **Descripción:** Contexto inicial sin entidades ni migración vacía; fábrica por operación.
- **Motivo:** Incorporar modelos al implementar cada módulo, sin inventar tablas de negocio durante fase 1. Primera migración junto con la primera entidad real; revisión y aplicación explícitas.
- **Estado:** Histórica de arranque cumplida; fábrica por operación vigente. Ya existen entidades y migraciones.
- **Consecuencias:** No crear migraciones vacías; conservar contextos cortos al ampliar módulos.

### D-12 — Directorio privado configurable y validado; SQLite con claves foráneas y sin pool inicial

- **Descripción:** Directorio privado configurable y validado; SQLite con claves foráneas y sin pool inicial.
- **Motivo:** Separar archivos públicos y privados, facilitar cierre de archivos y recuperación. Validaciones de rutas no reemplazan permisos/cifrado ni revisión del equipo.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Mantener rutas privadas, FKs y protección Windows; cifrado pendiente en fase 5.

### D-13 — ASP.NET Core Identity y páginas Razor HTTP para cuentas; Blazor para interfaz del sistema

- **Descripción:** ASP.NET Core Identity y páginas Razor HTTP para cuentas; Blazor para interfaz del sistema.
- **Motivo:** Cookies y operaciones de identidad requieren ciclo HTTP. No usar SignInManager en componentes interactivos. Fuentes: [Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0) y [configuración de cookies y sello](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0).
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Reutilizar Identity y formularios HTTP protegidos, sin mecanismo de autenticación nuevo.

### D-14 — Primer administrador y recuperación sólo por consola local

- **Descripción:** Primer administrador y recuperación sólo por consola local.
- **Motivo:** Sin registro público, credenciales por defecto ni entrega de contraseñas por chat. El acceso depende del usuario autorizado de Windows.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar cuentas y recuperación local; no crear usuarios o claves automáticamente.

### D-15 — Revalidación de cookie en cada solicitud; pantallas protegidas estáticas en esta fase

- **Descripción:** Revalidación de cookie en cada solicitud; pantallas protegidas estáticas en esta fase.
- **Motivo:** Revocar acceso inmediatamente al inactivar o cambiar contraseña. Antes de introducir circuitos con datos protegidos, incorporar revalidación del circuito.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Cambios de roles deben revocar acceso de sesiones existentes y mantener comprobaciones backend.

### D-16 — Roles cerrados y un rol por cuenta en esta entrega

- **Descripción:** Roles cerrados y un rol por cuenta en esta entrega.
- **Motivo:** Administrador gestiona usuarios/auditoría; Profesional habilita políticas clínicas; AtenciónVentas no recibe permisos administrativos o clínicos. No hay módulos clínicos todavía.
- **Estado:** Vigente en el código actual: un rol por cuenta. Sustitución multirrol aprobada para planificar en D-37; implementación pendiente de fase 5.
- **Consecuencias:** No modificar membresías durante documentación; probar combinaciones y último administrador antes de multirrol.

### D-17 — Documento obligatorio, normalizado y único incluso en clientes inactivos

- **Descripción:** Documento obligatorio, normalizado y único incluso en clientes inactivos.
- **Motivo:** Confirmado por el usuario en fase 3. Contactos opcionales; nacimiento y domicilio excluidos inicialmente. Nombres/teléfonos coincidentes advierten, pero permiten confirmar personas distintas con documentos diferentes.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No reducir campos obligatorios ni romper unicidad/historial de clientes.

### D-18 — Clientes y visitas mediante componentes Blazor con renderizado estático y formularios POST protegidos

- **Descripción:** Clientes y visitas mediante componentes Blazor con renderizado estático y formularios POST protegidos.
- **Motivo:** Mantener contexto por operación y evitar circuitos de autenticación persistentes. Búsquedas por POST para no incluir datos personales en URL/logs de solicitudes. Sin paquetes nuevos.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar formularios POST, validaciones y protección CSRF; no exponer búsquedas personales en URL.

### D-19 — Clasificación histórica en cada visita, calculada desde visitas completadas previas

- **Descripción:** Clasificación histórica en cada visita, calculada desde visitas completadas previas.
- **Motivo:** No reescribir el pasado al sumar nuevas atenciones. La clasificación de ficha/listado corresponde a la próxima atención; no equivale a compras previas. Una visita en curso por cliente.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No recalcular silenciosamente clasificación de visitas anteriores.

### D-20 — Concurrencia con token Guid y restricción única de documento en SQLite

- **Descripción:** Concurrencia con token Guid y restricción única de documento en SQLite.
- **Motivo:** Evitar sobrescrituras y duplicados aun entre operaciones simultáneas. Alta y visita incluyen ID de envío para no repetir la operación ante reenvíos. Cambios y auditoría se guardan atómicamente.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Reutilizar tokens y transacciones e impedir duplicación de pagos/reservas futuras.

### D-21 — Visitas administrativas; claves estables y vínculo restrictivo cliente/visita

- **Descripción:** Visitas administrativas; claves estables y vínculo restrictivo cliente/visita.
- **Motivo:** En fase 4 se podrán vincular recetas y antecedentes con cliente y visita sin eliminar historial ni aceptar vínculos cruzados. No anticipar tablas ni campos médicos.
- **Estado:** Implementada y ampliada por fase 4; las referencias a un futuro módulo clínico son históricas.
- **Consecuencias:** Preservar vínculos cliente/visita/receta y autorización clínica.

### D-22 — Una configuración por instalación y contrato separado de los clientes

- **Descripción:** Una configuración por instalación y contrato separado de los clientes.
- **Motivo:** SQLite conserva una fila BusinessSettings; campos opcionales, versiones de concurrencia y marca genérica por defecto. Misma cabecera para Blazor y páginas de cuentas; namespaces internos sin cambios.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar identidad configurable y separación entre establecimiento y paciente.

### D-23 — Logo normalizado en BLOB, sin archivo original ni ruta de usuario

- **Descripción:** Logo normalizado en BLOB, sin archivo original ni ruta de usuario.
- **Motivo:** Atomicidad con configuración y respaldo incluido en SQLite. PNG/JPEG máximo 2 MB/2048 px, decodificación real y salida PNG estática hasta 512 px. [SkiaSharp 4.153.1](https://www.nuget.org/packages/SkiaSharp/4.153.1) y [licencia](https://github.com/mono/SkiaSharp/blob/main/LICENSE.txt).
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar validación/normalización del logo y su persistencia privada.

### D-24 — Publicar sólo nombre y logo para presentación

- **Descripción:** Publicar sólo nombre y logo para presentación.
- **Motivo:** Datos completos y edición requieren business.manage (Administrador). Futuras salidas de documentos podrán reutilizar el perfil con permisos definidos en su fase; no se implementan comprobantes todavía.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No publicar datos completos del establecimiento ni otorgar permisos clínicos por marca/configuración.

### D-25 — Primer commit integra la base existente y la ampliación verificada

- **Descripción:** Primer commit integra la base existente y la ampliación verificada.
- **Motivo:** El proyecto no tenía Git ni historial y el remoto está vacío. No se inventan commits de fases anteriores ni se fuerza el remoto. Primer push requiere revisión del inventario; luego rige autorización de pushes automáticos satisfactorios.
- **Estado:** Histórica cumplida: inicialización y primer push realizados; autorización posterior vigente.
- **Consecuencias:** No inventar commits de fases anteriores ni reescribir historial; verificar privacidad y SHA al publicar.

### D-26 — Originales inmutables y metadatos de adjuntos por versión, sin cupos de cantidad

- **Descripción:** Originales inmutables y metadatos de adjuntos por versión, sin cupos de cantidad.
- **Motivo:** SHA-256 antes de descarga, estados Pending/Available/Failed/Retired, FK restrictivas y paginación. Los límites administrables sólo afectan cargas nuevas; los documentos previos permanecen accesibles.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No imponer cupos de historial; validar/hash/retirar sin eliminar originales.

### D-27 — Validación PDF con PdfPig 0.1.16, sin reescritura

- **Descripción:** Validación PDF con PdfPig 0.1.16, sin reescritura.
- **Motivo:** Biblioteca mantenida incorporada con autorización. Apertura estructural estricta y profundidad acotada; no se ejecutan ni previsualizan PDF dentro de la app. Fuentes: [paquete](https://www.nuget.org/packages/PdfPig/0.1.16), [opciones oficiales](https://github.com/UglyToad/PdfPig/blob/master/src/UglyToad.PdfPig/ParsingOptions.cs). No es una transformación del documento ni evaluación médica de su contenido.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** Conservar PDF original y validación; un lector PDF no es un generador para encargos.

### D-28 — Optimización PNG sin pérdida y HEIC/HEIF pendiente de conversor verificado

- **Descripción:** Optimización PNG sin pérdida y HEIC/HEIF pendiente de conversor verificado.
- **Motivo:** Recompresión IDAT sin cambiar píxeles, orientación, profundidad ni metadatos; opción explícita y copia descargable. No recomprimir PDF/JPEG. El enum HEIF no demuestra que un decoder esté disponible: [API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skencodedimageformat), [seguimiento del proyecto](https://github.com/mono/SkiaSharp/issues/2887). No habilitar conversiones sin pruebas de conservación.
- **Estado:** Implementada según las entregas registradas; condiciones de revisión vigentes.
- **Consecuencias:** No convertir HEIC ni recomprimir documentos con pérdida sin evaluación y autorización.

### D-29 — Acceso exclusivamente de empleados y base por instalación

- **Descripción:** Acceso exclusivamente de empleados y base por instalación.
- **Motivo:** Clientes, médicos externos y proveedores son registros; sin registro público, portal ni multiempresa compartida.
- **Estado:** Principio aprobado y vigente; acceso interno conservado.
- **Consecuencias:** Clientes, médicos, laboratorios y proveedores son registros sin cuenta automática.

### D-30 — Producto, variante, presentación, oferta y lote separados

- **Descripción:** Producto, variante, presentación, oferta y lote separados.
- **Motivo:** Evitar duplicar artículos por proveedor, mezclar envases o ocultar atributos críticos en texto libre.
- **Estado:** Principio conservado con alcance simplificado por D-44; estructura comercial pendiente.
- **Consecuencias:** Crear sólo variantes/presentaciones necesarias; no un catálogo combinatorio ni tablas vacías.

### D-31 — Inventario por movimientos con reservas separadas y saldo reconciliable

- **Descripción:** Inventario por movimientos con reservas separadas y saldo reconciliable.
- **Motivo:** Disponibilidad excluye no utilizables y compras pendientes; transacciones, concurrencia y reintentos seguros.
- **Estado:** Alcance básico aprobado en D-40; implementación pendiente.
- **Consecuencias:** Núcleo seguro de disponibilidad/reservas en fase 7, ampliación en 9; sin stock ficticio.

### D-32 — Pedido comercial distinto del trabajo OpticalWork existente

- **Descripción:** Pedido comercial distinto del trabajo OpticalWork existente.
- **Motivo:** Preservar calidad, recetas/versiones y trabajos históricos; no inventar ventas retroactivas ni un segundo motor de control.
- **Estado:** Aprobada como dirección de integración; vínculo comercial pendiente.
- **Consecuencias:** Reutilizar OpticalWork y controles, conservar trabajos previos sin ventas retroactivas.

### D-33 — Datos técnicos por ojo y montaje versionados

- **Descripción:** Datos técnicos por ojo y montaje versionados.
- **Motivo:** Aprobación profesional explícita; ningún cálculo de compatibilidad clínica ni sustitución automática.
- **Estado:** Propuesta para especificaciones técnicas comerciales; clínica OD/OI existente conservada.
- **Consecuencias:** Separar receta original de copia técnica aprobada para un pedido.

### D-34 — Autorización comercial distinta de técnica y documentos con proyecciones por rol

- **Descripción:** Autorización comercial distinta de técnica y documentos con proyecciones por rol.
- **Motivo:** Administrador aprueba costos/compra; Profesional prepara el anexo técnico mínimo; Ventas recibe resumen sin antecedentes ni graduaciones del paciente.
- **Estado:** Revisada por D-43; la vista técnica habilitada sigue pendiente de campos y permisos.
- **Consecuencias:** No ampliar acceso clínico ni envío a proveedores por ocultamiento de botones.

### D-35 — Etapas 5.A/5.B y 6.C–6.G

- **Descripción:** Etapas 5.A/5.B y 6.C–6.G.
- **Motivo:** Conservar fases 5–8 y exigir aprobación entre entregas. Priorizar backend; UX/UI definitivo y licencias quedan fuera.
- **Estado:** Sustituida como planificación por D-39. Las etapas A–G no fueron implementadas.
- **Consecuencias:** Conservar su propuesta e historia; no usar A–G como tareas vigentes.

### D-36 — Escalas exactas para unidades e importes

- **Descripción:** Escalas exactas para unidades e importes.
- **Motivo:** Propuesta de enteros escalados y moneda explícita; validar precisión/redondeo antes de código y conservarlos en documentos.
- **Estado:** Propuesta: escalas unitarias y reglas monetarias pendientes de confirmación P-03 de PLANS.md.
- **Consecuencias:** No usar float/double ni asumir que SQLite calcula importes exactos; no fijar moneda o redondeo sin acuerdo.

## Nuevas decisiones de la reorganización

### D-37 — Múltiples roles por cuenta

- **Descripción:** Permitir combinaciones explícitas de Administrador, Atención/Ventas y Profesional, preservando cuentas y autorización por operación.
- **Motivo:** Una misma persona puede cumplir responsabilidades distintas sin cuentas compartidas.
- **Estado:** Aprobada para planificar; no implementada. Sustitución prevista de D-16.
- **Consecuencias:** Fase 5 debe revisar contratos/listados, asignación/retiro, último administrador, concurrencia, sesiones y auditoría. Admin solo no concede clínica; la implementación requiere aprobación de inicio.

### D-38 — Igual importancia e independencia clínica y comercial

- **Descripción:** Conservar íntegros recetas, graduaciones OD/OI, síntomas, antecedentes, consultas, profesionales, adjuntos y versiones; atender sin compra.
- **Motivo:** El sistema gestiona salud visual y operación comercial como áreas fundamentales.
- **Estado:** Aprobada expresamente; módulos clínicos existentes se conservan.
- **Consecuencias:** Ninguna venta, pedido, pago, reserva o laboratorio será requisito para registrar o consultar clínica autorizada; mantener pruebas de atención sin compra.

### D-39 — Fases futuras 5–12 orientadas al trabajo diario

- **Descripción:** Reemplazar sólo la planificación futura A–G por seguridad, catálogo, ventas/pagos, laboratorio, inventario mixto, calidad/entregas, administración comercial y producto final.
- **Motivo:** Priorizar una versión práctica para ópticas pequeñas/medianas y conservar lo completado.
- **Estado:** Plan aprobado; fases 5–12 pendientes de implementación y autorización individual. Sustituye D-35 como plan.
- **Consecuencias:** Fases 0–4.1 permanecen completas en historial; no iniciar fase 5 durante esta reorganización.

### D-40 — Inventario mixto con núcleo mínimo en ventas

- **Descripción:** Un pedido puede combinar existencias físicas y encargos; comprobar cada material, reservar sólo recibido utilizable y detectar faltantes desde fase 7.
- **Motivo:** Se confirmó como modalidad definitiva, necesaria para vender correctamente.
- **Estado:** Alcance aprobado; no implementado.
- **Consecuencias:** Fase 9 amplía administración, no demora las reservas indispensables. Lotes/vencimiento cuando aplican, transacciones, no negativos ni sustitución clínica automática.

### D-41 — Seguridad y recuperación antes de datos clínicos reales

- **Descripción:** Adelantar a fase 5 respaldos manuales/automáticos consistentes, adjuntos, copia externa, cifrado evaluado, alertas y recuperación completa.
- **Motivo:** Las copias sólo en el disco principal no cubren su falla.
- **Estado:** Prioridad y bloqueo de uso real aprobados; copia manual existente, resto pendiente.
- **Consecuencias:** Evaluar BitLocker y cifrado externo sin activarlos hasta verificar custodia/recuperación de claves y autorización. Conservar copias anteriores; restaurar en entorno aislado antes de reemplazar datos.

### D-42 — Pantallas funcionales ahora y UX/UI definitivo en fase 12

- **Descripción:** Cada módulo debe permitir una prueba práctica desde navegador aun priorizando backend; rediseño integral posterior.
- **Motivo:** El usuario debe poder comprobar cada avance sin herramientas de desarrollo.
- **Estado:** Aprobada; requisito para toda entrega futura.
- **Consecuencias:** Incluir procedimiento, roles necesarios y datos ficticios; no aceptar funcionalidades únicamente backend.

### D-43 — Vista técnica comercial habilitada por Profesional

- **Descripción:** Separar receta/historial completos de especificaciones de un pedido, publicadas y versionadas para lectura comercial autorizada.
- **Motivo:** Ventas necesita datos suficientes sin acceso general a información médica.
- **Estado:** Principio aprobado para planificar; campos y permisos pendientes de P-02. Sin cambio de permisos actuales.
- **Consecuencias:** Permiso específico, pertenencia al pedido, publicación/revocación y auditoría en backend; Ventas no interpreta ni modifica graduaciones clínicas. Amplía de forma controlada la propuesta D-34.

### D-44 — Catálogo y encargos de laboratorio sencillos

- **Descripción:** Cuatro categorías iniciales, variantes reales y ficha práctica de laboratorio/proveedor; encargo directamente vinculado al pedido con fechas y estados.
- **Motivo:** Evitar procesos comerciales y entidades que aún no aportan valor real.
- **Estado:** Simplificación aprobada; detalles de campos/modelo sujetos a fase 6 y 8.
- **Consecuencias:** Posponer PurchaseAllocation, compras agrupadas y recepciones complejas. Conservar separación entre compra, venta, pedido y trabajo sin imponer un ERP.

### D-45 — Cinco documentos con responsabilidades únicas

- **Descripción:** AGENTS reglas, REQUISITOS comportamiento vigente, PLANS seguimiento, BITACORA historial y DECISIONES decisiones con estado.
- **Motivo:** Reducir duplicaciones y evitar tratar un plan antiguo como implementación.
- **Estado:** Reorganización documental aprobada para esta tarea.
- **Consecuencias:** Conservar íntegro el historial anterior y D-01–D-36; mantener decisiones pendientes en una sola sección de PLANS.

### D-46 — Adaptación de calidad con compatibilidad histórica

- **Descripción:** Reutilizar el motor existente y definir verificaciones esenciales/aplicables por producto o trabajo, con responsable, resultado, observaciones y fecha.
- **Motivo:** Armazones, anteojos y contacto tienen comprobaciones distintas.
- **Estado:** Adaptación aprobada para planificar; matriz nueva y migración aún no aprobadas ni implementadas.
- **Consecuencias:** Actualmente se exigen ambas etapas en dominio/SQLite. No marcar aprobaciones ficticias ni reinterpretar controles anteriores; presentar análisis y pruebas de preservación antes de cambiar reglas en fase 10.

Fuente histórica: PLANS.md del commit 94301cd. Las descripciones y motivos originales D-01–D-36 se conservan literalmente en las fichas anteriores, incluidos sus enlaces. La interpretación actual no convierte propuestas anteriores en implementaciones.

### D-47 — Respaldos con motor compartido y ejecución sin sesión HTTP

- **Descripción:** Extraer ClinicalSnapshotWriter del respaldo manual y reutilizarlo para tareas encoladas/programadas dentro del servidor; serializar copias y cambios de adjuntos, conservar hashes y verificar SQLite/relaciones antes del manifiesto completo.
- **Motivo:** Automatizar sin duplicar el mecanismo ni depender del navegador, manteniendo compatibilidad del respaldo local anterior.
- **Estado:** Implementada y probada con destinos/datos ficticios en 5.2; aceptación e instalación operativa pendientes.
- **Consecuencias:** Migración aditiva AutomaticBackups generada, sin aplicar a la base de uso. El programador no migra al arrancar; una base anterior muestra aviso. Cuentas y módulos clínicos/calidad conservados; 5.1/5.3 pendientes.

### D-48 — Destino por GUID y autorización separada de escritura externa

- **Descripción:** Seleccionar volumen explícitamente y verificar NTFS, disco físico independiente, capacidad y ACL. Configuración desactivada/sin hora inicial y Backups:ExternalWritesAuthorized=false por defecto; no elegir otro destino si falta el configurado.
- **Motivo:** Evitar depender de letras Windows o modificar discos reales durante el desarrollo autorizado sólo en aislamiento.
- **Estado:** Implementada; hardware real, carpeta privada, registro del servicio y activación requieren autorización y revisión previas.
- **Consecuencias:** Sólo Administrador administra/verifica respaldos, sin permiso clínico añadido. La identidad de sistema para tareas internas no crea cuenta Identity ni simula un usuario web. WindowsServices 10.0.12 autorizado/agregado; servicio Windows no registrado.

### D-49 — Recuperación aislada y cifrado sujeto a autorización

- **Descripción:** Verificar/restaurar en directorio privado vacío, sin iniciar servidor, reemplazar base activa ni habilitar programador. Evaluar BitLocker/To Go y custodias de claves fuera de los respaldos; configuración exportada mediante lista permitida y sin secretos.
- **Motivo:** Comprobar recuperabilidad sin poner en riesgo la instalación original ni confundir hashes con protección criptográfica contra manipulación deliberada.
- **Estado:** Restauración ficticia implementada/probada; cifrado, recuperación de claves y pérdida física del equipo pendientes.
- **Consecuencias:** No usar datos clínicos reales. La migración/restauración operativa requiere procedimiento y autorización específicos; no hay botón de reemplazo activo. El destino con acceso de administrador Windows requiere custodia y cifrado adicionales a los permisos de aplicación.

### D-50 — Carpeta relativa y vinculación explícita por instalación

- **Descripción:** Cada óptica configura volumen y carpeta relativa; un marcador privado liga carpeta/volumen/instalación a un ID persistido. Guardar configuración no prepara ni modifica carpetas; vincular requiere autorización de escritura, carpeta existente/privada y programación desactivada. Cambiar destino invalida la vinculación.
- **Motivo:** Las letras cambian y algunos adaptadores anuncian GUID/series genéricos; no confiar en esos valores solos. Evitar rutas específicas de un negocio y conservar el origen real de cada copia histórica.
- **Estado:** Implementada/probada con 157 casos de suite; preparación privada concreta autorizada y comprobada sólo con archivo ficticio. Migraciones y uso operativo reales pendientes.
- **Consecuencias:** Sin marcador esperado no se escribe ni se elige otro destino; no se sobrescribe marcador ajeno/dañado. Cada ejecución conserva carpeta/vinculación y la retención no cruza destinos. BackupDestinationBinding preserva ruta histórica y desactiva programación anterior hasta revisión; no se aplicó operativamente. El marcador no es cifrado ni protección contra clones deliberados. ACL concretas y cualquier futura identidad de servicio requieren revisión autorizada.

### D-51 — Selector de carpetas del servidor y creación separada

- **Descripción:** Explorar sólo directorios visibles/permitidos en un volumen NTFS físicamente independiente, identificado mediante el mecanismo existente. Confirmar ruta completa antes de guardar volumen/carpeta relativa. Revalidación de existencia y límites en servidor; handles Windows sin compartir escritura/eliminación durante navegación/selección/creación, rechazo de reparse points y rutas protegidas. Crear sólo con bandera de autorización, confirmación y padre privado; no cambiar ACL existentes, sobrescribir o borrar. No lista archivos ni abre contenido clínico.
- **Motivo:** El navegador del visitante no representa el sistema de archivos del servidor; impedir rutas falsas/escapes y no dar privilegios de disco por ser Administrador de la aplicación.
- **Estado:** Implementada y verificada con datos/directorios ficticios; creación real no autorizada. Migraciones previas ya aplicadas con autorización, según bitácora; D-50 conserva la evidencia histórica de su entrega original.
- **Consecuencias:** Sin nuevas tablas ni dependencias para el selector. El guardado no activa respaldos; conserva parámetros al navegar y mensajes españoles. Se reutilizan volumen/permisos y se separa interfaz de reglas/archivos. Referencia técnica: [CreateFile y directorios](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea).

### D-52 — Continuidad de fase 5 y cierre verificable

- **Descripción:** Usuario autorizó código y pruebas en orden 5.2.A → 5.1 → 5.2.B → 5.3, sin fase 6. No se necesita otra aprobación por edición; sí las autorizaciones específicas de instalaciones, datos, migraciones operativas, permisos, disco, servicio y cifrado. No iniciar otra subfase con cierre previo incumplido.
- **Estado:** Aprobada. Visor independiente de sólo lectura diseñado/aprobado y pendiente después de multirrol. Firmas CMS/PKCS#7 propuestas para autenticidad; paquete oficial autorizado únicamente para desarrollo/pruebas ficticias, sin claves o firmas operativas.
- **Consecuencias:** Hashes detectan corrupción, no autenticidad frente a manipulación; 5.2.A continúa abierta mientras falten firmas/cifrado/custodia y evidencias operativas. No presentar módulos comerciales futuros ni el visor como implementados.

### D-53 — Firmas CMS con confianza independiente del respaldo

- **Descripción:** System.Security.Cryptography.Pkcs 10.0.13 autorizado; firma separada del manifiesto completo, SHA-256 y certificado RSA mínimo 3072 con uso DigitalSignature. El almacén Windows CurrentUser/My se consulta sólo en lectura bajo la identidad ejecutora; no se generan claves ni exportan claves privadas. Huellas SHA-256 confiables/revocadas se aprovisionan fuera del respaldo y con autorización.
- **Estado:** Implementada y probada con certificados ficticios efímeros; activación, claves, custodia y ensayo en otra PC pendientes de autorización. NuGet alertó vulnerabilidades al intentar 10.0.12; se corrigió a 10.0.13 dentro de la línea autorizada, sin suprimir auditorías.
- **Consecuencias:** Formato 3 siempre exige firma; RequireSignature obliga también para copias antiguas. Mantener compatibilidad explícita con formato 2 no prueba autenticidad ni impide degradación intencional: producción debe exigir firmas. Se valida criptografía con CheckSignature(true) y confianza local por huella, sin consultas a internet ni confianza automática en certificados incluidos. Expiración se controla al firmar; por sí sola no invalida firmas históricas confiables. Revocación se gestiona explícitamente sin CRL/OCSP automático; SigningTime no es sellado temporal independiente. Rotación, compromiso y custodia se describen en README; la firma no cifra datos ni sustituye seguridad del equipo.
- **Referencias:** [SignedCms.CheckSignature](https://learn.microsoft.com/dotnet/api/system.security.cryptography.pkcs.signedcms.checksignature), [paquete 10.0.13](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/10.0.13), [aviso de seguridad](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).

### D-54 — Recuperación independiente y preparación operativa verificable

- **Descripción:** Consola local OpticaSosa.Recovery como composición de mantenimiento separada, reutilizando Infrastructure/BackupVerifier sin arrancar Web, Identity, migraciones o programador. Firma obligatoria, confianza externa, destino vacío privado, sin sustituir datos existentes. Comprueba PFX con carga efímera y posesión de clave; contraseñas sólo por terminal oculta. No es el visor 5.2.B. Sin paquetes nuevos; requiere runtime compatible para su publicación actual.
- **Estado:** Implementada y ensayada con originales/cuentas/calidad ficticios y dos PFX protegidos en memoria. Claves/custodias operativas y ensayo en otra PC física pendientes. Revisión del código no encontró cifrado de datos persistidos con Data Protection: se usa para sesiones/tokens de Identity/CSRF, que deben renovarse en otro equipo. Revisar de nuevo si cambia ese uso.
- **Consecuencias:** Consulta administrativa y bloqueo externo mediante estado BitLocker por GUID: desconocido, suspendido, incompleto, bloqueado o sin protector de recuperación no permiten copiar. Se consulta sólo estado/contadores, nunca contraseñas. Falta de acceso al proveedor requiere revisar la cuenta/WMI con autorización, no elevar automáticamente el servicio. El plan PowerShell de SCM no tiene efectos por defecto y exige identidad/derechos/ACL revisados antes de gestión operativa; inicia automáticamente con Windows sólo después de instalación autorizada. Pruebas de worker/plan no son evidencia de SCM real, reinicio Windows, recuperación BitLocker ni independencia física de custodias. 5.2.A conserva bloqueos y no habilita 5.1.
- **Referencias:** Procedimientos de custodia, instalación/desinstalación y acta de otra PC en README; referencias oficiales Microsoft allí. La consulta puntual del equipo/disco se conserva como evidencia histórica sin publicar identificadores privados ni claves.

### D-55 — Kit de simulacro con runtime privado, sin instalaciones

- **Descripción:** Segunda PC confirmada Windows 11 x64; incluir host/fxr y shared Microsoft.NETCore.App/Microsoft.AspNetCore.App 10.0.13 ya instalados, licencias y aplicación de recuperación, sin SDK ni descargas/instalación de dependencias nuevas. El host privado ejecuta la DLL con DOTNET_ROOT del propio proceso, sin modificar configuración global. No se presenta como publicación self-contained estándar: es un paquete con runtime privado explícito.
- **Estado:** Preparación/pruebas locales autorizadas; copia al carry disk, ejecución física y confirmación del usuario pendientes. Primer ensayo permite datos ficticios sin cifrar, sin autorizar BitLocker ni claves operativas; seguridad de producción continúa pendiente.
- **Consecuencias:** ZIP SHA-256 recibido fuera del carry disk permite comprobar el paquete antes de ejecutar scripts; CMS y huella pública independiente autentican manifiesto y hashes del contenido. Certificado ficticio efímero, sin clave privada en el kit ni almacén operativo. Generación sólo en directorio aislado de pruebas, scripts sin registro de servicios/migraciones o red; nueva carpeta privada local, inventario, prueba negativa en una copia derivada y revalidación del original. Runtime incluido debe actualizarse/revalidarse al distribuir nuevas versiones. La declaración de red desconectada y confirmación física corresponden al usuario, no se deducen de un test local. Una política corporativa que impida ejecutar se respeta, sin cambios globales.
- **Referencias:** [distribución .NET](https://learn.microsoft.com/dotnet/core/deploying/), [soporte Windows .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md); procedimiento y límites en docs/PRUEBA-SEGUNDA-PC.md.

Dirección de dependencias conservada: Aplicación depende de Dominio; Infraestructura implementa contratos de Aplicación; Web invoca Aplicación y compone Infraestructura. Las reglas no dependen de la interfaz ni de SQLite. No se requiere una API separada para los celulares en el alcance inicial.
