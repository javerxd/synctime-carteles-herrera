# Prueba de carga — fase 15A (RNFP01, RNFO13)

Fecha: 21-11-2026 · Guion: `scripts/carga/carga.mjs` · Resultado frente a la meta (el 95,0 % de las respuestas en 2 s o menos): **CUMPLE**

## Qué se midió

- **20 usuarios simultáneos** (8 visitantes, 6 clientes con cuenta y 6 personas del taller), cada uno con su propia dirección de origen; el personal comparte una, como en la oficina.
- Duración medida: 90 s (más 15 s de calentamiento que no se cuentan). Pausa entre pantallas: 0.1 a 0.4 s por persona.
- Límites de tasa de la API: **aflojados (solo para aislar el rendimiento; no es el comportamiento real)**.
- Datos sintéticos (nada real ni personal): 2.000 clientes, 6.000 solicitudes, unos 2.400 pedidos con sus abonos, 40.000 filas de bitácora y 60 publicaciones, en una **base temporal** que se borró al terminar. Nunca la base real ni la demo.
- Equipo: Windows_NT 10.0.26200, 11th Gen Intel(R) Core(TM) i5-11300H @ 3.10GHz (8 núcleos lógicos), 7.883 MB de RAM con 784 MB libres al empezar, SQL Server Express en el mismo equipo. **Las cifras dependen de este hardware**: la API, la base de datos y los veinte usuarios virtuales compiten por la misma CPU y memoria.

## Resultado

- Respuestas medidas: **8.680**. Buenas (el código esperado y en 2 s o menos): **8.662 (99,8 %)** → **CUMPLE**.
- Mediana 9 ms · p95 65 ms · p99 163 ms · máximo 5.317 ms.
- Límite de tasa (429): 0 · Errores del servidor (5xx): 0 · Sin respuesta: 0 · Respuestas distintas de las esperadas: 4.
- Códigos recibidos: 200 × 8656, 201 × 1, 202 × 19, 409 × 4.

### Por tipo de usuario

| Tipo | Respuestas | Mediana | p95 | Buenas |
|---|---:|---:|---:|---:|
| visitante | 3.820 | 5 ms | 38 ms | 99,9 % |
| cliente | 3.482 | 9 ms | 58 ms | 99,9 % |
| personal | 1.378 | 29 ms | 130 ms | 99,3 % |

### Por ruta (de la más lenta a la más rápida, por p95)

| Ruta | Respuestas | Mediana | p95 | Máximo | Buenas | Inesperadas |
|---|---:|---:|---:|---:|---:|---:|
| `POST /api/v1/mis/solicitudes` | 1 | 5.317 ms | 5.317 ms | 5.317 ms | 0/1 | 0 |
| `GET /api/v1/auditoria` | 4 | 24 ms | 5.222 ms | 5.222 ms | 3/4 | 0 |
| `POST /api/v1/auth/login-cliente` | 38 | 159 ms | 1.962 ms | 2.290 ms | 37/38 | 0 |
| `GET /api/v1/solicitudes?estado=` | 44 | 33 ms | 1.520 ms | 2.168 ms | 43/44 | 0 |
| `GET /api/v1/reportes/operativo` | 58 | 68 ms | 215 ms | 4.779 ms | 57/58 | 0 |
| `GET /api/v1/indicadores/resumen` | 64 | 99 ms | 208 ms | 1.799 ms | 64/64 | 0 |
| `GET /api/v1/reportes/gerencial` | 9 | 163 ms | 200 ms | 200 ms | 9/9 | 0 |
| `GET /api/v1/indicadores/demanda` | 64 | 84 ms | 163 ms | 1.798 ms | 64/64 | 0 |
| `POST /api/v1/auth/login` | 4 | 153 ms | 159 ms | 159 ms | 4/4 | 0 |
| `GET /api/v1/pedidos?q=` | 20 | 72 ms | 155 ms | 2.537 ms | 19/20 | 0 |
| `GET /api/v1/solicitudes/{id}` | 53 | 45 ms | 134 ms | 1.891 ms | 53/53 | 0 |
| `GET /api/v1/pedidos?estadoPedido=` | 29 | 40 ms | 118 ms | 4.931 ms | 28/29 | 0 |
| `GET /api/v1/indicadores/evolucion` | 64 | 42 ms | 110 ms | 1.797 ms | 64/64 | 0 |
| `GET /api/v1/pedidos?cobranza=` | 25 | 49 ms | 106 ms | 2.170 ms | 24/25 | 0 |
| `GET /api/v1/pedidos?anio=&mes=` | 25 | 40 ms | 101 ms | 110 ms | 25/25 | 0 |
| `GET /api/v1/pedidos/{id}` | 64 | 36 ms | 97 ms | 1.793 ms | 64/64 | 0 |
| `GET /api/v1/mis/solicitudes/{id}` | 184 | 32 ms | 94 ms | 1.541 ms | 184/184 | 0 |
| `GET /api/v1/indicadores/por-cobrar` | 64 | 38 ms | 93 ms | 1.798 ms | 64/64 | 0 |
| `GET /api/v1/solicitudes?q=` | 48 | 37 ms | 92 ms | 110 ms | 48/48 | 0 |
| `GET /api/v1/pedidos` | 25 | 40 ms | 84 ms | 1.946 ms | 25/25 | 0 |
| `GET /api/v1/indicadores/embudo` | 64 | 25 ms | 82 ms | 1.798 ms | 64/64 | 0 |
| `POST /api/v1/reportes/operativo/exportaciones` | 23 | 40 ms | 82 ms | 97 ms | 19/23 | 4 |
| `GET /api/v1/auditoria?q=` | 7 | 28 ms | 76 ms | 76 ms | 7/7 | 0 |
| `GET /api/v1/mis/solicitudes` | 617 | 26 ms | 76 ms | 1.894 ms | 617/617 | 0 |
| `GET /api/v1/solicitudes` | 43 | 35 ms | 75 ms | 79 ms | 43/43 | 0 |
| `GET /api/v1/solicitudes?origen=` | 50 | 30 ms | 68 ms | 95 ms | 50/50 | 0 |
| `GET /api/v1/catalogo?categoria=` | 244 | 18 ms | 67 ms | 2.129 ms | 243/244 | 0 |
| `GET /api/v1/reportes/parametrizable` | 17 | 30 ms | 60 ms | 60 ms | 17/17 | 0 |
| `GET /api/v1/auditoria?entidad=` | 8 | 16 ms | 56 ms | 56 ms | 8/8 | 0 |
| `GET /api/v1/clientes?q=&tamano=10` | 281 | 14 ms | 55 ms | 1.448 ms | 281/281 | 0 |
| `GET /api/v1/pedidos/calendario` | 34 | 16 ms | 50 ms | 56 ms | 34/34 | 0 |
| `GET /api/v1/catalogo (vitrina completa)` | 691 | 12 ms | 45 ms | 1.828 ms | 691/691 | 0 |
| `GET /api/v1/mi-cuenta/` | 617 | 13 ms | 45 ms | 1.894 ms | 617/617 | 0 |
| `GET /api/v1/catalogo (portada)` | 908 | 12 ms | 45 ms | 4.743 ms | 907/908 | 0 |
| `GET /api/v1/productos` | 304 | 13 ms | 45 ms | 4.759 ms | 303/304 | 0 |
| `GET /api/v1/indicadores/cobranza` | 64 | 18 ms | 43 ms | 1.798 ms | 64/64 | 0 |
| `GET /api/v1/catalogo/{id}` | 271 | 9 ms | 41 ms | 1.769 ms | 271/271 | 0 |
| `GET /api/v1/reportes/exportaciones/{id}` | 19 | 9 ms | 37 ms | 37 ms | 19/19 | 0 |
| `GET /api/v1/auditoria?desde=&hasta=` | 6 | 11 ms | 36 ms | 36 ms | 6/6 | 0 |
| `GET /api/v1/clientes` | 64 | 10 ms | 35 ms | 57 ms | 64/64 | 0 |
| `GET /api/v1/mis/pedidos` | 617 | 8 ms | 31 ms | 1.505 ms | 617/617 | 0 |
| `GET /api/v1/local/estado` | 908 | 6 ms | 19 ms | 4.743 ms | 906/908 | 0 |
| `GET /api/v1/clientes/{id}` | 34 | 6 ms | 18 ms | 23 ms | 34/34 | 0 |
| `GET /api/v1/categorias` | 995 | 5 ms | 17 ms | 4.759 ms | 994/995 | 0 |
| `GET /api/v1/documentos-legales` | 907 | 1 ms | 3 ms | 12 ms | 907/907 | 0 |

**Rutas con p95 sobre 2 s:** `POST /api/v1/mis/solicitudes`, `GET /api/v1/auditoria`.

**Respuestas distintas de las esperadas:**

- 4 × `POST /api/v1/reportes/operativo/exportaciones → 409 conflicto`

## Recursos del equipo durante la medición

- API (.NET): 0.29 núcleos de CPU en promedio, 288 MB de memoria como máximo.
- SQL Server: CPU no disponible (Windows exige permisos de administrador para leerla), 208 MB de memoria como máximo.
- Memoria libre del equipo al terminar: 733 MB.

## Consultas SQL más lentas (modo diagnóstico)

| Tiempo | Consulta |
|---:|---|
| 948 ms | `SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; INSERT INTO [claves_idempotencia] ([ambito], [clave], [estado_http], [fecha], [hash_cuerpo], [id_usuario], [respuesta], [tipo_usuario], [vence]) OUTPUT INSERTED.[id_clave] VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8);` |
| 904 ms | `SELECT COUNT(*) FROM [catalogo] AS [c] INNER JOIN [productos] AS [p] ON [c].[id_producto] = [p].[id_producto] INNER JOIN [categorias] AS [c0] ON [p].[id_categoria] = [c0].[id_categoria] WHERE [c].[visible] = CAST(1 AS bit) AND [p].[activo] = CAST(1 AS bit) AND [c0].[activo] = CAST(1 AS bit)` |
| 884 ms | `SELECT COUNT(*) FROM [catalogo] AS [c] INNER JOIN [productos] AS [p] ON [c].[id_producto] = [p].[id_producto] INNER JOIN [categorias] AS [c0] ON [p].[id_categoria] = [c0].[id_categoria] WHERE [c].[visible] = CAST(1 AS bit) AND [p].[activo] = CAST(1 AS bit) AND [c0].[activo] = CAST(1 AS bit)` |
| 875 ms | `SELECT [p].[id_pedido] AS [IdPedido], [p].[id_solicitud] AS [IdSolicitud], [p].[estado_pedido] AS [EstadoPedido], [p].[estado_pago] AS [EstadoPago], [p].[saldo_pendiente] AS [SaldoPendiente], [p].[fecha_entrega_estimada] AS [FechaEntregaEstimada], [p].[modalidad_entrega] AS [ModalidadEntrega], [p].[eliminado_logico] AS [EliminadoLogico] FROM [pedidos] AS [p]` |
| 852 ms | `SELECT COUNT(*) FROM [auditoria] AS [a]` |
| 814 ms | `SELECT [t].[id_trabajo], [t].[archivo_borrado_en], [t].[bloqueado_hasta], [t].[error], [t].[estado], [t].[fecha_inicio], [t].[fecha_pedido], [t].[fecha_termino], [t].[fecha_vencimiento], [t].[filas], [t].[filtros], [t].[formato], [t].[id_empleado], [t].[intentos], [t].[nombre_archivo], [t].[reporte], [t].[rol_solicitante], [t].[tamano_bytes] FROM [trabajos_e` |
| 811 ms | `SELECT COALESCE(SUM(CAST([p].[saldo_pendiente] AS bigint)), CAST(0 AS bigint)) FROM [pedidos] AS [p] INNER JOIN [solicitudes] AS [s] ON [p].[id_solicitud] = [s].[id_solicitud] INNER JOIN [clientes] AS [c] ON [s].[id_cliente] = [c].[id_cliente] WHERE [p].[eliminado_logico] = CAST(0 AS bit) AND ([p].[id_pedido] = @numero OR [c].[nombre] + N' ' + [c].[apellido]` |
| 788 ms | `SELECT COUNT(*) FROM [solicitudes] AS [s] WHERE [s].[id_cliente] = @idCliente AND [s].[eliminado_logico] = CAST(0 AS bit)` |
| 731 ms | `SELECT [s].[id_producto], [s].[id_categoria], [s].[id_categoria0], [s].[nombre], [s].[activo], [s].[nombre0], [s].[descripcion], [s].[precio_base_ref], [s].[activo0], [s].[row_version], [s].[ancho_min_mm], [s].[ancho_max_mm], [s].[alto_min_mm], [s].[alto_max_mm], [p0].[ancho_mm], [p0].[alto_mm], [p0].[id_medida], [p1].[grupo], [p1].[valor], [p1].[id_opcion] ` |
| 675 ms | `SELECT COUNT(*) FROM [solicitudes] AS [s] WHERE [s].[id_cliente] = @idCliente AND [s].[eliminado_logico] = CAST(0 AS bit)` |
| 674 ms | `SELECT [p].[id_pedido] AS [IdPedido], [p].[id_solicitud] AS [IdSolicitud], [p].[estado_pedido] AS [EstadoPedido], [p].[estado_pago] AS [EstadoPago], [p].[saldo_pendiente] AS [SaldoPendiente], [p].[fecha_entrega_estimada] AS [FechaEntregaEstimada], [p].[modalidad_entrega] AS [ModalidadEntrega], [p].[eliminado_logico] AS [EliminadoLogico] FROM [pedidos] AS [p]` |
| 671 ms | `SELECT [p0].[id_pedido], [p0].[origen], [p0].[fecha_pedido], [s].[id_cliente], [c].[nombre], [c].[apellido], [c].[telefono], [p0].[id_empleado_responsable], [e].[nombre] + N' ' + [e].[apellido], [p0].[fecha_entrega_estimada], [p0].[fecha_entrega_real], [p0].[modalidad_entrega], [p0].[estado_pedido], [p0].[estado_pago], [p0].[monto_total_final], [p0].[monto_a` |

## Notas

- Había solo 784 MB de RAM libres al empezar: SQL Server Express trabajó con muy poca memoria, así que estas cifras son más pesimistas que las de un equipo desocupado.
- Los límites de tasa estaban aflojados: esta corrida aísla el rendimiento, no es el comportamiento real de la API.
- Modo diagnóstico: la API registró cada consulta de EF Core, lo que la hace algo más lenta.
