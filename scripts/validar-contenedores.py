#!/usr/bin/env python3
"""Fase 16A (RNFO14, RNFO15): validacion ESTATICA de los archivos de contenedores y de nube.

En el equipo de desarrollo no hay Docker, asi que esto comprueba lo que se puede comprobar sin construir nada: que los YAML y JSON se
lean, que las imagenes tengan version fija, que nada corra como root ni publique la base de datos, que ningun secreto este escrito en un
archivo y que el sitio (nginx) y Vercel lleven las mismas cabeceras. La verificacion REAL (construir y levantar) es la de la fase 16B,
en un Codespace y en GitHub Actions (scripts/verificar-compose.sh).

Uso:  python scripts/validar-contenedores.py [raiz]        (necesita PyYAML: pip install pyyaml)
Sale con codigo 1 si hay algun FALLO. Las pruebas de este validador son scripts/test_validar_contenedores.py.
"""
import json
import re
import sys
from pathlib import Path

import yaml

API = "backend/Proyecto-Carteles-herrera-synctime-main"
WEB = "frontend/synctimefront"
NOMBRE_SECRETO = re.compile(r"(PASSWORD|SECRET|TOKEN|CLAVE|_KEY$|__KEY$|APIKEY|CONNECTIONSTRING)", re.IGNORECASE)
# Variables cuyo nombre parece un secreto pero que NO lo son (version, nombre de un archivo, etc.).
NO_SECRETO = re.compile(r"(PUBLIC|ISSUER|AUDIENCE)", re.IGNORECASE)


def leer(raiz, relativo):
    ruta = raiz / relativo
    return ruta.read_text(encoding="utf-8") if ruta.exists() else None


def valida_imagen(referencia):
    """Una imagen es fija si tiene etiqueta explicita que no sea latest ni flotante (sin version numerica)."""
    if "@sha256:" in referencia:
        return True
    if ":" not in referencia.rsplit("/", 1)[-1]:
        return False
    etiqueta = referencia.rsplit(":", 1)[1]
    return etiqueta not in ("latest", "edge", "stable", "main", "master") and bool(re.search(r"\d", etiqueta))


def es_secreto(nombre):
    return bool(NOMBRE_SECRETO.search(nombre)) and not NO_SECRETO.search(nombre)


def referencia_variable(valor):
    return isinstance(valor, str) and re.search(r"\$\{[A-Za-z_][A-Za-z0-9_]*(:?[-?][^}]*)?\}", valor) is not None


def csp_de(texto, formato):
    if formato == "nginx":
        m = re.search(r'add_header Content-Security-Policy "([^"]*)"', texto)
        return m.group(1) if m else None
    for bloque in json.loads(texto).get("headers", []):
        for cabecera in bloque.get("headers", []):
            if cabecera["key"].lower() == "content-security-policy":
                return cabecera["value"]
    return None


# --------------------------------------------------------------------------------------------------------------------
def etapa_final(texto):
    """El texto de la ultima etapa del Dockerfile (la que se ejecuta): ahi se mira el usuario."""
    return re.split(r"^FROM\s", texto, flags=re.MULTILINE | re.IGNORECASE)[-1]


def revisar_dockerfile(raiz, relativo, resultados, es_api):
    texto = leer(raiz, relativo)
    if texto is None:
        resultados.append(f"FALLO [dockerfile] falta {relativo}")
        return
    # ARG con valor por omision: sirven para resolver «FROM ${IMAGEN}».
    argumentos = dict(re.findall(r"^ARG\s+([A-Za-z_]+)=(\S+)", texto, re.MULTILINE))
    etapas = re.findall(r"^FROM\s+(\S+)(?:\s+AS\s+(\S+))?", texto, re.MULTILINE | re.IGNORECASE)
    if not etapas:
        resultados.append(f"FALLO [dockerfile] {relativo} no tiene FROM")
        return
    nombres_etapa = {nombre for _, nombre in etapas if nombre}
    for imagen, _ in etapas:
        if imagen in nombres_etapa:
            continue
        resuelta = re.sub(r"\$\{([A-Za-z_]+)\}", lambda m: argumentos.get(m.group(1), m.group(0)), imagen)
        if not valida_imagen(resuelta):
            resultados.append(f"FALLO [imagen-fija] {relativo}: «{imagen}» → «{resuelta}» no tiene version fija")
    if not re.search(r"^HEALTHCHECK\s", texto, re.MULTILINE):
        resultados.append(f"FALLO [healthcheck] {relativo} no define HEALTHCHECK")
    for linea in texto.splitlines():
        if re.match(r"^\s*(ENV|ARG)\s", linea) or linea.startswith("    ") and "=" in linea:
            for nombre, valor in re.findall(r"([A-Za-z_][A-Za-z0-9_]*)=(\S+)", linea):
                if es_secreto(nombre) and valor not in ('""', "''", "") and not referencia_variable(valor):
                    resultados.append(f"FALLO [secretos] {relativo}: {nombre} lleva un valor escrito en el Dockerfile")
    if re.search(r"curl[^\n|]*\|\s*(ba)?sh|^ADD\s+https?://", texto, re.MULTILINE):
        resultados.append(f"FALLO [dockerfile] {relativo} descarga y ejecuta codigo sin verificar")
    if es_api:
        for paquete in ("curl", "tzdata"):
            if not re.search(rf"apt-get install[^\n]*(\\\n[^\n]*)*\b{paquete}\b", texto):
                resultados.append(f"FALLO [{paquete}] la imagen de la API no instala {paquete}")
        if "efbundle" not in texto or not re.search(r"AS migrador", texto, re.IGNORECASE):
            resultados.append("FALLO [migrador] la imagen de la API no tiene la etapa del migrador con efbundle")
        # Fase 16B: la herramienta de EF arma la API para conocer el contexto y esta exige su configuracion; sin el script de diseno el migrador no se construye.
        if "construir-bundle.sh" not in texto or re.search(r"^RUN\s+dotnet ef migrations bundle", texto, re.MULTILINE):
            resultados.append("FALLO [bundle-config] el migrador debe construirse con construir-bundle.sh (la herramienta de EF arma la API, que exige configuracion)")
        script = leer(raiz, f"{API}/construir-bundle.sh") or ""
        for variable in ("Jwt__Key", "Jwt__Issuer", "Jwt__Audience", "ConnectionStrings__DefaultConnection", "Cors__AllowedOrigins__0", "Sitio__UrlPublica"):
            if not re.search(rf"^(export\s+)?{variable}=", script, re.MULTILINE):
                resultados.append(f"FALLO [bundle-config] construir-bundle.sh no da {variable} a la herramienta de EF")
        if not re.search(r"^Jwt__Key=\"\$\(head -c \d+ /dev/urandom", script, re.MULTILINE):
            resultados.append("FALLO [bundle-config] la clave JWT de diseno debe generarse al azar (/dev/urandom), no escribirse")
        if "exec dotnet ef migrations bundle" not in script:
            resultados.append("FALLO [bundle-config] construir-bundle.sh debe terminar ejecutando «dotnet ef migrations bundle»")
        if etapas[-1][1] != "api":
            resultados.append("FALLO [dockerfile] la ultima etapa debe ser «api» (destino por omision)")
        entrada = leer(raiz, f"{API}/entrada.sh") or ""
        # La API nunca corre como root: o el Dockerfile baja a un usuario, o el punto de entrada baja los privilegios.
        usuarios = re.findall(r"^USER\s+(\S+)", etapa_final(texto), re.MULTILINE)
        baja = "setpriv" in entrada and "ENTRYPOINT [\"/app/entrada.sh\"]" in texto and "--reuid" in entrada
        if not baja and (not usuarios or usuarios[-1] in ("root", "0")):
            resultados.append(f"FALLO [sin-privilegios] {relativo}: la API corre como root y nada baja los privilegios")
        if "${PORT" not in entrada:
            resultados.append("FALLO [puerto] entrada.sh no lee PORT: Render entrega el puerto por variable")
    else:
        usuarios = re.findall(r"^USER\s+(\S+)", etapa_final(texto), re.MULTILINE)
        if not usuarios or usuarios[-1] in ("root", "0"):
            resultados.append(f"FALLO [sin-privilegios] {relativo}: el frontend corre como root")


def revisar_dockerignore(raiz, relativo, exigidos, resultados):
    texto = leer(raiz, relativo)
    if texto is None:
        resultados.append(f"FALLO [dockerignore] falta {relativo}")
        return
    # Se compara la linea completa (sin «**/» ni «/» final): «.env.*» o «!.env.example» no cuentan como excluir «.env».
    lineas = {linea.strip().removeprefix("**/").rstrip("/") for linea in texto.splitlines() if linea.strip() and not linea.strip().startswith("#")}
    for patron in exigidos:
        if patron.rstrip("/") not in lineas:
            resultados.append(f"FALLO [dockerignore] {relativo} no excluye «{patron}»")


def revisar_compose(raiz, resultados):
    texto = leer(raiz, "compose.yaml")
    if texto is None:
        resultados.append("FALLO [yaml] falta compose.yaml")
        return
    try:
        compose = yaml.safe_load(texto)
    except yaml.YAMLError as error:
        resultados.append(f"FALLO [yaml] compose.yaml no se puede leer: {str(error).splitlines()[0]}")
        return
    servicios = compose.get("services") or {}
    for requerido in ("web", "api", "sql", "migrador", "correo"):
        if requerido not in servicios:
            resultados.append(f"FALLO [compose] falta el servicio «{requerido}»")
    for nombre, servicio in servicios.items():
        if "image" in servicio and "build" not in servicio and not valida_imagen(servicio["image"]):
            resultados.append(f"FALLO [imagen-fija] compose.yaml: {nombre} usa «{servicio['image']}» sin version fija")
        if servicio.get("privileged") or servicio.get("network_mode") == "host" or servicio.get("pid") == "host":
            resultados.append(f"FALLO [privilegios] compose.yaml: {nombre} corre con privilegios del anfitrion")
        for volumen in servicio.get("volumes") or []:
            origen = volumen if isinstance(volumen, str) else volumen.get("source", "")
            if "docker.sock" in str(origen):
                resultados.append(f"FALLO [privilegios] compose.yaml: {nombre} monta el socket de Docker")
        for puerto in servicio.get("ports") or []:
            texto_puerto = str(puerto)
            if nombre not in ("web", "correo"):
                resultados.append(f"FALLO [puertos] compose.yaml: {nombre} publica el puerto {texto_puerto}; solo web y correo se publican")
            elif not texto_puerto.startswith("127.0.0.1:"):
                resultados.append(f"FALLO [puertos] compose.yaml: {nombre} publica {texto_puerto} a toda la red; debe ser solo 127.0.0.1")
        entorno = servicio.get("environment") or {}
        if isinstance(entorno, list):
            entorno = dict(item.split("=", 1) for item in entorno if "=" in item)
        for variable, valor in entorno.items():
            if es_secreto(variable) and str(valor) != "" and not referencia_variable(str(valor)):
                resultados.append(f"FALLO [secretos] compose.yaml: {nombre}.{variable} tiene un valor escrito")
    # Los bloques compartidos (anclas) tambien se revisan: viven fuera de «services».
    for clave, valor in compose.items():
        if clave.startswith("x-") and isinstance(valor, dict):
            for variable, contenido in valor.items():
                if es_secreto(variable) and str(contenido) != "" and not referencia_variable(str(contenido)):
                    resultados.append(f"FALLO [secretos] compose.yaml: {clave}.{variable} tiene un valor escrito")
    for requerido in ("sql", "api", "web", "correo"):
        if requerido in servicios and "healthcheck" not in servicios[requerido]:
            resultados.append(f"FALLO [healthcheck] compose.yaml: {requerido} no tiene healthcheck")
    migrador = servicios.get("migrador", {})
    if str(migrador.get("restart", "no")) not in ("no", "False", "false"):
        resultados.append("FALLO [migrador] el migrador debe ser de un solo uso (restart: \"no\")")
    dependencias = (servicios.get("api") or {}).get("depends_on") or {}
    if (dependencias.get("migrador") or {}).get("condition") != "service_completed_successfully":
        resultados.append("FALLO [migrador] la API debe esperar a que el migrador termine bien (service_completed_successfully)")
    if (dependencias.get("sql") or {}).get("condition") != "service_healthy":
        resultados.append("FALLO [migrador] la API debe esperar a que sql este saludable")
    if (((servicios.get("migrador") or {}).get("depends_on") or {}).get("sql") or {}).get("condition") != "service_healthy":
        resultados.append("FALLO [migrador] el migrador debe esperar a que sql este saludable")
    if "cap_drop" not in (servicios.get("api") or {}):
        resultados.append("FALLO [privilegios] la API deberia quitar las capacidades (cap_drop: [ALL]) y agregar solo las que usa")
    if not compose.get("networks"):
        resultados.append("FALLO [compose] falta la red interna")
    for volumen in ("sql-datos", "almacen-api"):
        if volumen not in (compose.get("volumes") or {}):
            resultados.append(f"FALLO [compose] falta el volumen nombrado «{volumen}»")
    # Las variables obligatorias se declaran con ${VAR:?mensaje}: sin .env, compose se niega a arrancar y dice por que.
    for variable in ("MSSQL_SA_PASSWORD", "JWT_KEY", "DEMO_PASSWORD"):
        if f"${{{variable}:?" not in texto:
            resultados.append(f"FALLO [secretos] compose.yaml no exige {variable} con ${{{variable}:?...}}")


def revisar_env_y_gitignore(raiz, resultados):
    texto = leer(raiz, ".env.example")
    if texto is None:
        resultados.append("FALLO [secretos] falta .env.example")
    else:
        for linea in texto.splitlines():
            if linea.strip().startswith("#") or "=" not in linea:
                continue
            nombre, valor = linea.split("=", 1)
            if es_secreto(nombre) and valor.strip() != "":
                resultados.append(f"FALLO [secretos] .env.example: {nombre} trae un valor; solo el nombre")
    gitignore = (leer(raiz, ".gitignore") or "").splitlines()
    if ".env" not in [linea.strip() for linea in gitignore]:
        resultados.append("FALLO [gitignore] .gitignore no excluye .env")


def revisar_render(raiz, resultados):
    texto = leer(raiz, "render.yaml")
    if texto is None:
        resultados.append("FALLO [render] falta render.yaml")
        return
    try:
        render = yaml.safe_load(texto)
    except yaml.YAMLError as error:
        resultados.append(f"FALLO [yaml] render.yaml no se puede leer: {str(error).splitlines()[0]}")
        return
    servicios = render.get("services") or []
    api = next((s for s in servicios if s.get("name") == "synctime-api"), None)
    if not api:
        resultados.append("FALLO [render] falta el servicio synctime-api")
        return
    if api.get("runtime") != "docker":
        resultados.append("FALLO [render] la API debe ser runtime: docker")
    if api.get("healthCheckPath") != "/salud":
        resultados.append("FALLO [render] healthCheckPath debe ser /salud (no /salud/lista: consultaria la base cada pocos segundos)")
    if (api.get("disk") or {}).get("mountPath") != "/app/almacen":
        resultados.append("FALLO [render] el disco persistente debe montarse en /app/almacen")
    if str(api.get("autoDeployTrigger")) != "off":
        resultados.append("FALLO [render] autoDeployTrigger debe ser off: una version nueva puede exigir migraciones previas")
    for variable in api.get("envVars") or []:
        clave = variable.get("key", "")
        if es_secreto(clave) or clave.startswith("ConnectionStrings") or clave.startswith("Bootstrap__Admin"):
            if "value" in variable or variable.get("sync") is not False:
                resultados.append(f"FALLO [render-secretos] render.yaml: {clave} debe declararse con sync: false y sin valor")


def revisar_sitio(raiz, resultados):
    vercel_texto = leer(raiz, f"{WEB}/vercel.json")
    cabeceras = leer(raiz, f"{WEB}/nginx/cabeceras-seguridad.conf")
    plantilla = leer(raiz, f"{WEB}/nginx/default.conf.template")
    if vercel_texto is None or cabeceras is None or plantilla is None:
        resultados.append("FALLO [vercel] faltan vercel.json o los archivos de nginx")
        return
    try:
        vercel = json.loads(vercel_texto)
    except json.JSONDecodeError as error:
        resultados.append(f"FALLO [yaml] vercel.json no es JSON valido: {error}")
        return
    reescrituras = vercel.get("rewrites") or []
    api = next((r for r in reescrituras if r.get("source", "").startswith("/api/")), None)
    if not api or not str(api.get("destination", "")).startswith("https://"):
        resultados.append("FALLO [reescritura] vercel.json debe reescribir /api/:path* hacia la API por https")
    if re.search(r"cross-origin-opener-policy", vercel_texto, re.IGNORECASE) and "same-origin" in vercel_texto:
        resultados.append("FALLO [coop] vercel.json envia Cross-Origin-Opener-Policy same-origin: bloquearia la ventana de Google")
    if re.search(r"add_header\s+Cross-Origin-Opener-Policy", cabeceras, re.IGNORECASE):
        resultados.append("FALLO [coop] nginx envia Cross-Origin-Opener-Policy: bloquearia la ventana de Google")
    inmutable = "public, max-age=31536000, immutable"
    assets_vercel = [b for b in vercel.get("headers", []) if b.get("source", "").startswith("/assets/")]
    if not any(c["value"] == inmutable for b in assets_vercel for c in b["headers"] if c["key"] == "Cache-Control"):
        resultados.append("FALLO [cache] vercel.json no guarda /assets/ un ano (immutable)")
    if inmutable not in plantilla:
        resultados.append("FALLO [cache] nginx no guarda /assets/ un ano (immutable)")
    if not re.search(r"location = /index\.html \{[^}]*no-cache", plantilla):
        resultados.append("FALLO [cache] nginx debe servir index.html con no-cache")
    for exigido in ("gzip on;", "try_files $uri /index.html;", "location ^~ /api/", "proxy_pass"):
        if exigido not in plantilla:
            resultados.append(f"FALLO [nginx] la plantilla no contiene «{exigido}»")
    csp_vercel, csp_nginx = csp_de(vercel_texto, "vercel"), csp_de(cabeceras, "nginx")
    if not csp_vercel or not csp_nginx:
        resultados.append("FALLO [csp-igual] falta la politica de contenido en vercel.json o en nginx")
    elif csp_vercel != csp_nginx:
        resultados.append("FALLO [csp-igual] la politica de contenido de nginx y la de vercel.json no coinciden")
    elif "frame-ancestors 'none'" not in csp_vercel or "object-src 'none'" not in csp_vercel:
        resultados.append("FALLO [csp-igual] la politica de contenido debe llevar frame-ancestors 'none' y object-src 'none'")


def revisar_secretos(raiz, resultados):
    """Busca secretos evidentes en los archivos nuevos de esta fase."""
    patrones = [
        (re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----"), "una clave privada"),
        (re.compile(r"AKIA[0-9A-Z]{16}"), "una clave de acceso de AWS"),
        (re.compile(r"(?i)password\s*=\s*(?!\$\{|\$\$|\{|<|\"?\s*;|[\s;]|$)[^;\s'\"$]{6,}"), "una contrasena escrita en una cadena de conexion"),
        (re.compile(r"(?i)\b(GOCSPX-[A-Za-z0-9_-]{10,})"), "un secreto de cliente de Google"),
        (re.compile(r"\b[0-9]{6,}-[a-z0-9]{20,}\.apps\.googleusercontent\.com\b"), "un ID de cliente de Google"),
    ]
    archivos = ["compose.yaml", "render.yaml", ".env.example", f"{API}/Dockerfile", f"{API}/entrada.sh", f"{API}/construir-bundle.sh", f"{WEB}/Dockerfile",
                f"{WEB}/vercel.json", f"{WEB}/nginx/default.conf.template", f"{WEB}/nginx/cabeceras-seguridad.conf"]
    for relativo in archivos:
        texto = leer(raiz, relativo)
        if texto is None:
            continue
        for patron, descripcion in patrones:
            if patron.search(texto):
                resultados.append(f"FALLO [secretos] {relativo} contiene {descripcion}")


def validar(raiz):
    raiz = Path(raiz)
    resultados = []
    revisar_dockerfile(raiz, f"{API}/Dockerfile", resultados, es_api=True)
    revisar_dockerfile(raiz, f"{WEB}/Dockerfile", resultados, es_api=False)
    revisar_dockerignore(raiz, f"{API}/.dockerignore", [".env", "bin/", "obj/", "almacen"], resultados)
    revisar_dockerignore(raiz, f"{WEB}/.dockerignore", [".env", "node_modules"], resultados)
    revisar_compose(raiz, resultados)
    revisar_env_y_gitignore(raiz, resultados)
    revisar_render(raiz, resultados)
    revisar_sitio(raiz, resultados)
    revisar_secretos(raiz, resultados)
    if not resultados:
        resultados.append("OK todas las reglas de contenedores y de nube se cumplen")
    return resultados


if __name__ == "__main__":
    raiz_argumento = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent
    salida = validar(raiz_argumento)
    for linea in salida:
        print(linea)
    sys.exit(1 if any(linea.startswith("FALLO") for linea in salida) else 0)
