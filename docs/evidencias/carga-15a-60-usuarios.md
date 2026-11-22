# Prueba de carga — fase 15A (RNFP01, RNFO13)

Fecha: 21-11-2026 · Guion: `scripts/carga/carga.mjs` · Resultado frente a la meta (el 95,0 % de las respuestas en 2 s o menos): **CUMPLE**

## Qué se midió

- **60 usuarios simultáneos** (24 visitantes, 18 clientes con cuenta y 18 personas del taller), cada uno con su propia dirección de origen; el personal comparte una, como en la oficina.
- Duración medida: 90 s (más 15 s de calentamiento que no se cuentan). Pausa entre pantallas: 0.1 a 0.4 s por persona.
- Límites de tasa de la API: **aflojados (solo para aislar el rendimiento; no es el comportamiento real)**.
- Datos sintéticos (nada real ni personal): 2.000 clientes, 6.000 solicitudes, unos 2.400 pedidos con sus abonos, 40.000 filas de bitácora y 60 publicaciones, en una **base temporal** que se borró al terminar. Nunca la base real ni la demo.
- Equipo: Windows_NT 10.0.26200, 11th Gen Intel(R) Core(TM) i5-11300H @ 3.10GHz (8 núcleos lógicos), 7.883 MB de RAM con 1.033 MB libres al empezar, SQL Server Express en el mismo equipo. **Las cifras dependen de este hardware**: la API, la base de datos y los veinte usuarios virtuales compiten por la misma CPU y memoria.

## Resultado

- Respuestas medidas: **23.559**. Buenas (el código esperado y en 2 s o menos): **23.558 (100,0 %)** → **CUMPLE**.
- Mediana 19 ms · p95 202 ms · p99 1.594 ms · máximo 1.996 ms.
- Límite de tasa (429): 0 · Errores del servidor (5xx): 0 · Sin respuesta: 0 · Respuestas distintas de las esperadas: 1.
- Códigos recibidos: 200 × 23515, 202 × 43, 409 × 1.

### Por tipo de usuario

| Tipo | Respuestas | Mediana | p95 | Buenas |
|---|---:|---:|---:|---:|
| visitante | 10.647 | 13 ms | 99 ms | 100,0 % |
| cliente | 9.314 | 18 ms | 168 ms | 100,0 % |
| personal | 3.598 | 76 ms | 475 ms | 100,0 % |

### Por ruta (de la más lenta a la más rápida, por p95)

| Ruta | Respuestas | Mediana | p95 | Máximo | Buenas | Inesperadas |
|---|---:|---:|---:|---:|---:|---:|
| `GET /api/v1/auditoria?q=` | 17 | 92 ms | 1.996 ms | 1.996 ms | 17/17 | 0 |
| `POST /api/v1/auth/login` | 11 | 218 ms | 1.846 ms | 1.846 ms | 11/11 | 0 |
| `GET /api/v1/pedidos?q=` | 56 | 198 ms | 1.779 ms | 1.866 ms | 56/56 | 0 |
| `GET /api/v1/indicadores/resumen` | 169 | 281 ms | 1.691 ms | 1.980 ms | 169/169 | 0 |
| `POST /api/v1/auth/login-cliente` | 102 | 228 ms | 1.681 ms | 1.785 ms | 102/102 | 0 |
| `GET /api/v1/pedidos?anio=&mes=` | 72 | 85 ms | 1.639 ms | 1.719 ms | 72/72 | 0 |
| `GET /api/v1/indicadores/evolucion` | 171 | 125 ms | 1.622 ms | 1.776 ms | 171/171 | 0 |
| `GET /api/v1/indicadores/demanda` | 170 | 203 ms | 1.621 ms | 1.977 ms | 170/170 | 0 |
| `GET /api/v1/reportes/operativo` | 167 | 178 ms | 1.595 ms | 1.870 ms | 167/167 | 0 |
| `GET /api/v1/indicadores/por-cobrar` | 171 | 116 ms | 1.594 ms | 1.775 ms | 171/171 | 0 |
| `GET /api/v1/pedidos` | 66 | 102 ms | 1.543 ms | 1.693 ms | 66/66 | 0 |
| `GET /api/v1/reportes/gerencial` | 30 | 315 ms | 728 ms | 1.797 ms | 30/30 | 0 |
| `GET /api/v1/solicitudes/{id}` | 159 | 98 ms | 603 ms | 1.790 ms | 159/159 | 0 |
| `GET /api/v1/pedidos/{id}` | 161 | 102 ms | 509 ms | 1.908 ms | 161/161 | 0 |
| `GET /api/v1/pedidos?estadoPedido=` | 71 | 132 ms | 485 ms | 1.539 ms | 71/71 | 0 |
| `GET /api/v1/solicitudes?q=` | 105 | 97 ms | 439 ms | 1.814 ms | 105/105 | 0 |
| `GET /api/v1/solicitudes` | 106 | 75 ms | 418 ms | 1.772 ms | 106/106 | 0 |
| `GET /api/v1/pedidos?cobranza=` | 76 | 151 ms | 403 ms | 1.659 ms | 76/76 | 0 |
| `GET /api/v1/mis/solicitudes/{id}` | 521 | 75 ms | 400 ms | 1.793 ms | 521/521 | 0 |
| `GET /api/v1/indicadores/cobranza` | 171 | 47 ms | 305 ms | 1.689 ms | 171/171 | 0 |
| `POST /api/v1/reportes/operativo/exportaciones` | 44 | 95 ms | 286 ms | 1.644 ms | 43/44 | 1 |
| `GET /api/v1/reportes/exportaciones/{id}` | 43 | 27 ms | 264 ms | 320 ms | 43/43 | 0 |
| `GET /api/v1/reportes/parametrizable` | 25 | 63 ms | 251 ms | 401 ms | 25/25 | 0 |
| `GET /api/v1/indicadores/embudo` | 171 | 85 ms | 249 ms | 1.691 ms | 171/171 | 0 |
| `GET /api/v1/solicitudes?estado=` | 99 | 79 ms | 239 ms | 1.686 ms | 99/99 | 0 |
| `GET /api/v1/mis/solicitudes` | 1626 | 45 ms | 223 ms | 1.852 ms | 1626/1626 | 0 |
| `GET /api/v1/solicitudes?origen=` | 102 | 79 ms | 213 ms | 1.812 ms | 102/102 | 0 |
| `GET /api/v1/catalogo?categoria=` | 782 | 41 ms | 203 ms | 1.766 ms | 782/782 | 0 |
| `GET /api/v1/pedidos/calendario` | 88 | 35 ms | 187 ms | 1.692 ms | 88/88 | 0 |
| `GET /api/v1/auditoria` | 16 | 39 ms | 184 ms | 184 ms | 16/16 | 0 |
| `GET /api/v1/clientes?q=&tamano=10` | 795 | 30 ms | 153 ms | 1.756 ms | 795/795 | 0 |
| `GET /api/v1/auditoria?entidad=` | 23 | 39 ms | 146 ms | 284 ms | 23/23 | 0 |
| `GET /api/v1/productos` | 839 | 32 ms | 137 ms | 1.767 ms | 839/839 | 0 |
| `GET /api/v1/catalogo (vitrina completa)` | 1861 | 20 ms | 128 ms | 1.753 ms | 1861/1861 | 0 |
| `GET /api/v1/catalogo (portada)` | 2493 | 17 ms | 121 ms | 1.753 ms | 2493/2493 | 0 |
| `GET /api/v1/mi-cuenta/` | 1628 | 26 ms | 118 ms | 1.773 ms | 1628/1628 | 0 |
| `GET /api/v1/clientes` | 125 | 27 ms | 113 ms | 1.603 ms | 125/125 | 0 |
| `GET /api/v1/catalogo/{id}` | 795 | 16 ms | 84 ms | 1.698 ms | 795/795 | 0 |
| `GET /api/v1/mis/pedidos` | 1628 | 12 ms | 75 ms | 1.693 ms | 1628/1628 | 0 |
| `GET /api/v1/clientes/{id}` | 94 | 15 ms | 69 ms | 1.605 ms | 94/94 | 0 |
| `GET /api/v1/auditoria?desde=&hasta=` | 24 | 31 ms | 54 ms | 71 ms | 24/24 | 0 |
| `GET /api/v1/categorias` | 2700 | 11 ms | 47 ms | 1.752 ms | 2700/2700 | 0 |
| `GET /api/v1/local/estado` | 2493 | 12 ms | 44 ms | 1.693 ms | 2493/2493 | 0 |
| `GET /api/v1/documentos-legales` | 2493 | 1 ms | 6 ms | 1.585 ms | 2493/2493 | 0 |

**Respuestas distintas de las esperadas:**

- 1 × `POST /api/v1/reportes/operativo/exportaciones → 409 conflicto`

## Recursos del equipo durante la medición

- API (.NET): 0.71 núcleos de CPU en promedio, 383 MB de memoria como máximo.
- SQL Server: CPU no disponible (Windows exige permisos de administrador para leerla), 232 MB de memoria como máximo.
- Memoria libre del equipo al terminar: 620 MB.

## Notas

- Había solo 1033 MB de RAM libres al empezar: SQL Server Express trabajó con muy poca memoria, así que estas cifras son más pesimistas que las de un equipo desocupado.
- Los límites de tasa estaban aflojados: esta corrida aísla el rendimiento, no es el comportamiento real de la API.
