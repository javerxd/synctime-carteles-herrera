# syntax=docker/dockerfile:1.7
# Fase 16A (RNFO14, RNFO15): imagen de la API. El contexto de construccion es esta carpeta.
#
# Etapas:
#   restaurar -> publicar -> api (destino por omision: la API en ejecucion)
#                         -> bundle -> migrador (aplica las migraciones; se ejecuta UNA vez y termina)
# Versiones fijas, verificadas contra el registro de Microsoft el 22-11-2026 (no se usa «latest» ni una etiqueta flotante).
# Para subir de version: cambiar ARG, volver a verificar la etiqueta y dejar la prueba de CI en verde.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.404-noble
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.15-noble

# ---------------------------------------------------------------------------------------------------------------------
# restaurar: solo el archivo de proyecto, para que las dependencias queden en una capa que no cambia con cada edicion.
FROM ${SDK_IMAGE} AS restaurar
WORKDIR /src
COPY LetrerosHerreraSYNCTIMEapi/LetrerosHerreraSYNCTIMEapi.csproj LetrerosHerreraSYNCTIMEapi/
RUN dotnet restore LetrerosHerreraSYNCTIMEapi/LetrerosHerreraSYNCTIMEapi.csproj

# ---------------------------------------------------------------------------------------------------------------------
# publicar: compila la API (sin las pruebas, que nunca entran a la imagen).
FROM restaurar AS publicar
COPY LetrerosHerreraSYNCTIMEapi/ LetrerosHerreraSYNCTIMEapi/
RUN dotnet publish LetrerosHerreraSYNCTIMEapi/LetrerosHerreraSYNCTIMEapi.csproj \
        --configuration Release --no-restore --output /app/publicar -p:UseAppHost=false

# ---------------------------------------------------------------------------------------------------------------------
# bundle: el migrador (efbundle), un ejecutable que aplica las migraciones sin necesitar el SDK ni el codigo fuente.
# La version de dotnet-ef es la de los paquetes de EF Core del proyecto o una posterior.
FROM publicar AS bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.14
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
        --project LetrerosHerreraSYNCTIMEapi/LetrerosHerreraSYNCTIMEapi.csproj \
        --configuration Release --output /app/efbundle --force

# ---------------------------------------------------------------------------------------------------------------------
# base de ejecucion: tzdata (la hora oficial de Chile no depende de la zona del servidor: sin ella la API se niega a arrancar,
# a proposito) y curl (para la comprobacion de salud). El PDF no necesita fuentes del sistema: la fuente va incrustada.
FROM ${RUNTIME_IMAGE} AS base
USER root
# hadolint ignore=DL3008
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

# ---------------------------------------------------------------------------------------------------------------------
# migrador: aplica las migraciones y termina. Corre sin privilegios. NUNCA corre solo contra una base real: en la nube se
# ejecuta a mano, con respaldo previo (ver docs/despliegue). Necesita las mismas variables de entorno que la API (la
# herramienta de EF arma la aplicacion para conocer el contexto), mas la cadena de conexion.
FROM base AS migrador
WORKDIR /migrador
COPY --from=bundle /app/efbundle /migrador/efbundle
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/extraccion \
    DOTNET_NOLOGO=1
USER 1654
ENTRYPOINT ["/migrador/efbundle"]

# ---------------------------------------------------------------------------------------------------------------------
# api: la imagen final y destino por omision (el ultimo FROM es el que se construye si no se indica --target).
FROM base AS api
COPY --from=publicar /app/publicar /app
COPY entrada.sh /app/entrada.sh
RUN chmod 0755 /app/entrada.sh \
    && mkdir -p /app/almacen \
    && chown -R 1654:1654 /app/almacen

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_EnableDiagnostics=0 \
    PORT=8080 \
    CARPETA_ARCHIVOS=/app/almacen
# Los archivos de los clientes (adjuntos, imagenes de la vitrina y reportes) viven aqui: se monta un volumen o un disco.
VOLUME ["/app/almacen"]
EXPOSE 8080

# /salud solo comprueba que el proceso responde (no consulta la base): una base en pausa no debe reiniciar la API.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl --fail --silent --show-error "http://127.0.0.1:${PORT:-8080}/salud" || exit 1

# El punto de entrada arranca como root solo para preparar la carpeta de archivos y baja a un usuario sin privilegios
# (UID 1654, «app») ANTES de ejecutar dotnet: la API nunca corre como root.
ENTRYPOINT ["/app/entrada.sh"]
CMD ["dotnet", "LetrerosHerreraSYNCTIMEapi.dll"]
