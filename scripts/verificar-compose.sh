#!/usr/bin/env bash
# Fase 16A (RNFO14): levanta TODO el entorno con `docker compose up`, espera la salud de cada servicio, recorre lo esencial
# (salud, portada, cabeceras y cache, ingreso con la semilla de demostracion, API protegida, buzon de correo) y deja la evidencia en
# docs/evidencias/compose-<fecha>.md. Se ejecuta en un Codespace o en GitHub Actions (el equipo de desarrollo no tiene Docker).
#
# Uso, desde la raiz del repositorio:
#   ./scripts/verificar-compose.sh              verifica y deja el entorno encendido
#   ./scripts/verificar-compose.sh --limpiar    verifica y despues apaga y borra los volumenes (docker compose down -v)
# Usa SOLO la base de demostracion de compose (SyncTime_Demo, en su propio volumen): nunca una base real.
# La evidencia no lleva secretos: antes de terminar se comprueba que ningun valor del .env aparezca en ella.
set -uo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$raiz"
limpiar=0
[ "${1:-}" = "--limpiar" ] && limpiar=1

fecha="$(date -u +%Y-%m-%d)"
evidencia="docs/evidencias/compose-${fecha}.md"
mkdir -p docs/evidencias
puerto_web="${WEB_PORT:-8080}"
puerto_correo="${MAIL_UI_PORT:-8025}"
base="http://localhost:${puerto_web}"

fallos=0
filas=""
registrar() { # estado, nombre, detalle
    local marca="OK"
    [ "$1" = "fallo" ] && { marca="FALLO"; fallos=$((fallos + 1)); }
    filas+="| ${marca} | $2 | $3 |"$'\n'
    echo "[${marca}] $2 - $3"
}
comprobar() { # nombre, comando..., (detalle se toma de la salida)
    local nombre="$1"; shift
    local salida
    if salida="$("$@" 2>&1)"; then registrar ok "$nombre" "${salida:-correcto}"; else registrar fallo "$nombre" "${salida:-sin salida}"; fi
}

command -v docker >/dev/null 2>&1 || { echo "Falta docker. Abra este repositorio en un Codespace (.devcontainer) o instale Docker." >&2; exit 2; }
[ -f .env ] || bash ./scripts/crear-env.sh >/dev/null   # «bash»: git guarda los .sh sin bit de ejecucion (modo 100644)
# El .env se LEE (no se ejecuta como shell): un valor con espacios, «$» o «;» no puede romper ni ejecutar nada.
while IFS='=' read -r clave valor; do
    case "$clave" in ''|'#'*) continue ;; esac
    [[ "$clave" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] && export "$clave=$valor"
done < .env
# Si algo falla a medias (incluida la guarda de secretos), --limpiar igual apaga y borra el entorno.
# Si algo fallo, los registros de los servicios se imprimen ANTES de apagar (despues ya no existirian).
trap 'if [ "$fallos" -gt 0 ]; then echo "== Registros de los servicios (ultimas 80 lineas)"; docker compose logs --no-color --tail 80; fi; if [ "$limpiar" -eq 1 ]; then docker compose down -v; fi' EXIT
puerto_web="${WEB_PORT:-8080}"; puerto_correo="${MAIL_UI_PORT:-8025}"; base="http://localhost:${puerto_web}"

echo "== Validacion estatica"
comprobar "docker compose config" bash -c 'docker compose config -q && echo "compose.yaml valido"'

echo "== Construccion y arranque (puede tardar varios minutos la primera vez)"
inicio=$SECONDS
if docker compose up -d --build; then
    registrar ok "docker compose up --build" "arranco en $((SECONDS - inicio)) s"
else
    registrar fallo "docker compose up --build" "no arranco: revise docker compose logs"
fi

estado_salud() { docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}sin-comprobacion{{end}}' "$(docker compose ps -a -q "$1")" 2>/dev/null; }
esperar_salud() { # servicio, segundos
    local limite=$((SECONDS + $2))
    while [ "$SECONDS" -lt "$limite" ]; do
        [ "$(estado_salud "$1")" = "healthy" ] && return 0
        sleep 5
    done
    return 1
}
for servicio in sql correo api web; do
    if esperar_salud "$servicio" 600; then registrar ok "salud de ${servicio}" "healthy"; else registrar fallo "salud de ${servicio}" "estado: $(estado_salud "$servicio")"; fi
done
for servicio in migrador semilla; do
    codigo="$(docker inspect -f '{{.State.ExitCode}}' "$(docker compose ps -a -q "$servicio")" 2>/dev/null || echo "?")"
    if [ "$codigo" = "0" ]; then registrar ok "${servicio} termino bien" "codigo de salida 0"; else registrar fallo "${servicio} termino bien" "codigo de salida ${codigo}"; fi
done

echo "== Recorrido por el sitio (${base})"
# Cada comprobacion es una funcion del propio guion (con pipefail): una cabecera ausente o un sitio caido FALLA, no pasa en falso.
http() { curl --silent --show-error --max-time 30 "$@"; }
cabecera() { local v; v="$(http --fail --head "$1" | tr -d '\r' | grep -i "^$2:" | head -1)" && [ -n "$v" ] && printf '%s' "$v"; }
c_api_publica() { http --fail "${base}/api/v1/local/estado" >/dev/null && echo "200 por /api"; }
c_portada() { http --fail "${base}/" | grep -q 'id="root"' && echo "index.html con #root"; }
c_ruta_spa() { http --fail "${base}/ingresar/cliente" | grep -q 'id="root"' && echo "/ingresar/cliente -> index.html"; }
c_index_sin_cache() { cabecera "${base}/" cache-control | grep -qi 'no-cache' && echo "Cache-Control: no-cache"; }
c_assets_cache() { local h; h="$(cabecera "${base}${js}" cache-control)" && grep -qi 'max-age=31536000' <<<"$h" && grep -qi immutable <<<"$h" && echo "${js}: immutable, 1 ano"; }
c_cabeceras() { local h; for h in content-security-policy x-content-type-options x-frame-options referrer-policy permissions-policy; do cabecera "${base}/" "$h" >/dev/null || { echo "falta $h"; return 1; }; done; echo "CSP, nosniff, frame, referrer y permisos presentes"; }
c_sin_coop() { local h; h="$(http --fail --head "${base}/" | tr -d '\r')" && ! grep -qi 'cross-origin-opener-policy' <<<"$h" && echo "ausente"; }
c_gzip() { http --fail --head -H 'Accept-Encoding: gzip' "${base}${js}" | grep -qi 'content-encoding: gzip' && echo "gzip activo"; }
c_401() { [ "$(http -o /dev/null -w '%{http_code}' "${base}/api/v1/solicitudes")" = 401 ] && echo "401 sin token"; }
c_200() { [ "$(http -o /dev/null -w '%{http_code}' -H "Authorization: Bearer ${TOKEN}" "${base}/api/v1/solicitudes")" = 200 ] && echo "200 con token"; }
c_correo() { http --fail "http://localhost:${puerto_correo}/api/v1/messages" >/dev/null && echo "Mailpit responde"; }
c_disco() { docker compose exec -T api sh -c 'echo "uid $(id -u)"; test -w /app/almacen && echo escribible' | tr '\n' ' '; }

js="$(http --fail "${base}/" | grep -o '/assets/[^"]*\.js' | head -1)"
comprobar "La API publica responde detras de nginx (/api/v1/local/estado)" c_api_publica
comprobar "La portada entrega la aplicacion" c_portada
comprobar "Una ruta de la aplicacion entrega index.html (try_files)" c_ruta_spa
comprobar "index.html sin cache (no-cache)" c_index_sin_cache
comprobar "Los /assets/ con huella se guardan un ano" c_assets_cache
comprobar "Cabeceras de seguridad del sitio" c_cabeceras
comprobar "Sin Cross-Origin-Opener-Policy (bloquearia la ventana de Google)" c_sin_coop
comprobar "Compresion gzip" c_gzip

# El token vive solo en esta variable de entorno del proceso (no en argv de otro programa ni en la evidencia).
cuerpo="$(http -X POST "${base}/api/v1/auth/login" -H 'Content-Type: application/json' --data-binary @- <<<"{\"email\":\"admin@letreria.test\",\"password\":\"${DEMO_PASSWORD}\"}")"
TOKEN="$(grep -o '"token":"[^"]*"' <<<"$cuerpo" | head -1 | cut -d'"' -f4)"; export TOKEN
if [ -n "$TOKEN" ]; then registrar ok "Ingreso del administrador de demostracion" "token recibido (no se guarda)"; else registrar fallo "Ingreso del administrador de demostracion" "no hubo token"; fi
comprobar "La API rechaza sin sesion (401)" c_401
comprobar "La API responde con sesion (200)" c_200
comprobar "Buzon de correo de desarrollo (Mailpit)" c_correo
comprobar "Disco de archivos escribible por el usuario sin privilegios" c_disco

{
    echo "# Verificacion de docker compose — ${fecha}"
    echo
    echo "Generada por \`scripts/verificar-compose.sh\` el $(date -u +%Y-%m-%dT%H:%M:%SZ) en: $(uname -sr), $(docker --version), $(docker compose version)."
    echo "Entorno: base de demostracion de compose (\`${SQL_DATABASE:-SyncTime_Demo}\`), datos inventados. Esta evidencia no contiene secretos."
    echo
    echo "| Resultado | Comprobacion | Detalle |"
    echo "|---|---|---|"
    printf '%s' "$filas"
    echo
    echo "Resultado global: $([ "$fallos" -eq 0 ] && echo "**TODO EN VERDE**" || echo "**${fallos} FALLO(S)**")"
    echo
    echo "## Servicios"
    echo '```'
    docker compose ps -a --format 'table {{.Service}}\t{{.Status}}'
    echo '```'
} > "$evidencia"

# Guarda final: ningun valor secreto del .env puede aparecer en la evidencia.
for variable in MSSQL_SA_PASSWORD JWT_KEY DEMO_PASSWORD ROSTRO_CLAVE ADMIN_PASSWORD; do
    valor="${!variable:-}"
    if [ -n "$valor" ] && grep -qF -- "$valor" "$evidencia"; then
        echo "ERROR: ${variable} aparece en ${evidencia}; se borra la evidencia." >&2
        rm -f "$evidencia"
        exit 3
    fi
done

echo "Evidencia: ${evidencia}"
[ "$fallos" -eq 0 ]
