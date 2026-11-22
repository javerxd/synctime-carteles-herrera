# Prueba de carga de SyncTime (fase 15A)

Guion propio, en **Node.js nativo y sin paquetes**, que mide si la API cumple el requisito RNFP01: **el 95 % de las respuestas en 2 segundos o menos con 20 usuarios simultáneos**. Escribe su informe en `docs/evidencias/carga-15a.md`.

## Cómo se corre

```powershell
node scripts/carga/carga.mjs
```

Todo se hace solo, de principio a fin:

1. Construye la API en Release en una **carpeta temporal** (no pisa los archivos de una API que tengas encendida).
2. La levanta en un puerto libre contra una **base nueva y temporal** (`SyncTime_Pruebas_carga_xxxxxxxx`). Las migraciones crean el esquema.
3. Crea las cuentas de prueba por la API y siembra **datos sintéticos** con `sqlcmd` (`datos-sinteticos.sql`).
4. Lanza los usuarios virtuales, mide y escribe el informe.
5. Apaga la API y **borra la base y la carpeta temporales**, aunque algo falle o canceles con Ctrl+C.

**No usa la base real ni la demo**, ni ningún dato personal: todo es inventado (teléfonos `7…`, correos `@carga.test`). Las claves de las cuentas de prueba se generan al azar en cada corrida (o salen de `SYNCTIME_CARGA_CLAVE` si la defines), valen solo para la base temporal y no se escriben en ningún archivo ni en el informe. La API de la prueba lleva sus propias variables de entorno, que pisan los *User Secrets* del equipo: ni el correo real ni el ID de cliente de Google llegan a ella.

Antes de lanzarlo: **cierra navegadores, el IDE y Discord**. SQL Server Express usa la memoria que encuentra libre; con poca, las cifras salen peores que las reales (el guion avisa y lo anota en el informe).

## Qué mide

- **20 usuarios** repartidos en 8 visitantes (portada, vitrina, detalle, formulario), 6 clientes con cuenta (ingreso, Mi cuenta, sus solicitudes, envío de una solicitud en línea) y 6 personas del taller (bandeja de solicitudes con filtros y búsqueda, pedidos, calendario, búsqueda de clientes escribiendo, indicadores, reportes, exportación y auditoría, según su rol).
- Cada usuario tiene **su propia dirección de origen** (`127.0.1.N`), porque los límites de tasa de la API cuentan por IP y veinte personas reales no comparten una. El personal comparte una (`127.0.0.2`), como en la oficina. Con los **límites reales**, la prueba también comprueba que el uso normal no recibe `429` falsos.
- Cuenta como **respuesta buena** la que trae el código esperado y llega en 2 s o menos: un `500` o un `429` rápido no cuenta. Reporta mediana, p95, p99 y máximo por ruta y por tipo de usuario, los códigos recibidos, y el uso de CPU y memoria de la API y de SQL Server.

## Opciones útiles

| Opción | Para qué |
|---|---|
| `--limites holgados` | Sube los límites de tasa para **aislar el rendimiento** (no es el comportamiento real). |
| `--pensar 100,400` | Pausa mínima entre pantallas: un **estrés** de varias veces el uso normal. |
| `--diagnostico` | Registra las consultas de EF Core y lista las **más lentas** (la causa medida de una ruta lenta). |
| `--usuarios N`, `--duracion S` | Más o menos gente, más o menos tiempo. |
| `--datos-clientes N` … | Volumen de los datos sintéticos. |
| `--conservar` | No borra la base temporal (para mirarla con SSMS). Bórrala después. |
| `--ayuda` | La lista completa. |

Código de salida: `0` si se cumple la meta, `2` si no, `1` si la prueba no pudo correr.

## Pruebas del propio guion

```powershell
node --test "scripts/carga/*.test.mjs"
```

Cubren el cálculo de percentiles y de la meta, el informe y las opciones (sin red, sin reloj, sin base de datos).

## Limitaciones que hay que tener presentes

- Mide **este equipo** (Windows, SQL Server Express, API y usuarios virtuales compartiendo CPU y memoria). La prueba en la nube de la fase 16 dará otras cifras.
- Las fotos de la vitrina no se siembran (viven en disco, no en la base): la ruta de imágenes no entra en la mezcla.
- El correo va a la bandeja local (`Correo:Modo = Bandeja`); no se envía ninguno de verdad.
