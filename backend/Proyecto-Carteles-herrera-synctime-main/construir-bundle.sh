#!/bin/sh
# Fase 16B (RNFO14): construye el migrador (efbundle) dentro de la imagen. Lo llama la etapa «bundle» del Dockerfile.
#
# Por que existe: `dotnet ef migrations bundle` ARMA la aplicacion (ejecuta Program.cs hasta antes de escuchar) para conocer el contexto de
# datos, y la API se niega a armarse sin su configuracion de produccion (cadena de conexion, JWT, CORS y URL publica). Ese fallo se vio
# en la primera ejecucion de la CI («No se configuro Jwt:Key»).
#
# Estas variables son SOLO DE DISENO, para que la herramienta pueda armar la aplicacion al construir:
#   - no se usan para migrar: el migrador recibe la configuracion REAL cuando se ejecuta (compose, o migrar.yml en la nube);
#   - la clave de las sesiones se genera al azar en cada construccion, no esta escrita en ningun archivo y no queda en la imagen final
#     (esta etapa no se copia a ella);
#   - la cadena de conexion no apunta a ningun servidor ni lleva clave.
set -eu

Jwt__Key="$(head -c 48 /dev/urandom | base64 | tr -d '\n')"
export Jwt__Key
export Jwt__Issuer=synctime
export Jwt__Audience=synctime
export ConnectionStrings__DefaultConnection="Server=sin-servidor;Database=sin-base;TrustServerCertificate=True"
export Cors__AllowedOrigins__0=http://localhost
export Sitio__UrlPublica=http://localhost

exec dotnet ef migrations bundle "$@"
