#!/usr/bin/env python3
"""Fase 16B (RNFO15): validacion ESTATICA del pipeline (GitHub Actions) y de lo que lo rodea.

Los workflows no se pueden ejecutar en el equipo de desarrollo, asi que esto comprueba lo que si se puede comprobar: que las acciones de
terceros esten fijadas por SHA, que los permisos sean los minimos, que ningun secreto este escrito, que la publicacion de imagenes dependa
de que TODO lo demas haya pasado, que la migracion y el respaldo tengan sus protecciones y que las pruebas del backend no dependan de la
hora del equipo. La verificacion REAL es la ejecucion en GitHub (y en un Codespace para compose).

Uso:  python scripts/validar-workflows.py [raiz]        (necesita PyYAML: pip install pyyaml)
Sale con codigo 1 si hay algun FALLO. Las pruebas de este validador son scripts/test_validar_workflows.py.
"""
import re
import sys
from pathlib import Path

import yaml

API = "backend/Proyecto-Carteles-herrera-synctime-main"
CARPETA_WORKFLOWS = ".github/workflows"
SQL_IMAGEN = "mcr.microsoft.com/mssql/server:2022-CU28-ubuntu-22.04"
NOMBRE_SECRETO = re.compile(r"(PASSWORD|SECRET|TOKEN|CLAVE|_KEY$|__KEY$|APIKEY|CONNECTIONSTRING)", re.IGNORECASE)

# Disparadores permitidos por workflow: la migracion JAMAS corre sola y la CI no se programa.
DISPARADORES = {
    "ci.yml": {"push", "workflow_dispatch"},
    "migrar.yml": {"workflow_dispatch"},
    "respaldo.yml": {"schedule", "workflow_dispatch"},
}
# (workflow, job) -> permisos de ESCRITURA que ese job puede pedir. Todo lo demas es de solo lectura.
ESCRITURA_PERMITIDA = {
    ("ci.yml", "publicar"): {"packages"},
    ("migrar.yml", "migrar"): {"id-token"},
    ("respaldo.yml", "respaldar"): {"id-token"},
}
SECRETOS_EVIDENTES = [
    (re.compile(r"ghp_[0-9A-Za-z]{36}"), "un token de GitHub"),
    (re.compile(r"github_pat_[0-9A-Za-z_]{22,}"), "un token de GitHub"),
    (re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----"), "una clave privada"),
    (re.compile(r"AKIA[0-9A-Z]{16}"), "una clave de acceso de AWS"),
    (re.compile(r"xox[baprs]-[0-9A-Za-z-]{10,}"), "un token de Slack"),
    (re.compile(r"AccountKey=[A-Za-z0-9+/=]{20,}"), "la llave de una cuenta de almacenamiento de Azure"),
    (re.compile(r"[?&]sig=[A-Za-z0-9%+/=]{20,}"), "una firma de acceso compartido (SAS) de Azure"),
    (re.compile(r"(?i)password\s*=\s*(?!\$|\{|<|\"?\s*;|[\s;]|$)[^;\s'\"$]{6,}"), "una contrasena escrita en una cadena de conexion"),
    (re.compile(r"GOCSPX-[A-Za-z0-9_-]{10,}"), "un secreto de cliente de Google"),
    (re.compile(r"\b[0-9]{6,}-[a-z0-9]{20,}\.apps\.googleusercontent\.com\b"), "un ID de cliente de Google"),
]


def leer(raiz, relativo):
    ruta = raiz / relativo
    return ruta.read_text(encoding="utf-8") if ruta.exists() else None


def cargar_workflow(texto):
    datos = yaml.safe_load(texto)
    # PyYAML (YAML 1.1) lee la clave «on» como True.
    if isinstance(datos, dict) and True in datos and "on" not in datos:
        datos["on"] = datos.pop(True)
    return datos


def disparadores_de(workflow):
    on = workflow.get("on")
    if isinstance(on, str):
        return {on}
    if isinstance(on, list):
        return set(on)
    return set(on or {})


def pasos_de(trabajo):
    return [p for p in (trabajo.get("steps") or []) if isinstance(p, dict)]


def nombres_de_pasos(trabajo):
    return [str(p.get("name", "")) for p in pasos_de(trabajo)]


def lineas_de_codigo(texto):
    """Lineas que no son comentarios completos (para no marcar una palabra que solo aparece explicada en un comentario)."""
    return [l for l in texto.splitlines() if not l.lstrip().startswith("#")]


# --------------------------------------------------------------------------------------------------------------------
def revisar_texto_comun(nombre, texto, resultados):
    for patron, descripcion in SECRETOS_EVIDENTES:
        if patron.search(texto):
            resultados.append(f"FALLO [secretos] {nombre} contiene {descripcion}")
    if re.search(r"curl[^\n|]*\|\s*(ba)?sh|wget[^\n|]*\|\s*(ba)?sh", texto):
        resultados.append(f"FALLO [descarga-ejecutada] {nombre} descarga y ejecuta codigo sin verificar")
    for linea in lineas_de_codigo(texto):
        m = re.match(r"^\s*-?\s*uses:\s*(\S+)(.*)$", linea)
        if not m:
            continue
        referencia, resto = m.group(1), m.group(2)
        if referencia.startswith("./") or referencia.startswith("docker://"):
            continue
        if not re.fullmatch(r"[\w.-]+/[\w./-]+@[0-9a-f]{40}", referencia):
            resultados.append(f"FALLO [accion-fijada] {nombre}: «{referencia}» no esta fijada por SHA completo de 40 caracteres")
        elif not re.match(r"\s+#\s*v?\d", resto):
            resultados.append(f"FALLO [accion-fijada] {nombre}: «{referencia}» no lleva el comentario con su version (# vX.Y.Z)")


def revisar_workflow(nombre, texto, workflow, resultados):
    """Reglas que valen para TODOS los workflows."""
    archivo = nombre.rsplit("/", 1)[-1]
    # Disparadores
    activos = disparadores_de(workflow)
    permitidos = DISPARADORES.get(archivo)
    if "pull_request_target" in activos or "workflow_run" in activos:
        resultados.append(f"FALLO [disparadores] {nombre}: pull_request_target y workflow_run ejecutan codigo ajeno con secretos")
    if permitidos is not None and activos - permitidos:
        resultados.append(f"FALLO [disparadores] {nombre}: dispara con {sorted(activos - permitidos)}; solo se permite {sorted(permitidos)}")
    # Permisos
    permisos = workflow.get("permissions")
    if not isinstance(permisos, dict) or permisos.get("contents") != "read" or any(v != "read" for v in permisos.values()):
        resultados.append(f"FALLO [permisos] {nombre}: debe declarar «permissions: contents: read» (y nada de escritura) a nivel de workflow")
    for id_trabajo, trabajo in (workflow.get("jobs") or {}).items():
        permisos_trabajo = trabajo.get("permissions")
        if isinstance(permisos_trabajo, str):
            resultados.append(f"FALLO [permisos] {nombre}: el job «{id_trabajo}» usa «permissions: {permisos_trabajo}»; declare cada permiso")
        elif isinstance(permisos_trabajo, dict):
            permitidas = ESCRITURA_PERMITIDA.get((archivo, id_trabajo), set())
            for permiso, valor in permisos_trabajo.items():
                if valor == "write" and permiso not in permitidas:
                    resultados.append(f"FALLO [permisos] {nombre}: el job «{id_trabajo}» pide «{permiso}: write», que no le corresponde")
        # Robustez
        minutos = trabajo.get("timeout-minutes")
        if not isinstance(minutos, int) or not 0 < minutos <= 120:
            resultados.append(f"FALLO [timeout] {nombre}: el job «{id_trabajo}» necesita timeout-minutes (1 a 120)")
        if trabajo.get("runs-on") != "ubuntu-24.04":
            resultados.append(f"FALLO [ejecutor] {nombre}: el job «{id_trabajo}» debe usar «runs-on: ubuntu-24.04» (no «latest»)")
        for paso in pasos_de(trabajo):
            descripcion = paso.get("name") or paso.get("uses") or "(sin nombre)"
            ejecuta = paso.get("run")
            if isinstance(ejecuta, str):
                if "${{" in ejecuta:
                    resultados.append(f"FALLO [inyeccion] {nombre}: el paso «{descripcion}» usa «${{{{ }}}}» dentro de run; pase el valor por env")
                if re.search(r"openssl rand|\$RANDOM", ejecuta) and re.search(r"GITHUB_(ENV|OUTPUT)", ejecuta) and "::add-mask::" not in ejecuta:
                    resultados.append(f"FALLO [mascara] {nombre}: el paso «{descripcion}» guarda un valor generado sin «::add-mask::» antes")
                if ("releases/download" in ejecuta or re.search(r"curl[^\n]*(--output|-o |-O )", ejecuta)) and "sha256sum --check" not in ejecuta:
                    resultados.append(f"FALLO [suma-verificada] {nombre}: el paso «{descripcion}» descarga un binario sin «sha256sum --check»")
                if re.search(r"--account-key|account-key|--connection-string|--sas-token", ejecuta):
                    resultados.append(f"FALLO [secretos] {nombre}: el paso «{descripcion}» usa llaves de cuenta; use «--auth-mode login»")
            if str(paso.get("uses", "")).startswith("actions/checkout@"):
                if (paso.get("with") or {}).get("persist-credentials") is not False:
                    resultados.append(f"FALLO [checkout] {nombre}: el checkout de «{id_trabajo}» debe declarar persist-credentials: false")
            for origen, entorno in (("paso", paso.get("env")),):
                _revisar_entorno(nombre, entorno, f"{origen} «{descripcion}»", resultados)
        _revisar_entorno(nombre, trabajo.get("env"), f"job «{id_trabajo}»", resultados)
    _revisar_entorno(nombre, workflow.get("env"), "el workflow", resultados)


def _revisar_entorno(nombre, entorno, donde, resultados):
    for variable, valor in (entorno or {}).items():
        if NOMBRE_SECRETO.search(str(variable)) and str(valor) != "" and "${{" not in str(valor):
            resultados.append(f"FALLO [secretos] {nombre}: {variable} en {donde} tiene un valor escrito; debe venir de secrets o generarse")


# --------------------------------------------------------------------------------------------------------------------
def revisar_ci(nombre, texto, workflow, raiz, resultados):
    trabajos = workflow.get("jobs") or {}
    # --- publicar: solo si TODO pasa ---
    publicar = trabajos.get("publicar")
    if publicar is None:
        resultados.append(f"FALLO [publicar] {nombre}: falta el job «publicar»")
    else:
        necesita = publicar.get("needs") or []
        necesita = [necesita] if isinstance(necesita, str) else list(necesita)
        faltan = sorted(set(trabajos) - {"publicar"} - set(necesita))
        if faltan:
            resultados.append(f"FALLO [publicar] {nombre}: «publicar» debe esperar a TODOS los demas jobs; no espera a {faltan}")
        concurrencia = publicar.get("concurrency")
        if not isinstance(concurrencia, dict) or concurrencia.get("cancel-in-progress") is not False:
            resultados.append(f"FALLO [publicar] {nombre}: «publicar» necesita su propia «concurrency» con cancel-in-progress: false (una publicacion a medias dejaria un conjunto incompleto en GHCR)")
        bloque = re.search(r"^  publicar:\n(.*?)(?=^  [A-Za-z0-9_-]+:\n|\Z)", texto, re.MULTILINE | re.DOTALL)
        condicion = re.search(r"^    if:.*?(?=^    [A-Za-z-]+:)", bloque.group(1), re.MULTILINE | re.DOTALL) if bloque else None
        if condicion is None:
            resultados.append(f"FALLO [publicar] {nombre}: «publicar» necesita una condicion «if» (solo main, etiquetas v* o ejecucion manual)")
        elif re.search(r"always\(\)|failure\(\)|cancelled\(\)", condicion.group(0)):
            resultados.append(f"FALLO [publicar] {nombre}: la condicion de «publicar» usa always()/failure()/cancelled(): publicaria aunque algo fallara")
    for id_trabajo, trabajo in trabajos.items():
        if id_trabajo == "publicar":
            continue
        for paso in pasos_de(trabajo):
            if isinstance(paso.get("run"), str) and re.search(r"docker\s+push|docker\s+login|--push", paso["run"]):
                resultados.append(f"FALLO [publicar] {nombre}: el job «{id_trabajo}» publica o inicia sesion en un registro; solo «publicar» puede")
    if any(re.search(r"(^|[:\"' ])latest([\"' ]|$)", l) for l in lineas_de_codigo(texto) if "ubuntu" not in l):
        resultados.append(f"FALLO [sin-latest] {nombre}: no se publican ni se usan etiquetas «latest»")
    # --- contenido que la CI no puede perder ---
    imagen_sql = SQL_IMAGEN
    compose = leer(raiz, "compose.yaml")
    if compose:
        m = re.search(r"image:\s*(mcr\.microsoft\.com/mssql/server:\S+)", compose)
        imagen_sql = m.group(1) if m else SQL_IMAGEN
    exigidos = [
        (imagen_sql, "SQL Server de las pruebas igual al de compose"),
        ("TEST_SQL_PASSWORD=", "clave de SQL Server para las pruebas de integracion"),
        ("Autoprueba del escáner de secretos", "autoprueba del escaner de secretos"),
        ("gitleaks", "analisis de secretos"),
        ("trivy image", "analisis de vulnerabilidades de las imagenes"),
        ("trivy fs", "analisis de vulnerabilidades del repositorio"),
        ("verificar-compose.sh", "verificacion de docker compose"),
        ("scripts/fallos-a-proposito.sh", "comprobacion de que la API sin tzdata se niega a arrancar"),
        ("scripts/presupuesto.mjs", "presupuesto de peso"),
        ("npm run lint", "lint del frontend"),
        ("npm test", "pruebas del frontend"),
        ("npm run build", "construccion del frontend"),
        ("validar-contenedores.py", "validacion estatica de contenedores"),
        ("validar-workflows.py", "validacion estatica del pipeline"),
        ("pip install", "instalacion de PyYAML"),
        ("ci-resumen.py", "resumen y pisos de pruebas"),
        ("dotnet list", "vulnerabilidades de NuGet"),
        ("npm audit", "vulnerabilidades de npm"),
        ("synctime-api", "imagen de la API"),
        ("synctime-web", "imagen del frontend"),
        ("synctime-migrador", "imagen del migrador"),
    ]
    for cadena, descripcion in exigidos:
        if cadena not in texto:
            resultados.append(f"FALLO [ci-contenido] {nombre}: falta {descripcion} (no aparece «{cadena}»)")
    if "--exit-code 42" not in texto:
        resultados.append(f"FALLO [autoprueba-exacta] {nombre}: la autoprueba del escaner debe exigir un codigo de salida EXACTO (--exit-code 42); si no, cualquier error de gitleaks se confunde con una deteccion")
    if len(re.findall(r"dotnet test", texto)) < 2:
        resultados.append(f"FALLO [ci-contenido] {nombre}: deben correr las pruebas unitarias Y las de integracion (dos «dotnet test»)")


def revisar_trabajo_con_azure(nombre, trabajo, resultados):
    """Reglas de los dos workflows que tocan Azure (migrar y respaldo)."""
    if "refs/heads/main" not in str(trabajo.get("if", "")):
        resultados.append(f"FALLO [rama-main] {nombre}: el job debe llevar «if: github.ref == 'refs/heads/main'»: desde una rama sin revisar no se tocan las credenciales reales")
    for variable, valor in (trabajo.get("env") or {}).items():
        if "secrets." in str(valor) and "GITHUB_TOKEN" not in str(valor) or "secrets.AZURE_SQL" in str(valor):
            resultados.append(f"FALLO [secretos-por-paso] {nombre}: {variable} esta en el env de TODO el job; los secretos van solo en el env de los pasos que los usan")
    cierre = next((p for p in pasos_de(trabajo) if "Cerrar el cortafuegos" in str(p.get("name", ""))), None)
    ejecuta = str((cierre or {}).get("run", ""))
    if cierre is not None and not ("::error::" in ejecuta and "exit 1" in ejecuta):
        resultados.append(f"FALLO [cortafuegos] {nombre}: si la regla del cortafuegos no se puede borrar, el job debe quedar en ROJO (::error:: y exit 1), no solo avisar")
    nombres = nombres_de_pasos(trabajo)
    instalar = next((i for i, n in enumerate(nombres) if "Instalar sqlpackage" in n), None)
    iniciar = next((i for i, p in enumerate(pasos_de(trabajo)) if str(p.get("uses", "")).startswith("azure/login@")), None)
    if instalar is not None and iniciar is not None and instalar > iniciar:
        resultados.append(f"FALLO [secretos-por-paso] {nombre}: sqlpackage se instala DESPUES de iniciar sesion en Azure; instale primero lo que baja codigo de internet")


def revisar_migrar(nombre, texto, workflow, resultados):
    trabajo = (workflow.get("jobs") or {}).get("migrar")
    if trabajo is None:
        resultados.append(f"FALLO [migrar] {nombre}: falta el job «migrar»")
        return
    revisar_trabajo_con_azure(nombre, trabajo, resultados)
    if "org.opencontainers.image.revision" not in texto:
        resultados.append(f"FALLO [migrar] {nombre}: antes de migrar hay que comprobar que la imagen descargada sea del commit pedido (etiqueta org.opencontainers.image.revision)")
    if not trabajo.get("environment"):
        resultados.append(f"FALLO [migrar] {nombre}: el job debe usar un «environment» protegido (pide la aprobacion de una persona)")
    entradas = ((workflow.get("on") or {}).get("workflow_dispatch") or {}).get("inputs") or {}
    for entrada in ("confirmar", "sha"):
        if entrada not in entradas:
            resultados.append(f"FALLO [migrar] {nombre}: falta la entrada «{entrada}» (se escribe «MIGRAR» y el commit exacto)")
    if '"MIGRAR"' not in texto:
        resultados.append(f"FALLO [migrar] {nombre}: no se exige escribir MIGRAR para continuar")
    if not workflow.get("concurrency") and not trabajo.get("concurrency"):
        resultados.append(f"FALLO [migrar] {nombre}: dos migraciones al mismo tiempo no pueden ocurrir; falta «concurrency»")
    nombres = nombres_de_pasos(trabajo)

    def indice(parte):
        return next((i for i, n in enumerate(nombres) if parte in n), None)

    respaldo, aplicar, comprobar, cierre = indice("Respaldo previo"), indice("Aplicar las migraciones"), indice("Comprobación posterior"), indice("Cerrar el cortafuegos")
    for parte, valor in (("Respaldo previo", respaldo), ("Aplicar las migraciones", aplicar), ("Comprobación posterior", comprobar), ("Cerrar el cortafuegos", cierre)):
        if valor is None:
            resultados.append(f"FALLO [migrar] {nombre}: falta el paso «{parte}»")
    if None not in (respaldo, aplicar, comprobar):
        if not respaldo < aplicar < comprobar:
            resultados.append(f"FALLO [migrar] {nombre}: el orden debe ser respaldo previo, migracion y comprobacion posterior")
    if cierre is not None and "always()" not in str(pasos_de(trabajo)[cierre].get("if", "")):
        resultados.append(f"FALLO [migrar] {nombre}: el cortafuegos de Azure SQL debe cerrarse SIEMPRE («if: always()»), tambien si algo falla")
    if not any(str(p.get("uses", "")).startswith("azure/login@") for p in pasos_de(trabajo)):
        resultados.append(f"FALLO [migrar] {nombre}: falta el inicio de sesion en Azure (azure/login)")


def revisar_respaldo(nombre, texto, workflow, resultados):
    trabajo = (workflow.get("jobs") or {}).get("respaldar")
    if trabajo is None:
        resultados.append(f"FALLO [respaldo] {nombre}: falta el job «respaldar»")
        return
    revisar_trabajo_con_azure(nombre, trabajo, resultados)
    cron = (workflow.get("on") or {}).get("schedule") if isinstance(workflow.get("on"), dict) else None
    if not cron or not cron[0].get("cron"):
        resultados.append(f"FALLO [disparadores] {nombre}: el respaldo diario necesita un «schedule» con cron")
    entorno = workflow.get("env") or {}
    if str(entorno.get("RETENCION_DIAS")) != "30":
        resultados.append(f"FALLO [respaldo] {nombre}: la retencion debe ser de 30 dias (RETENCION_DIAS: \"30\"; RNFP09)")
    if entorno.get("CONTENEDOR") != "respaldos":
        resultados.append(f"FALLO [respaldo] {nombre}: el contenedor de Blob Storage debe ser el privado «respaldos»")
    if "respaldos" not in texto or re.search(r"--public-access\s+(blob|container)", texto):
        resultados.append(f"FALLO [respaldo] {nombre}: el contenedor de respaldos no puede ser publico")
    if not workflow.get("concurrency") and not trabajo.get("concurrency"):
        resultados.append(f"FALLO [respaldo] {nombre}: dos respaldos al mismo tiempo no pueden ocurrir; falta «concurrency»")
    nombres = nombres_de_pasos(trabajo)
    cierre = next((i for i, n in enumerate(nombres) if "Cerrar el cortafuegos" in n), None)
    if cierre is None or "always()" not in str(pasos_de(trabajo)[cierre].get("if", "")):
        resultados.append(f"FALLO [respaldo] {nombre}: el cortafuegos de Azure SQL debe cerrarse SIEMPRE («if: always()»)")
    for parte in ("Exportar la base", "Subir el respaldo", "Verificar el respaldo", "Borrar respaldos de mas de"):
        if not any(parte in n for n in nombres):
            resultados.append(f"FALLO [respaldo] {nombre}: falta el paso «{parte}»")
    if not any(str(p.get("uses", "")).startswith("azure/login@") for p in pasos_de(trabajo)):
        resultados.append(f"FALLO [respaldo] {nombre}: falta el inicio de sesion en Azure (azure/login)")


def revisar_escaneres(raiz, resultados):
    trivy = leer(raiz, ".trivyignore")
    if trivy is None:
        resultados.append("FALLO [trivyignore] falta .trivyignore (aunque este vacio: es donde se documentan las excepciones)")
    else:
        for linea in trivy.splitlines():
            if linea.strip() and not linea.strip().startswith("#") and not re.search(r"\bexp:\d{4}-\d{2}-\d{2}\b", linea):
                resultados.append(f"FALLO [trivyignore] «{linea.strip()}» no tiene fecha de vencimiento (exp:AAAA-MM-DD): una excepcion no puede ser eterna")
    gitleaks = leer(raiz, ".gitleaks.toml")
    if gitleaks is None or not re.search(r"^\s*useDefault\s*=\s*true", gitleaks, re.MULTILINE):
        resultados.append("FALLO [gitleaks] .gitleaks.toml debe extender las reglas por omision (useDefault = true)")
    dependabot = leer(raiz, ".github/dependabot.yml")
    if dependabot is None or "github-actions" not in dependabot:
        resultados.append("FALLO [dependabot] falta .github/dependabot.yml con el ecosistema github-actions (mantiene al dia los SHA fijados)")
    if leer(raiz, "scripts/ci-resumen.py") is None:
        resultados.append("FALLO [ci-contenido] falta scripts/ci-resumen.py")


def revisar_pruebas_del_backend(raiz, resultados):
    """Las pruebas no pueden depender de la hora del equipo: en un contenedor o en CI el reloj esta en UTC y «hoy» cambia entre 20:00 y 24:00 de Chile."""
    patron = re.compile(r"\bDateTime(Offset)?\.(Now|Today)\b")
    for carpeta in (f"{API}/LetrerosHerreraSYNCTIMEapi.Tests", f"{API}/LetrerosHerreraSYNCTIMEapi.UnitTests"):
        base = raiz / carpeta
        if not base.exists():
            continue
        for ruta in sorted(base.rglob("*.cs")):
            if any(parte in ("bin", "obj") for parte in ruta.parts):
                continue
            for numero, linea in enumerate(ruta.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
                codigo = linea.split("//", 1)[0]
                if patron.search(codigo):
                    resultados.append(f"FALLO [hora-de-pruebas] {ruta.relative_to(raiz).as_posix()}:{numero} usa la hora del equipo; use HoraChile.Convertir(DateTimeOffset.UtcNow)")


def validar(raiz):
    raiz = Path(raiz)
    resultados = []
    carpeta = raiz / CARPETA_WORKFLOWS
    esperados = ["ci.yml", "migrar.yml", "respaldo.yml"]
    for archivo in esperados:
        relativo = f"{CARPETA_WORKFLOWS}/{archivo}"
        texto = leer(raiz, relativo)
        if texto is None:
            resultados.append(f"FALLO [workflow] falta {relativo}")
            continue
        try:
            workflow = cargar_workflow(texto)
        except yaml.YAMLError as error:
            resultados.append(f"FALLO [yaml] {relativo} no se puede leer: {str(error).splitlines()[0]}")
            continue
        if not isinstance(workflow, dict) or not workflow.get("jobs"):
            resultados.append(f"FALLO [workflow] {relativo} no define jobs")
            continue
        revisar_texto_comun(relativo, texto, resultados)
        revisar_workflow(relativo, texto, workflow, resultados)
        if archivo == "ci.yml":
            revisar_ci(relativo, texto, workflow, raiz, resultados)
        elif archivo == "migrar.yml":
            revisar_migrar(relativo, texto, workflow, resultados)
        else:
            revisar_respaldo(relativo, texto, workflow, resultados)
    if carpeta.exists():
        for extra in sorted(carpeta.glob("*.y*ml")):
            if extra.name not in esperados:
                resultados.append(f"FALLO [workflow] {extra.relative_to(raiz).as_posix()} no esta revisado por este validador; agreguelo a DISPARADORES y a las reglas")
    revisar_escaneres(raiz, resultados)
    for relativo in (".github/dependabot.yml", ".gitleaks.toml", ".trivyignore", "scripts/ci-resumen.py"):
        texto = leer(raiz, relativo)
        if texto:
            revisar_texto_comun(relativo, texto, resultados)
    revisar_pruebas_del_backend(raiz, resultados)
    if not resultados:
        resultados.append("OK todas las reglas del pipeline se cumplen")
    return resultados


if __name__ == "__main__":
    raiz_argumento = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent
    salida = validar(raiz_argumento)
    for linea in salida:
        print(linea)
    sys.exit(1 if any(linea.startswith("FALLO") for linea in salida) else 0)
