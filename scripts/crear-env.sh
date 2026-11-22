#!/usr/bin/env bash
# Fase 16A: crea el archivo .env para compose con claves al azar. No sobrescribe uno que ya exista.
# Uso, desde la raiz del repositorio:  ./scripts/crear-env.sh
# La clave de los usuarios de demostracion se muestra UNA vez al final para que pueda ingresar (queda tambien en .env).
set -euo pipefail

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
destino="$raiz/.env"
molde="$raiz/.env.example"

if [ -e "$destino" ]; then
    echo "Ya existe $destino: no se cambia nada. Borrelo si quiere generarlo de nuevo." >&2
    exit 0
fi
[ -f "$molde" ] || { echo "Falta $molde." >&2; exit 1; }
command -v openssl >/dev/null 2>&1 || { echo "Se necesita openssl para generar claves al azar." >&2; exit 1; }

# Solo letras y numeros: la clave de SQL Server va dentro de una cadena de conexion y no admite «;».
alfanumerica() { openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | cut -c1-"$1"; }

sa="$(alfanumerica 20)Aa1"
jwt="$(alfanumerica 48)"
demo="$(alfanumerica 14)Aa1-"
rostro="$(openssl rand -base64 32)"

umask 077
sed -e "s|^MSSQL_SA_PASSWORD=.*|MSSQL_SA_PASSWORD=$sa|" \
    -e "s|^JWT_KEY=.*|JWT_KEY=$jwt|" \
    -e "s|^DEMO_PASSWORD=.*|DEMO_PASSWORD=$demo|" \
    -e "s|^ROSTRO_CLAVE=.*|ROSTRO_CLAVE=$rostro|" \
    "$molde" > "$destino"

echo "Creado $destino (solo usted puede leerlo)."
echo "Usuarios de demostracion: dueno, admin, disenador, operario y cliente, todos @letreria.test"
echo "Clave de todos ellos: $demo"
echo "Ahora:  docker compose up --build   y abra http://localhost:8080"
