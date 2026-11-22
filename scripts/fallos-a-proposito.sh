#!/usr/bin/env bash
# Fase 16B (RNFO14, RNFO15): «fallo a proposito» que necesita Docker: la imagen de la API SIN datos de zona horaria (tzdata).
# Lo esperado, y lo que este guion comprueba:
#   1. La imagen SANA (control) arranca y su comprobacion de salud (/salud) responde 200.
#   2. La imagen SIN tzdata se NIEGA a arrancar (el contenedor termina con codigo distinto de 0) y su comprobacion de salud FALLA.
# La hora oficial de Chile no depende de la zona del servidor: sin tzdata la API no puede calcularla y prefiere no arrancar (en vez de
# guardar fechas con tres o cuatro horas de error). Deja la evidencia en docs/evidencias/fallos-a-proposito-<fecha>.md (sin secretos).
#
# Uso, desde la raiz del repositorio (en un Codespace o en cualquier equipo con Docker):   bash scripts/fallos-a-proposito.sh
# No toca ninguna base de datos: la API arranca con una cadena de conexion que no apunta a ningun servidor (no se necesita para /salud).
set -uo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$raiz"
api="backend/Proyecto-Carteles-herrera-synctime-main"
fecha="$(date -u +%Y-%m-%d)"
evidencia="docs/evidencias/fallos-a-proposito-${fecha}.md"
temporal="$(mktemp -d)"
sana="synctime-api:fallo-control"
rota="synctime-api:fallo-sin-tzdata"
puerto="${PUERTO_PRUEBA:-18080}"
fallos=0
filas=""

command -v docker >/dev/null 2>&1 || { echo "Falta docker." >&2; exit 2; }
limpiar() {
    docker rm --force fallo-control fallo-sin-tzdata >/dev/null 2>&1 || true
    docker rmi --force "$sana" "$rota" >/dev/null 2>&1 || true
    rm -rf "$temporal"
}
trap limpiar EXIT

registrar() { # estado, nombre, detalle
    local marca="OK"
    [ "$1" = "fallo" ] && { marca="FALLO"; fallos=$((fallos + 1)); }
    filas+="| ${marca} | $2 | $3 |"$'\n'
    echo "[${marca}] $2 - $3"
}

# Variables minimas de la API en produccion; los valores son de un solo uso y no se imprimen.
clave_jwt="$(openssl rand -base64 48 | tr -d '\n')"
clave_sql="Zz1-$(openssl rand -hex 12)"
entorno=(
    # Las mismas restricciones que compose.yaml da a la API (el punto de entrada necesita CHOWN, SETUID y SETGID para bajar privilegios).
    --security-opt no-new-privileges:true --cap-drop ALL --cap-add CHOWN --cap-add SETUID --cap-add SETGID
    --env "ASPNETCORE_ENVIRONMENT=Production"
    --env "ConnectionStrings__DefaultConnection=Server=127.0.0.1,1;Database=SyncTime_Fallo;User Id=sa;Password=${clave_sql};TrustServerCertificate=True;Connect Timeout=3"
    --env "Jwt__Key=${clave_jwt}" --env "Jwt__Issuer=synctime" --env "Jwt__Audience=synctime"
    --env "Cors__AllowedOrigins__0=http://localhost:8080" --env "Sitio__UrlPublica=http://localhost:8080"
)

echo "== Construyendo la imagen sana (control)"
if ! docker build --quiet --target api --tag "$sana" "$api" >/dev/null; then
    registrar fallo "Construir la imagen sana" "docker build fallo"
    echo "No se puede seguir sin la imagen sana." >&2
    exit 1
fi
registrar ok "Construir la imagen sana" "construida"

echo "== Construyendo la imagen SIN tzdata (copia del Dockerfile con la carpeta de zonas horarias borrada)"
# Se parte el Dockerfile en la etapa «base»: despues de instalar curl y tzdata, se borra todo /usr/share/zoneinfo.
sed -e 's#^\(\s*\)&& rm -rf /var/lib/apt/lists/\*$#\1\&\& rm -rf /var/lib/apt/lists/* /usr/share/zoneinfo#' "$api/Dockerfile" > "$temporal/Dockerfile.sin-tzdata"
if ! grep -q '/usr/share/zoneinfo' "$temporal/Dockerfile.sin-tzdata"; then
    registrar fallo "Preparar el Dockerfile roto" "el Dockerfile cambio y este guion ya no sabe estropearlo: actualice el sed"
    exit 1
fi
if ! docker build --quiet --file "$temporal/Dockerfile.sin-tzdata" --target api --tag "$rota" "$api" >/dev/null; then
    registrar fallo "Construir la imagen sin tzdata" "docker build fallo"
    exit 1
fi
if docker run --rm --entrypoint sh "$rota" -c 'test -e /usr/share/zoneinfo/America/Santiago'; then
    registrar fallo "La imagen rota no tiene zonas horarias" "America/Santiago sigue existiendo: el fallo a proposito no se aplico"
else
    registrar ok "La imagen rota no tiene zonas horarias" "/usr/share/zoneinfo/America/Santiago no existe"
fi

echo "== Control: la imagen sana debe arrancar y estar saludable"
docker run --detach --name fallo-control --publish "127.0.0.1:${puerto}:8080" "${entorno[@]}" "$sana" >/dev/null
estado_sana="sin-respuesta"
for _ in $(seq 1 30); do
    codigo="$(curl --silent --output /dev/null --write-out '%{http_code}' --max-time 3 "http://127.0.0.1:${puerto}/salud" || true)"
    if [ "$codigo" = "200" ]; then estado_sana="200"; break; fi
    sleep 2
done
if [ "$estado_sana" = "200" ]; then
    registrar ok "Control: la imagen sana arranca y /salud responde 200" "responde 200"
else
    registrar fallo "Control: la imagen sana arranca y /salud responde 200" "no respondio 200 en 60 s; ultimo registro: $(docker logs --tail 3 fallo-control 2>&1 | tr '\n' ' ' | cut -c1-300)"
fi
docker rm --force fallo-control >/dev/null 2>&1 || true

echo "== Fallo a proposito: la imagen sin tzdata debe negarse a arrancar"
docker run --detach --name fallo-sin-tzdata --publish "127.0.0.1:${puerto}:8080" "${entorno[@]}" "$rota" >/dev/null
corriendo="true"
for _ in $(seq 1 30); do
    corriendo="$(docker inspect -f '{{.State.Running}}' fallo-sin-tzdata 2>/dev/null || echo false)"
    [ "$corriendo" = "false" ] && break
    sleep 2
done
codigo_salida="$(docker inspect -f '{{.State.ExitCode}}' fallo-sin-tzdata 2>/dev/null || echo "?")"
if [ "$corriendo" = "false" ] && [ "$codigo_salida" != "0" ]; then
    registrar ok "Sin tzdata la API se niega a arrancar" "el contenedor termino con codigo ${codigo_salida}"
else
    registrar fallo "Sin tzdata la API se niega a arrancar" "sigue corriendo o termino con codigo 0 (${corriendo}, ${codigo_salida}): la API NO exige la zona horaria"
fi
motivo="$(docker logs --tail 4 fallo-sin-tzdata 2>&1 | tr '\n' ' ' | cut -c1-300)"
# La comprobacion de salud de la imagen (la misma orden del HEALTHCHECK) debe fallar: no hay nada escuchando.
codigo_salud="$(docker run --rm --network container:fallo-sin-tzdata --entrypoint sh "$rota" -c 'curl --fail --silent "http://127.0.0.1:8080/salud" >/dev/null 2>&1; echo $?' 2>/dev/null || echo "sin-red")"
if [ "$codigo_salud" != "0" ]; then
    registrar ok "Sin tzdata la comprobacion de salud falla" "la orden del HEALTHCHECK devolvio ${codigo_salud}"
else
    registrar fallo "Sin tzdata la comprobacion de salud falla" "la orden del HEALTHCHECK respondio bien"
fi

{
    echo "# Fallos a proposito con Docker — ${fecha}"
    echo
    echo "Generada por \`scripts/fallos-a-proposito.sh\` el $(date -u +%Y-%m-%dT%H:%M:%SZ) en: $(uname -sr), $(docker --version)."
    echo "Esta evidencia no contiene secretos (las claves de la prueba se generan al azar y no se imprimen)."
    echo
    echo "| Resultado | Comprobacion | Detalle |"
    echo "|---|---|---|"
    printf '%s' "$filas"
    echo
    echo "Ultimas lineas del registro de la API sin tzdata (para el informe): ${motivo}"
    echo
    echo "Resultado global: $([ "$fallos" -eq 0 ] && echo "**EL FALLO A PROPOSITO SE DETECTA COMO SE ESPERA**" || echo "**${fallos} COMPROBACION(ES) NO SE CUMPLIERON**")"
} > "$evidencia"

echo "Evidencia: ${evidencia}"
[ "$fallos" -eq 0 ]
