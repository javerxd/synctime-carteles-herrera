# Fallos a proposito con Docker — 2026-11-22

Generada por `scripts/fallos-a-proposito.sh` el 2026-11-22T19:14:38Z en: Linux 7.0.0-1014-aws, Docker version 28.0.4, build b8034c0.
Esta evidencia no contiene secretos (las claves de la prueba se generan al azar y no se imprimen).

| Resultado | Comprobacion | Detalle |
|---|---|---|
| OK | Construir la imagen sana | construida |
| OK | La imagen rota no tiene zonas horarias | /usr/share/zoneinfo/America/Santiago no existe |
| OK | Control: con tzdata la API no falla por la zona horaria | supera la comprobacion de zona horaria (si luego termina es por no tener base de datos, que este control no tiene) |
| OK | Sin tzdata la API se niega a arrancar, y por la zona horaria | el contenedor termino con codigo 139 y su registro nombra HoraChile.ZonaHoraria |
| OK | Sin tzdata la comprobacion de salud falla | la orden del HEALTHCHECK de la imagen falla (no hay API escuchando) |

Ultimas lineas del registro de la API sin tzdata (para el informe): Unhandled exception. System.InvalidOperationException: El servidor no tiene la zona horaria de Chile (America/Santiago). En Linux instala el paquete tzdata y no actives InvariantTimezone.    at LetrerosHerreraSYNCTIMEapi.Negocio.Local.HoraChile.Buscar() in /src/LetrerosHerreraSYNCTIMEapi/Negocio/Loc

Resultado global: **EL FALLO A PROPOSITO SE DETECTA COMO SE ESPERA**

## Procedencia

Evidencia generada por el job `fallo-tzdata` de GitHub Actions (`.github/workflows/ci.yml`, ejecución #5, commit `c65833b`, rama `fase-16-contenedores-nube`, ejecutor `ubuntu-24.04`): artefacto `evidencia-fallo-tzdata`, sin modificar salvo esta sección. La ejecución #4 (commit `fa4cb04`) ya había mostrado que sin `tzdata` la API se niega a arrancar en `HoraChile.ZonaHoraria`; su control fallaba por un defecto del guion (la API consulta la base al arrancar), corregido para la #5.
