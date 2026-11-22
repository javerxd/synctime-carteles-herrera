# syntax=docker/dockerfile:1.7
# Fase 16A (RNFO14, RNFO15): imagen del frontend (Node compila, nginx sin privilegios sirve). El contexto es esta carpeta.
# Versiones fijas, verificadas contra Docker Hub el 22-11-2026 (sin «latest»).
ARG NODE_IMAGE=node:24.22.3-alpine3.24
ARG NGINX_IMAGE=nginxinc/nginx-unprivileged:1.31.7-alpine3.24

# ---------------------------------------------------------------------------------------------------------------------
FROM ${NODE_IMAGE} AS dependencias
WORKDIR /app
COPY package.json package-lock.json ./
# npm ci instala exactamente lo del package-lock.json (reproducible) y no ejecuta scripts de los paquetes.
RUN npm ci --ignore-scripts --no-audit --no-fund

# ---------------------------------------------------------------------------------------------------------------------
FROM dependencias AS construir
# VITE_* se incorporan al JavaScript publicado: aqui NUNCA va un secreto. El ID de cliente de Google no es secreto, pero tampoco se
# escribe en el repositorio: llega como argumento de construccion (compose lo toma del .env). VITE_API_URL queda vacia a proposito:
# la API se llama en el mismo origen (/api), que nginx (o la reescritura de Vercel) reenvia.
ARG VITE_GOOGLE_CLIENT_ID=""
ARG VITE_URL_PORTAL=""
ENV VITE_GOOGLE_CLIENT_ID=${VITE_GOOGLE_CLIENT_ID} \
    VITE_URL_PORTAL=${VITE_URL_PORTAL}
COPY . .
RUN npm run build

# ---------------------------------------------------------------------------------------------------------------------
# Destino final: nginx sin privilegios (usuario «nginx», puerto 8080).
FROM ${NGINX_IMAGE} AS web
ENV API_UPSTREAM=http://api:8080 \
    DNS_RESOLVER=127.0.0.11 \
    NGINX_ENVSUBST_FILTER=^(API_UPSTREAM|DNS_RESOLVER)$
COPY --chown=nginx:nginx nginx/cabeceras-seguridad.conf /etc/nginx/snippets/cabeceras-seguridad.conf
COPY --chown=nginx:nginx nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=construir --chown=nginx:nginx /app/dist /usr/share/nginx/html
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://127.0.0.1:8080/ || exit 1
USER nginx
