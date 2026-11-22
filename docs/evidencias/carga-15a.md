# Prueba de carga — fase 15A (RNFP01, RNFO13)

Fecha: 21-11-2026 · Guion: `scripts/carga/carga.mjs` · Resultado frente a la meta (el 95,0 % de las respuestas en 2 s o menos): **CUMPLE**

## Qué se midió

- **20 usuarios simultáneos** (8 visitantes, 6 clientes con cuenta y 6 personas del taller), cada uno con su propia dirección de origen; el personal comparte una, como en la oficina.
- Duración medida: 120 s (más 15 s de calentamiento que no se cuentan). Pausa entre pantallas: 1 a 3 s por persona.
- Límites de tasa de la API: **los reales de producción (sin aflojar nada)**.
- Datos sintéticos (nada real ni personal): 2.000 clientes, 6.000 solicitudes, unos 2.400 pedidos con sus abonos, 40.000 filas de bitácora y 60 publicaciones, en una **base temporal** que se borró al terminar. Nunca la base real ni la demo.
- Equipo: Windows_NT 10.0.26200, 11th Gen Intel(R) Core(TM) i5-11300H @ 3.10GHz (8 núcleos lógicos), 7.883 MB de RAM con 721 MB libres al empezar, SQL Server Express en el mismo equipo. **Las cifras dependen de este hardware**: la API, la base de datos y los veinte usuarios virtuales compiten por la misma CPU y memoria.

## Resultado

- Respuestas medidas: **2.001**. Buenas (el código esperado y en 2 s o menos): **1.996 (99,8 %)** → **CUMPLE**.
- Mediana 21 ms · p95 289 ms · p99 568 ms · máximo 4.450 ms.
- Límite de tasa (429): 0 · Errores del servidor (5xx): 0 · Sin respuesta: 0 · Respuestas distintas de las esperadas: 0.
- Códigos recibidos: 200 × 1991, 201 × 4, 202 × 6.

### Por tipo de usuario

| Tipo | Respuestas | Mediana | p95 | Buenas |
|---|---:|---:|---:|---:|
| visitante | 815 | 15 ms | 217 ms | 100,0 % |
| cliente | 774 | 24 ms | 278 ms | 99,7 % |
| personal | 412 | 46 ms | 383 ms | 99,3 % |

### Por ruta (de la más lenta a la más rápida, por p95)

| Ruta | Respuestas | Mediana | p95 | Máximo | Buenas | Inesperadas |
|---|---:|---:|---:|---:|---:|---:|
| `GET /api/v1/indicadores/demanda` | 18 | 146 ms | 4.450 ms | 4.450 ms | 17/18 | 0 |
| `GET /api/v1/indicadores/embudo` | 18 | 55 ms | 4.450 ms | 4.450 ms | 17/18 | 0 |
| `GET /api/v1/solicitudes?q=` | 17 | 53 ms | 4.009 ms | 4.009 ms | 16/17 | 0 |
| `POST /api/v1/auth/login-cliente` | 8 | 197 ms | 2.133 ms | 2.133 ms | 7/8 | 0 |
| `GET /api/v1/indicadores/resumen` | 18 | 155 ms | 725 ms | 725 ms | 18/18 | 0 |
| `GET /api/v1/pedidos?q=` | 7 | 137 ms | 718 ms | 718 ms | 7/7 | 0 |
| `GET /api/v1/indicadores/evolucion` | 18 | 88 ms | 694 ms | 694 ms | 18/18 | 0 |
| `GET /api/v1/indicadores/por-cobrar` | 18 | 78 ms | 611 ms | 611 ms | 18/18 | 0 |
| `GET /api/v1/solicitudes/{id}` | 17 | 58 ms | 582 ms | 582 ms | 17/17 | 0 |
| `GET /api/v1/pedidos/{id}` | 18 | 53 ms | 524 ms | 524 ms | 18/18 | 0 |
| `GET /api/v1/clientes` | 23 | 13 ms | 521 ms | 528 ms | 23/23 | 0 |
| `GET /api/v1/auditoria?q=` | 2 | 40 ms | 518 ms | 518 ms | 2/2 | 0 |
| `GET /api/v1/indicadores/cobranza` | 18 | 34 ms | 517 ms | 517 ms | 18/18 | 0 |
| `POST /api/v1/reportes/operativo/exportaciones` | 6 | 43 ms | 499 ms | 499 ms | 6/6 | 0 |
| `GET /api/v1/reportes/operativo` | 17 | 76 ms | 498 ms | 498 ms | 17/17 | 0 |
| `GET /api/v1/pedidos?anio=&mes=` | 8 | 43 ms | 474 ms | 474 ms | 8/8 | 0 |
| `GET /api/v1/clientes/{id}` | 10 | 8 ms | 449 ms | 449 ms | 10/10 | 0 |
| `GET /api/v1/pedidos` | 12 | 46 ms | 383 ms | 383 ms | 12/12 | 0 |
| `GET /api/v1/solicitudes?origen=` | 11 | 35 ms | 381 ms | 381 ms | 11/11 | 0 |
| `GET /api/v1/catalogo?categoria=` | 56 | 36 ms | 376 ms | 586 ms | 56/56 | 0 |
| `GET /api/v1/reportes/parametrizable` | 7 | 30 ms | 373 ms | 373 ms | 7/7 | 0 |
| `GET /api/v1/mis/solicitudes` | 151 | 45 ms | 365 ms | 2.147 ms | 150/151 | 0 |
| `GET /api/v1/solicitudes` | 13 | 68 ms | 346 ms | 346 ms | 13/13 | 0 |
| `GET /api/v1/mis/solicitudes/{id}` | 41 | 39 ms | 318 ms | 667 ms | 41/41 | 0 |
| `GET /api/v1/reportes/gerencial` | 2 | 264 ms | 301 ms | 301 ms | 2/2 | 0 |
| `GET /api/v1/catalogo (portada)` | 182 | 20 ms | 300 ms | 568 ms | 182/182 | 0 |
| `GET /api/v1/catalogo (vitrina completa)` | 143 | 23 ms | 289 ms | 1.754 ms | 143/143 | 0 |
| `GET /api/v1/productos` | 72 | 16 ms | 288 ms | 548 ms | 72/72 | 0 |
| `GET /api/v1/mi-cuenta/` | 151 | 25 ms | 278 ms | 546 ms | 151/151 | 0 |
| `POST /api/v1/auth/login` | 1 | 270 ms | 270 ms | 270 ms | 1/1 | 0 |
| `GET /api/v1/catalogo/{id}` | 51 | 21 ms | 217 ms | 337 ms | 51/51 | 0 |
| `GET /api/v1/auditoria?desde=&hasta=` | 2 | 16 ms | 215 ms | 215 ms | 2/2 | 0 |
| `GET /api/v1/pedidos?estadoPedido=` | 9 | 40 ms | 204 ms | 204 ms | 9/9 | 0 |
| `POST /api/v1/mis/solicitudes` | 4 | 137 ms | 196 ms | 196 ms | 4/4 | 0 |
| `GET /api/v1/mis/pedidos` | 151 | 16 ms | 171 ms | 540 ms | 151/151 | 0 |
| `GET /api/v1/local/estado` | 182 | 7 ms | 165 ms | 505 ms | 182/182 | 0 |
| `GET /api/v1/pedidos/calendario` | 13 | 47 ms | 163 ms | 163 ms | 13/13 | 0 |
| `GET /api/v1/clientes?q=&tamano=10` | 86 | 16 ms | 156 ms | 271 ms | 86/86 | 0 |
| `GET /api/v1/reportes/exportaciones/{id}` | 5 | 8 ms | 100 ms | 100 ms | 5/5 | 0 |
| `GET /api/v1/solicitudes?estado=` | 8 | 29 ms | 84 ms | 84 ms | 8/8 | 0 |
| `GET /api/v1/categorias` | 215 | 9 ms | 72 ms | 587 ms | 215/215 | 0 |
| `GET /api/v1/pedidos?cobranza=` | 6 | 39 ms | 57 ms | 57 ms | 6/6 | 0 |
| `GET /api/v1/auditoria?entidad=` | 2 | 19 ms | 30 ms | 30 ms | 2/2 | 0 |
| `GET /api/v1/auditoria` | 2 | 22 ms | 24 ms | 24 ms | 2/2 | 0 |
| `GET /api/v1/documentos-legales` | 182 | 1 ms | 7 ms | 27 ms | 182/182 | 0 |

**Rutas con p95 sobre 2 s:** `GET /api/v1/indicadores/demanda`, `GET /api/v1/indicadores/embudo`, `GET /api/v1/solicitudes?q=`, `POST /api/v1/auth/login-cliente`.

## Recursos del equipo durante la medición

- API (.NET): 0.07 núcleos de CPU en promedio, 288 MB de memoria como máximo.
- SQL Server: CPU no disponible (Windows exige permisos de administrador para leerla), 195 MB de memoria como máximo.
- Memoria libre del equipo al terminar: 685 MB.

## Notas

- Había solo 721 MB de RAM libres al empezar: SQL Server Express trabajó con muy poca memoria, así que estas cifras son más pesimistas que las de un equipo desocupado.
