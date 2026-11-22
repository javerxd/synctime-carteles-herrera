"""Fase 16A: pruebas del validador de contenedores (scripts/validar-contenedores.py).

Se escribieron ANTES del validador. Cada prueba copia los archivos reales a una carpeta temporal, los ESTROPEA a proposito
(el «fallo a proposito» del plan) y comprueba que el validador lo detecta y nombra la regla. La primera comprueba que los archivos
reales pasan. Uso:  python scripts/test_validar_contenedores.py
"""
import importlib.util
import shutil
import tempfile
import unittest
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
ARCHIVOS = [
    "compose.yaml", "render.yaml", ".env.example", ".gitignore",
    "backend/Proyecto-Carteles-herrera-synctime-main/Dockerfile",
    "backend/Proyecto-Carteles-herrera-synctime-main/.dockerignore",
    "backend/Proyecto-Carteles-herrera-synctime-main/entrada.sh",
    "backend/Proyecto-Carteles-herrera-synctime-main/construir-bundle.sh",
    "frontend/synctimefront/Dockerfile",
    "frontend/synctimefront/.dockerignore",
    "frontend/synctimefront/vercel.json",
    "frontend/synctimefront/nginx/default.conf.template",
    "frontend/synctimefront/nginx/cabeceras-seguridad.conf",
]
API = "backend/Proyecto-Carteles-herrera-synctime-main"
WEB = "frontend/synctimefront"


def cargar_validador():
    ruta = RAIZ / "scripts" / "validar-contenedores.py"
    especificacion = importlib.util.spec_from_file_location("validar_contenedores", ruta)
    modulo = importlib.util.module_from_spec(especificacion)
    especificacion.loader.exec_module(modulo)
    return modulo


class ValidadorDeContenedores(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.validador = cargar_validador()

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="validar-contenedores-"))
        for relativo in ARCHIVOS:
            destino = self.carpeta / relativo
            destino.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(RAIZ / relativo, destino)

    def tearDown(self):
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def estropear(self, relativo, antes, despues):
        ruta = self.carpeta / relativo
        texto = ruta.read_text(encoding="utf-8")
        self.assertIn(antes, texto, f"la prueba esta desactualizada: {relativo} ya no contiene {antes!r}")
        ruta.write_text(texto.replace(antes, despues, 1), encoding="utf-8")

    def fallos(self):
        return [f for f in self.validador.validar(self.carpeta) if f.startswith("FALLO")]

    def assertDetecta(self, regla):
        fallos = self.fallos()
        self.assertTrue(any(regla in f for f in fallos), f"no se detecto {regla}; fallos: {fallos}")

    def test_los_archivos_reales_pasan(self):
        self.assertEqual([], self.fallos())

    # --- Dockerfile ---
    def test_imagen_base_con_latest(self):
        self.estropear(f"{WEB}/Dockerfile", "node:24.22.3-alpine3.24", "node:latest")
        self.assertDetecta("imagen-fija")

    def test_imagen_base_sin_etiqueta(self):
        self.estropear(f"{API}/Dockerfile", "dotnet/aspnet:10.0.15-noble", "dotnet/aspnet")
        self.assertDetecta("imagen-fija")

    def test_sin_healthcheck_en_la_api(self):
        self.estropear(f"{API}/Dockerfile", "HEALTHCHECK", "# HEALTHCHECK")
        self.assertDetecta("healthcheck")

    def test_la_api_corre_como_root(self):
        self.estropear(f"{API}/entrada.sh", "exec setpriv", "exec env")
        self.assertDetecta("sin-privilegios")

    def test_el_frontend_corre_como_root(self):
        self.estropear(f"{WEB}/Dockerfile", "USER nginx", "USER root")
        self.assertDetecta("sin-privilegios")

    def test_sin_tzdata_en_la_imagen(self):
        self.estropear(f"{API}/Dockerfile", "curl tzdata", "curl")
        self.assertDetecta("tzdata")

    # Fase 16B: la primera ejecucion de la CI mostro que el migrador no se construia («No se configuro Jwt:Key»): la herramienta de EF arma la
    # API y esta exige su configuracion. Estas pruebas impiden que el script de construccion pierda esa configuracion.
    def test_el_bundle_se_construye_sin_el_script_de_diseno(self):
        self.estropear(f"{API}/Dockerfile", "RUN sh /usr/local/bin/construir-bundle.sh", "RUN dotnet ef migrations bundle")
        self.assertDetecta("bundle-config")

    def test_el_script_del_bundle_sin_la_clave_jwt(self):
        self.estropear(f"{API}/construir-bundle.sh", "export Jwt__Audience=synctime\n", "")
        self.assertDetecta("bundle-config")

    def test_el_script_del_bundle_sin_cors(self):
        self.estropear(f"{API}/construir-bundle.sh", "export Cors__AllowedOrigins__0=http://localhost\n", "")
        self.assertDetecta("bundle-config")

    def test_el_script_del_bundle_con_la_clave_jwt_escrita(self):
        self.estropear(f"{API}/construir-bundle.sh", 'Jwt__Key="$(head -c 48 /dev/urandom | base64 | tr -d \'\\n\')"', "Jwt__Key=una-clave-escrita-a-mano-0123456789")
        self.assertDetecta("bundle-config")

    # Fase 16B: la CI encontro 14 vulnerabilidades altas o criticas en los paquetes de Alpine de la imagen de nginx y no hay una etiqueta mas nueva.
    def test_el_frontend_sin_actualizar_los_paquetes_de_alpine(self):
        self.estropear(f"{WEB}/Dockerfile", "RUN apk upgrade --no-cache\n", "")
        self.assertDetecta("parches-base")

    def test_el_frontend_que_se_queda_como_root_tras_actualizar(self):
        self.estropear(f"{WEB}/Dockerfile", "USER nginx", "USER root")
        self.assertDetecta("sin-privilegios")

    def test_secreto_escrito_en_el_dockerfile(self):
        self.estropear(f"{WEB}/Dockerfile", "ENV API_UPSTREAM", "ENV JWT_KEY=abcdef123456 \\\n    API_UPSTREAM")
        self.assertDetecta("secretos")

    def test_dockerignore_deja_pasar_el_env(self):
        self.estropear(f"{WEB}/.dockerignore", ".env\n", "")
        self.assertDetecta("dockerignore")

    # --- compose ---
    def test_compose_con_imagen_latest(self):
        self.estropear("compose.yaml", "mssql/server:2022-CU28-ubuntu-22.04", "mssql/server:latest")
        self.assertDetecta("imagen-fija")

    def test_compose_publica_la_base_de_datos(self):
        self.estropear("compose.yaml", "    volumes:\n      - sql-datos:/var/opt/mssql\n", "    ports:\n      - \"127.0.0.1:1433:1433\"\n    volumes:\n      - sql-datos:/var/opt/mssql\n")
        self.assertDetecta("puertos")

    def test_compose_publica_el_sitio_sin_limitar_a_localhost(self):
        self.estropear("compose.yaml", '"127.0.0.1:${WEB_PORT:-8080}:8080"', '"${WEB_PORT:-8080}:8080"')
        self.assertDetecta("puertos")

    def test_compose_con_clave_escrita_a_mano(self):
        self.estropear("compose.yaml", "Jwt__Key: ${JWT_KEY:?Falta JWT_KEY - ejecute scripts/crear-env.sh}", "Jwt__Key: una-clave-escrita-a-mano-0123456789")
        self.assertDetecta("secretos")

    def test_el_migrador_se_reinicia(self):
        self.estropear("compose.yaml", "    # Un solo uso: aplica las migraciones y termina. No se reinicia.\n    restart: \"no\"", "    restart: unless-stopped")
        self.assertDetecta("migrador")

    def test_la_api_no_espera_al_migrador(self):
        self.estropear("compose.yaml", "      migrador:\n        condition: service_completed_successfully\n      semilla:", "      semilla:")
        self.assertDetecta("migrador")

    def test_compose_con_socket_de_docker(self):
        self.estropear("compose.yaml", "      - almacen-api:/app/almacen\n    networks: [interna]\n    healthcheck:",
                       "      - almacen-api:/app/almacen\n      - /var/run/docker.sock:/var/run/docker.sock\n    networks: [interna]\n    healthcheck:")
        self.assertDetecta("privilegios")

    def test_compose_con_yaml_roto(self):
        self.estropear("compose.yaml", "services:\n  sql:", "services:\n sql:\n    - roto: [")
        self.assertDetecta("yaml")

    # --- .env.example y .gitignore ---
    def test_env_example_con_un_secreto(self):
        self.estropear(".env.example", "JWT_KEY=\n", "JWT_KEY=ZXhhbXBsZXNlY3JldGtleWV4YW1wbGU=\n")
        self.assertDetecta("secretos")

    def test_gitignore_deja_pasar_el_env(self):
        ruta = self.carpeta / ".gitignore"
        lineas = [linea for linea in ruta.read_text(encoding="utf-8").splitlines() if linea.strip() != ".env"]
        ruta.write_text("\n".join(lineas) + "\n", encoding="utf-8")
        self.assertDetecta("gitignore")

    # --- render.yaml ---
    def test_render_con_un_secreto_escrito(self):
        self.estropear("render.yaml", "      - key: Jwt__Key\n        sync: false", "      - key: Jwt__Key\n        value: una-clave-escrita-a-mano")
        self.assertDetecta("render-secretos")

    def test_render_vigila_la_base_en_cada_chequeo(self):
        self.estropear("render.yaml", "healthCheckPath: /salud\n", "healthCheckPath: /salud/lista\n")
        self.assertDetecta("render")

    # --- vercel.json y nginx ---
    def test_vercel_con_cross_origin_opener_policy(self):
        self.estropear(f"{WEB}/vercel.json", '{ "key": "X-Frame-Options", "value": "DENY" },',
                       '{ "key": "X-Frame-Options", "value": "DENY" },\n        { "key": "Cross-Origin-Opener-Policy", "value": "same-origin" },')
        self.assertDetecta("coop")

    def test_vercel_sin_cache_larga_en_assets(self):
        self.estropear(f"{WEB}/vercel.json", "public, max-age=31536000, immutable", "public, max-age=60")
        self.assertDetecta("cache")

    def test_vercel_sin_reescritura_de_la_api(self):
        self.estropear(f"{WEB}/vercel.json", '"source": "/api/:path*"', '"source": "/otra/:path*"')
        self.assertDetecta("reescritura")

    def test_nginx_sin_cache_larga_en_assets(self):
        self.estropear(f"{WEB}/nginx/default.conf.template", "max-age=31536000, immutable", "max-age=60")
        self.assertDetecta("cache")

    def test_nginx_cambia_el_csp_y_deja_de_coincidir_con_vercel(self):
        self.estropear(f"{WEB}/nginx/cabeceras-seguridad.conf", "object-src 'none'", "object-src 'self'")
        self.assertDetecta("csp-igual")

    def test_nginx_con_coop(self):
        self.estropear(f"{WEB}/nginx/cabeceras-seguridad.conf", 'add_header X-Frame-Options "DENY" always;',
                       'add_header X-Frame-Options "DENY" always;\nadd_header Cross-Origin-Opener-Policy "same-origin" always;')
        self.assertDetecta("coop")


if __name__ == "__main__":
    unittest.main(verbosity=2)
