# Informe de la fase 16C-1 — Código y archivos para la nube gratuita

Fecha: 22-11-2026 · Rama `fase-16c-nube-1` (nace de `main`, commit `f8e4999`) · Estado: **APROBADA por el usuario el 22-11-2026** (visto bueno formal tras la CI en verde). No existe ningún recurso en la nube.

Requisitos tocados: **RNFP04** (límite documentado de disponibilidad), **RNFP09 y RNFP20** (la base se prepara; respaldo y restauración son de la 16C-2), **RNFE03** (16C-3), **RNFO15/RNFO16** (CI), **RNFP29** (CORS sin cambios).

## 1. Qué se hizo

| Pieza | Resultado |
|---|---|
| **Almacén de archivos** (H12) | `IAlmacenArchivos` con `AlmacenEnDisco` y `AlmacenEnBlob` (contenedor privado `archivos`, prefijos `adjuntos/`, `imagenes/`, `exportaciones/`). Las tres clases de nombres quedan como fachadas; las 4 rutas de descarga pasan de `Results.File(ruta)` a flujos; las purgas usan la fecha del objeto. `Almacen__Modo` (Disco o Blob) es obligatorio fuera de desarrollo y se valida al arrancar, incluida la cadena de Blob. Un contenedor público se rechaza. Único paquete nuevo: `Azure.Storage.Blobs` 12.30.2. |
| **Correo por HTTPS** (H11, D8) | `EnviadorCorreoApi` (Brevo) detrás de `IEnviadorCorreo`, con `Correo__Modo=Api`: clave solo en cabecera, sin seguir redirecciones, solo https, errores clasificados por código HTTP sin guardar el cuerpo. Hallazgos de la documentación de Brevo: sin `cid:` (el logo va por URL pública o se quita) y sin autenticación de remitentes `@gmail.com` (riesgo de spam, plan B ACS Email). |
| **Arranque con la base en pausa** (H14) | Migraciones pendientes, destinatarios y primer administrador corren en segundo plano fuera de desarrollo, con espera creciente hasta 30 minutos; credenciales rechazadas o base inexistente **no** se reintentan. La configuración inválida del administrador sigue impidiendo arrancar. `/salud` responde de inmediato y `/salud/lista` la despierta. |
| **Sitio «despertando»** | El cliente repite las lecturas ante 502/503/504 sin formato de error de la API (~66 s) y muestra «El sistema está despertando» (región viva, `aria-hidden` en la copia visual, sin animación con «reducir movimiento»). Los envíos no se repiten. |
| **`render.yaml` gratuito** | `plan: free`, sin disco, `Almacen__Modo=Blob`, `Correo__Modo=Api`, secretos nuevos con `sync: false`; se quitaron los de SMTP. `compose.yaml` declara `Almacen__Modo: Disco`. |
| **Validadores y CI** | `validar-contenedores.py` (plan, disco, modos, secretos; 10 pruebas nuevas, 43 en total), `validar-workflows.py` (Azurite fijo y sin omisión; 3 nuevas, 60 en total). `ci.yml` levanta **Azurite 3.38.0** antes de las unitarias. Pisos: 874 / 785 / 998. |
| **Documentos** | `docs/despliegue.md` (sección 7), `docs/plan/desviaciones.md` (sección 16C-1). |

## 2. Resultados reales (este equipo)

| Verificación | Resultado |
|---|---|
| Unitarias (.NET) | **863 pasan y 11 omitidas** (las de Blob contra Azurite, con `SYNCTIME_OMITIR_BLOB=1`); en la CI deben ser 874 de 874 |
| Integración contra `localhost\SQLEXPRESS` (bases temporales) | **785 de 785** (17 nuevas en `Fase16c1Tests`) |
| Vitest | **998 de 998** (17 nuevas en `fase16cDespertar.test.jsx`); una ejecución intermedia falló una vez en un archivo y no se repitió en dos ejecuciones siguientes (prueba intermitente conocida de la 16B) |
| Lint / build del frontend | sin errores nuevos (avisos previos) / build correcto, presupuesto de peso en verde |
| `validar-contenedores.py`, `validar-workflows.py` y sus pruebas | **43 / 60 de 60, en verde** (con PyYAML en un entorno temporal) |

**No verificable en este equipo (no hay Docker), verificado en la CI:** las pruebas de Blob contra Azurite 3.38.0 y `docker compose up` con el cambio.

## 2b. Resultado en la CI de GitHub (rama `fase-16c-nube-1`)

- **Primera ejecución (commit `2bd7ffd`):** en verde `estática`, `backend` (874 unitarias, **con las 11 pruebas de Blob contra Azurite**, y 785 de integración), `imágenes` y `compose`. En rojo, con causa propia y ninguna del código de la fase: (1) `secretos`: gitleaks marcó la cadena de conexión inventada de `AlmacenArchivosTests.cs` (falso positivo); (2) `fallo-tzdata`: el guion no pasaba `Almacen__Modo`, que la API ahora exige, y moría por eso (código 139) en vez de por la zona horaria; (3) `frontend`: una sola prueba, la intermitente conocida del foco de `cotizaciones.test.jsx`.
- **Corrección (commit `9b87fe7`):** excepción acotada en `.gitleaks.toml` (regla `generic-api-key`, ese archivo, con su motivo), `Almacen__Modo=Disco` en `fallos-a-proposito.sh` y más margen a la prueba del foco (20 s y 60 s).
- **Segunda ejecución (commit `9b87fe7`): los siete jobs en verde** y `publicar` omitido (correcto en una rama). Con esto queda **confirmado en un entorno real** el uso del SDK de Azure.Storage.Blobs contra el emulador y que la API arranca en compose con `Almacen__Modo: Disco`.
- Sigue sin verificarse, a propósito, contra la nube real: es trabajo de la 16C-2 (Azure) y la 16C-3.

## 3. Fallos a propósito (hechos)

Almacén: nombre con `../`, área desconocida, contenedor público (se rechaza y no guarda nada), reinicio del proceso (el adjunto sobrevive: instancia nueva lee lo guardado), nube caída (error sin repetir la cadena), subida cancelada. Correo: 401/403/429/5xx/400, red caída, proveedor que no responde, clave en el error. Arranque: base inalcanzable (la API levanta y `/salud` responde, `/salud/lista` da 503), credencial rechazada sin reintento, configuración inválida que impide arrancar, cadena de Blob mal escrita, modo sin declarar. Frontend: 502/503/504 que despiertan, error de la API que no es «dormido», envíos que no se repiten, cancelación. Validadores: disco en Render, plan de pago, SMTP, secretos escritos, Azurite ausente u omitido. *Los que requieren la CI:* Azurite real.

## 4. Revisiones de ECC

`csharp-reviewer`, `security-reviewer`, `react-reviewer`: **ningún crítico**. Corregidos: reintento de credenciales rechazadas, texto de errores en el registro crítico, cadena de Blob mal formada al arrancar, clave con salto de línea y remitente sin nombre, señal ya cancelada en la espera del cliente, posición y capa del aviso. Aceptados y anotados en `desviaciones.md` (renovar sesión no espera, deduplicación sin cancelación, 400 de Brevo, borrados sin plazo propio, etiqueta de Azurite sin resumen). `database-reviewer` no aplica (sin migraciones). `docker-patterns`: compose declara el modo; `entrada.sh` funciona sin disco.

## 5. Lo que necesito de ti

1. ~~Subir la rama y revisar la CI~~ — hecho: verde en `9b87fe7`.
2. **Aprobación formal (22-11-2026):** el usuario aprueba la 16C-1. Queda por hacer, antes de la sesión de la 16C-2, **fusionar `fase-16c-nube-1` a `main`** (Pull request con commit de fusión; la fusión dispara la primera publicación real de imágenes en GHCR). La 16C-2 (Azure) empieza en una sesión nueva con el «Prompt 2» de `fase-16c-prompt.md`.
