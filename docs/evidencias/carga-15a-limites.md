# Prueba de carga — fase 15A (RNFP01, RNFO13)

Fecha: 21-11-2026 · Guion: `scripts/carga/carga.mjs` · Resultado frente a la meta (el 95,0 % de las respuestas en 2 s o menos): **NO CUMPLE**

## Qué se midió

- **20 usuarios simultáneos** (8 visitantes, 6 clientes con cuenta y 6 personas del taller), cada uno con su propia dirección de origen; el personal comparte una, como en la oficina.
- Duración medida: 90 s (más 15 s de calentamiento que no se cuentan). Pausa entre pantallas: 0.1 a 0.4 s por persona.
- Límites de tasa de la API: **los reales de producción (sin aflojar nada)**.
- Datos sintéticos (nada real ni personal): 2.000 clientes, 6.000 solicitudes, unos 2.400 pedidos con sus abonos, 40.000 filas de bitácora y 60 publicaciones, en una **base temporal** que se borró al terminar. Nunca la base real ni la demo.
- Equipo: Windows_NT 10.0.26200, 11th Gen Intel(R) Core(TM) i5-11300H @ 3.10GHz (8 núcleos lógicos), 7.883 MB de RAM con 856 MB libres al empezar, SQL Server Express en el mismo equipo. **Las cifras dependen de este hardware**: la API, la base de datos y los veinte usuarios virtuales compiten por la misma CPU y memoria.

## Resultado

- Respuestas medidas: **10.265**. Buenas (el código esperado y en 2 s o menos): **3.740 (36,4 %)** → **NO CUMPLE**.
- Mediana 1 ms · p95 41 ms · p99 109 ms · máximo 1.603 ms.
- Límite de tasa (429): 6520 · Errores del servidor (5xx): 0 · Sin respuesta: 0 · Respuestas distintas de las esperadas: 6525.
- Códigos recibidos: 200 × 3726, 202 × 14, 409 × 5, 429 × 6520.

### Por tipo de usuario

| Tipo | Respuestas | Mediana | p95 | Buenas |
|---|---:|---:|---:|---:|
| visitante | 4.672 | 1 ms | 16 ms | 19,0 % |
| cliente | 4.080 | 1 ms | 35 ms | 44,0 % |
| personal | 1.513 | 17 ms | 86 ms | 69,7 % |

### Por ruta (de la más lenta a la más rápida, por p95)

| Ruta | Respuestas | Mediana | p95 | Máximo | Buenas | Inesperadas |
|---|---:|---:|---:|---:|---:|---:|
| `GET /api/v1/auditoria?desde=&hasta=` | 7 | 11 ms | 1.442 ms | 1.442 ms | 5/7 | 2 |
| `GET /api/v1/reportes/gerencial` | 16 | 115 ms | 171 ms | 171 ms | 16/16 | 0 |
| `POST /api/v1/auth/login` | 4 | 149 ms | 168 ms | 168 ms | 4/4 | 0 |
| `POST /api/v1/auth/login-cliente` | 42 | 154 ms | 166 ms | 1.483 ms | 42/42 | 0 |
| `GET /api/v1/indicadores/resumen` | 69 | 40 ms | 147 ms | 160 ms | 35/69 | 34 |
| `GET /api/v1/pedidos?q=` | 23 | 50 ms | 111 ms | 1.588 ms | 15/23 | 8 |
| `GET /api/v1/indicadores/demanda` | 69 | 31 ms | 109 ms | 137 ms | 35/69 | 34 |
| `GET /api/v1/reportes/operativo` | 65 | 66 ms | 93 ms | 109 ms | 65/65 | 0 |
| `GET /api/v1/pedidos?cobranza=` | 33 | 35 ms | 88 ms | 1.443 ms | 23/33 | 10 |
| `GET /api/v1/indicadores/evolucion` | 69 | 14 ms | 86 ms | 138 ms | 35/69 | 34 |
| `GET /api/v1/solicitudes/{id}` | 52 | 38 ms | 83 ms | 89 ms | 52/52 | 0 |
| `GET /api/v1/mis/solicitudes/{id}` | 85 | 31 ms | 82 ms | 1.489 ms | 79/85 | 6 |
| `GET /api/v1/indicadores/por-cobrar` | 69 | 26 ms | 71 ms | 91 ms | 35/69 | 34 |
| `GET /api/v1/pedidos` | 30 | 26 ms | 66 ms | 69 ms | 18/30 | 12 |
| `GET /api/v1/pedidos/{id}` | 53 | 40 ms | 65 ms | 90 ms | 50/53 | 3 |
| `GET /api/v1/solicitudes?q=` | 48 | 33 ms | 63 ms | 1.579 ms | 37/48 | 11 |
| `POST /api/v1/reportes/operativo/exportaciones` | 19 | 35 ms | 63 ms | 63 ms | 14/19 | 5 |
| `GET /api/v1/solicitudes` | 56 | 23 ms | 62 ms | 1.484 ms | 41/56 | 15 |
| `GET /api/v1/indicadores/embudo` | 69 | 15 ms | 60 ms | 74 ms | 35/69 | 34 |
| `GET /api/v1/pedidos?estadoPedido=` | 39 | 31 ms | 59 ms | 89 ms | 26/39 | 13 |
| `GET /api/v1/solicitudes?origen=` | 50 | 25 ms | 51 ms | 58 ms | 36/50 | 14 |
| `GET /api/v1/pedidos?anio=&mes=` | 28 | 30 ms | 47 ms | 82 ms | 20/28 | 8 |
| `GET /api/v1/mis/solicitudes` | 812 | 1 ms | 47 ms | 1.603 ms | 232/812 | 580 |
| `GET /api/v1/solicitudes?estado=` | 57 | 23 ms | 47 ms | 57 ms | 38/57 | 19 |
| `GET /api/v1/catalogo?categoria=` | 61 | 16 ms | 44 ms | 1.562 ms | 56/61 | 5 |
| `GET /api/v1/auditoria?q=` | 11 | 23 ms | 39 ms | 39 ms | 6/11 | 5 |
| `GET /api/v1/reportes/parametrizable` | 13 | 29 ms | 38 ms | 38 ms | 13/13 | 0 |
| `GET /api/v1/pedidos/calendario` | 35 | 16 ms | 34 ms | 1.449 ms | 30/35 | 5 |
| `GET /api/v1/indicadores/cobranza` | 69 | 11 ms | 33 ms | 43 ms | 35/69 | 34 |
| `GET /api/v1/clientes?q=&tamano=10` | 329 | 13 ms | 31 ms | 72 ms | 240/329 | 89 |
| `GET /api/v1/reportes/exportaciones/{id}` | 14 | 5 ms | 31 ms | 31 ms | 13/14 | 1 |
| `GET /api/v1/auditoria?entidad=` | 10 | 11 ms | 27 ms | 27 ms | 6/10 | 4 |
| `GET /api/v1/productos` | 321 | 1 ms | 26 ms | 63 ms | 70/321 | 251 |
| `GET /api/v1/catalogo/{id}` | 72 | 8 ms | 25 ms | 66 ms | 65/72 | 7 |
| `GET /api/v1/catalogo (portada)` | 1016 | 1 ms | 25 ms | 1.467 ms | 367/1016 | 649 |
| `GET /api/v1/auditoria` | 7 | 12 ms | 25 ms | 25 ms | 4/7 | 3 |
| `GET /api/v1/clientes` | 73 | 8 ms | 23 ms | 35 ms | 50/73 | 23 |
| `GET /api/v1/catalogo (vitrina completa)` | 1183 | 1 ms | 23 ms | 1.489 ms | 254/1183 | 929 |
| `GET /api/v1/mi-cuenta/` | 812 | 1 ms | 22 ms | 1.432 ms | 232/812 | 580 |
| `GET /api/v1/mis/pedidos` | 812 | 1 ms | 15 ms | 1.432 ms | 229/812 | 583 |
| `GET /api/v1/clientes/{id}` | 27 | 6 ms | 12 ms | 16 ms | 23/27 | 4 |
| `GET /api/v1/local/estado` | 1016 | 1 ms | 10 ms | 1.467 ms | 369/1016 | 647 |
| `GET /api/v1/categorias` | 1504 | 1 ms | 8 ms | 1.453 ms | 324/1504 | 1180 |
| `GET /api/v1/documentos-legales` | 1016 | 1 ms | 3 ms | 1.467 ms | 366/1016 | 650 |

**Respuestas distintas de las esperadas:**

- 1180 × `GET /api/v1/categorias → 429 demasiados_intentos`
- 929 × `GET /api/v1/catalogo (vitrina completa) → 429 demasiados_intentos`
- 650 × `GET /api/v1/documentos-legales → 429 demasiados_intentos`
- 649 × `GET /api/v1/catalogo (portada) → 429 demasiados_intentos`
- 647 × `GET /api/v1/local/estado → 429 demasiados_intentos`
- 583 × `GET /api/v1/mis/pedidos → 429 demasiados_intentos`
- 580 × `GET /api/v1/mi-cuenta/ → 429 demasiados_intentos`
- 580 × `GET /api/v1/mis/solicitudes → 429 demasiados_intentos`
- 251 × `GET /api/v1/productos → 429 demasiados_intentos`
- 89 × `GET /api/v1/clientes?q=&tamano=10 → 429 demasiados_intentos`
- 34 × `GET /api/v1/indicadores/embudo → 429 demasiados_intentos`
- 34 × `GET /api/v1/indicadores/evolucion → 429 demasiados_intentos`
- 34 × `GET /api/v1/indicadores/resumen → 429 demasiados_intentos`
- 34 × `GET /api/v1/indicadores/cobranza → 429 demasiados_intentos`
- 34 × `GET /api/v1/indicadores/por-cobrar → 429 demasiados_intentos`
- 34 × `GET /api/v1/indicadores/demanda → 429 demasiados_intentos`
- 23 × `GET /api/v1/clientes → 429 demasiados_intentos`
- 19 × `GET /api/v1/solicitudes?estado= → 429 demasiados_intentos`
- 15 × `GET /api/v1/solicitudes → 429 demasiados_intentos`
- 14 × `GET /api/v1/solicitudes?origen= → 429 demasiados_intentos`
- 13 × `GET /api/v1/pedidos?estadoPedido= → 429 demasiados_intentos`
- 12 × `GET /api/v1/pedidos → 429 demasiados_intentos`
- 11 × `GET /api/v1/solicitudes?q= → 429 demasiados_intentos`
- 10 × `GET /api/v1/pedidos?cobranza= → 429 demasiados_intentos`
- 8 × `GET /api/v1/pedidos?anio=&mes= → 429 demasiados_intentos`
- 8 × `GET /api/v1/pedidos?q= → 429 demasiados_intentos`
- 7 × `GET /api/v1/catalogo/{id} → 429 demasiados_intentos`
- 6 × `GET /api/v1/mis/solicitudes/{id} → 429 demasiados_intentos`
- 5 × `GET /api/v1/catalogo?categoria= → 429 demasiados_intentos`
- 5 × `GET /api/v1/pedidos/calendario → 429 demasiados_intentos`
- 5 × `GET /api/v1/auditoria?q= → 429 demasiados_intentos`
- 5 × `POST /api/v1/reportes/operativo/exportaciones → 409 conflicto`
- 4 × `GET /api/v1/clientes/{id} → 429 demasiados_intentos`
- 4 × `GET /api/v1/auditoria?entidad= → 429 demasiados_intentos`
- 3 × `GET /api/v1/pedidos/{id} → 429 demasiados_intentos`
- 3 × `GET /api/v1/auditoria → 429 demasiados_intentos`
- 2 × `GET /api/v1/auditoria?desde=&hasta= → 429 demasiados_intentos`
- 1 × `GET /api/v1/reportes/exportaciones/{id} → 429 demasiados_intentos`

## Recursos del equipo durante la medición

- API (.NET): 0.20 núcleos de CPU en promedio, 288 MB de memoria como máximo.
- SQL Server: CPU no disponible (Windows exige permisos de administrador para leerla), 213 MB de memoria como máximo.
- Memoria libre del equipo al terminar: 831 MB.

## Notas

- Había solo 856 MB de RAM libres al empezar: SQL Server Express trabajó con muy poca memoria, así que estas cifras son más pesimistas que las de un equipo desocupado.
