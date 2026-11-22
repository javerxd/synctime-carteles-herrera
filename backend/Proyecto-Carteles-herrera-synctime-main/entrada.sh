#!/bin/sh
# Fase 16A: punto de entrada de la imagen de la API.
#  1. PORT es configurable (Render lo entrega por variable; sin ella, 8080).
#  2. Si el contenedor arranca como root (es lo normal: asi se puede preparar la carpeta de archivos), deja la carpeta de
#     archivos en manos del usuario de la aplicacion y BAJA los privilegios antes de ejecutar nada de la API. Un disco
#     persistente de Render o un volumen recien montado llegan a nombre de root, y sin esto la API (sin privilegios) no
#     podria guardar adjuntos, imagenes ni reportes. La API NUNCA corre como root.
#  3. Si ya arranca sin privilegios (por ejemplo, con «user:» en compose), ejecuta directamente.
set -eu

PORT="${PORT:-8080}"
export ASPNETCORE_HTTP_PORTS="$PORT"
unset ASPNETCORE_URLS

CARPETA_ARCHIVOS="${CARPETA_ARCHIVOS:-/app/almacen}"
UID_APP="${APP_UID:-1654}"

if [ "$(id -u)" = "0" ]; then
    mkdir -p "$CARPETA_ARCHIVOS"
    # Solo se cambia el dueno cuando hace falta: en un disco con muchos archivos, recorrerlo en cada arranque seria lento.
    if [ "$(stat -c %u "$CARPETA_ARCHIVOS")" != "$UID_APP" ]; then
        chown -R "$UID_APP:$UID_APP" "$CARPETA_ARCHIVOS"
    fi
    exec setpriv --reuid="$UID_APP" --regid="$UID_APP" --clear-groups --no-new-privs "$@"
fi

exec "$@"
