"""Fase 16B (RNFO15): pruebas del validador de workflows (scripts/validar-workflows.py).

Se escribieron ANTES del validador y de los workflows. Cada prueba copia los archivos reales a una carpeta temporal, los ESTROPEA a
proposito (el «fallo a proposito» del plan) y comprueba que el validador lo detecta y nombra la regla. La primera comprueba que los
archivos reales pasan. Uso:  python scripts/test_validar_workflows.py   (necesita PyYAML)
"""
import importlib.util
import re
import shutil
import tempfile
import unittest
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
API = "backend/Proyecto-Carteles-herrera-synctime-main"
CI = ".github/workflows/ci.yml"
MIGRAR = ".github/workflows/migrar.yml"
RESPALDO = ".github/workflows/respaldo.yml"
ARCHIVOS = [CI, MIGRAR, RESPALDO, ".github/dependabot.yml", ".gitleaks.toml", ".trivyignore", "scripts/ci-resumen.py"]
# Una prueba de integracion cualquiera, para la regla de «sin DateTime.Now».
PRUEBA_BACKEND = f"{API}/LetrerosHerreraSYNCTIMEapi.Tests/FlujoNegocioTests.cs"


def cargar_validador():
    ruta = RAIZ / "scripts" / "validar-workflows.py"
    especificacion = importlib.util.spec_from_file_location("validar_workflows", ruta)
    modulo = importlib.util.module_from_spec(especificacion)
    especificacion.loader.exec_module(modulo)
    return modulo


class ValidadorDeWorkflows(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.validador = cargar_validador()

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="validar-workflows-"))
        for relativo in [*ARCHIVOS, PRUEBA_BACKEND]:
            destino = self.carpeta / relativo
            destino.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(RAIZ / relativo, destino)

    def tearDown(self):
        shutil.rmtree(self.carpeta, ignore_errors=True)

    def cambiar(self, relativo, patron, reemplazo, veces=1):
        """Sustituye con una expresion regular. Falla si el patron ya no existe: asi una prueba desactualizada no pasa en falso."""
        ruta = self.carpeta / relativo
        texto = ruta.read_text(encoding="utf-8")
        nuevo, cantidad = re.subn(patron, reemplazo, texto, count=veces, flags=re.MULTILINE | re.DOTALL)
        self.assertGreater(cantidad, 0, f"la prueba esta desactualizada: {relativo} ya no contiene {patron!r}")
        ruta.write_text(nuevo, encoding="utf-8")

    def agregar(self, relativo, texto):
        ruta = self.carpeta / relativo
        ruta.write_text(ruta.read_text(encoding="utf-8") + texto, encoding="utf-8")

    def fallos(self):
        return [f for f in self.validador.validar(self.carpeta) if f.startswith("FALLO")]

    def assertDetecta(self, regla):
        fallos = self.fallos()
        self.assertTrue(any(f"[{regla}]" in f for f in fallos), f"no se detecto [{regla}]; fallos: {fallos}")

    def test_los_archivos_reales_pasan(self):
        self.assertEqual([], self.fallos())

    # --- Acciones de terceros ---
    def test_accion_fijada_por_etiqueta_en_vez_de_sha(self):
        self.cambiar(CI, r"actions/checkout@[0-9a-f]{40} # v[0-9.]+", "actions/checkout@v7")
        self.assertDetecta("accion-fijada")

    def test_accion_fijada_por_rama(self):
        self.cambiar(CI, r"actions/checkout@[0-9a-f]{40} # v[0-9.]+", "actions/checkout@main")
        self.assertDetecta("accion-fijada")

    def test_accion_con_sha_pero_sin_comentario_de_version(self):
        self.cambiar(CI, r"(actions/setup-python@[0-9a-f]{40}) # v[0-9.]+", r"\1")
        self.assertDetecta("accion-fijada")

    def test_accion_con_sha_corto(self):
        self.cambiar(CI, r"actions/checkout@([0-9a-f]{7})[0-9a-f]{33} # v[0-9.]+", r"actions/checkout@\1 # v7.0.1")
        self.assertDetecta("accion-fijada")

    # --- Permisos ---
    def test_sin_permisos_declarados(self):
        self.cambiar(CI, r"^permissions:\n  contents: read\n", "")
        self.assertDetecta("permisos")

    def test_permisos_write_all(self):
        self.cambiar(CI, r"^permissions:\n  contents: read\n", "permissions: write-all\n")
        self.assertDetecta("permisos")

    def test_un_job_de_pruebas_con_permiso_de_escritura(self):
        self.cambiar(CI, r"(  backend:\n    name:[^\n]*\n)", r"\1    permissions:\n      contents: read\n      packages: write\n")
        self.assertDetecta("permisos")

    def test_el_trabajo_de_migracion_pide_mas_que_id_token(self):
        self.cambiar(MIGRAR, r"(permissions:\n      id-token: write)", r"\1\n      actions: write")
        self.assertDetecta("permisos")

    # --- Disparadores ---
    def test_pull_request_target(self):
        self.cambiar(CI, r"^on:\n", "on:\n  pull_request_target:\n")
        self.assertDetecta("disparadores")

    def test_la_migracion_se_dispara_sola(self):
        self.cambiar(MIGRAR, r"^on:\n", "on:\n  push:\n")
        self.assertDetecta("disparadores")

    def test_el_respaldo_no_esta_programado(self):
        self.cambiar(RESPALDO, r"  schedule:\n    - cron: [^\n]*\n", "")
        self.assertDetecta("disparadores")

    def test_la_ci_se_programa_sola(self):
        self.cambiar(CI, r"^on:\n", "on:\n  schedule:\n    - cron: '0 3 * * *'\n")
        self.assertDetecta("disparadores")

    # --- La publicacion solo ocurre si todo pasa ---
    def test_publicar_no_espera_al_compose(self):
        self.cambiar(CI, r"(  publicar:\n.*?    needs: \[)([^\]]*?), compose\]", r"\1\2]")
        self.assertDetecta("publicar")

    def test_publicar_no_espera_a_los_secretos(self):
        self.cambiar(CI, r"(  publicar:\n.*?    needs: \[[^\]]*?)secretos, ", r"\1")
        self.assertDetecta("publicar")

    def test_publicar_se_ejecuta_aunque_algo_falle(self):
        self.cambiar(CI, r"(  publicar:\n.*?    if: )>-\n", r"\1always() && >-\n")
        self.assertDetecta("publicar")

    def test_otro_job_publica_imagenes(self):
        self.cambiar(CI, r"(  frontend:\n.*?    steps:\n)", r"\1      - run: docker push ghcr.io/x/y:z\n")
        self.assertDetecta("publicar")

    def test_la_publicacion_usa_latest(self):
        self.cambiar(CI, r'etiquetas=\("sha-\$\{corto\}"\)', 'etiquetas=("sha-${corto}" "latest")')
        self.assertDetecta("sin-latest")

    # --- Secretos y registros ---
    def test_secreto_escrito_en_el_entorno_de_un_job(self):
        self.cambiar(CI, r"(  backend:\n    name:[^\n]*\n)", r"\1    env:\n      MSSQL_SA_PASSWORD: hunter2hunter2\n")
        self.assertDetecta("secretos")

    def test_token_de_github_escrito_en_un_archivo(self):
        # Se arma en la prueba para que este archivo no contenga un secreto completo.
        self.agregar(CI, "\n# ghp_" + "a1B2c3D4e5F6" * 3 + "\n")
        self.assertDetecta("secretos")

    def test_clave_privada_escrita_en_un_archivo(self):
        self.agregar(RESPALDO, "\n# -----BEGIN " + "RSA PRIVATE KEY-----\n")
        self.assertDetecta("secretos")

    def test_la_clave_aleatoria_se_escribe_sin_enmascarar(self):
        self.cambiar(CI, r'\n[^\n]*::add-mask::[^\n]*', "")
        self.assertDetecta("mascara")

    def test_una_expresion_dentro_de_run_permite_inyeccion(self):
        self.cambiar(CI, r"(  estatica:\n.*?    steps:\n)", r"\1      - run: echo ${{ github.event.head_commit.message }}\n")
        self.assertDetecta("inyeccion")

    def test_una_entrada_manual_dentro_de_run(self):
        self.cambiar(MIGRAR, r"(  migrar:\n.*?    steps:\n)", r"\1      - run: echo ${{ inputs.sha }}\n")
        self.assertDetecta("inyeccion")

    def test_checkout_guarda_credenciales(self):
        self.cambiar(CI, r"persist-credentials: false", "persist-credentials: true")
        self.assertDetecta("checkout")

    def test_checkout_sin_decidir_credenciales(self):
        self.cambiar(CI, r"(actions/checkout@[0-9a-f]{40} # v[0-9.]+)\n        with:\n          persist-credentials: false\n", r"\1\n")
        self.assertDetecta("checkout")

    # --- Robustez ---
    def test_un_job_sin_limite_de_tiempo(self):
        self.cambiar(CI, r"(  estatica:\n.*?)    timeout-minutes: \d+\n", r"\1")
        self.assertDetecta("timeout")

    def test_ejecutor_flotante(self):
        self.cambiar(CI, r"runs-on: ubuntu-24.04", "runs-on: ubuntu-latest")
        self.assertDetecta("ejecutor")

    def test_descarga_de_un_binario_sin_verificar_la_suma(self):
        self.cambiar(CI, r"[^\n]*sha256sum --check[^\n]*\n", "", veces=1)
        self.assertDetecta("suma-verificada")

    # --- Contenido que la CI no puede perder ---
    def test_sin_autoprueba_del_escaner_de_secretos(self):
        self.cambiar(CI, r"Autoprueba del escáner de secretos", "Otra cosa")
        self.assertDetecta("ci-contenido")

    def test_las_pruebas_de_integracion_sin_la_clave_de_sql(self):
        self.cambiar(CI, r"TEST_SQL_PASSWORD=", "OTRA_VARIABLE=")
        self.assertDetecta("ci-contenido")

    def test_la_ci_no_verifica_compose(self):
        self.cambiar(CI, r"verificar-compose\.sh", "otra-cosa.sh")
        self.assertDetecta("ci-contenido")

    def test_la_ci_no_comprueba_que_la_api_sin_tzdata_se_niega_a_arrancar(self):
        self.cambiar(CI, r"scripts/fallos-a-proposito\.sh", "scripts/otra-cosa.sh")
        self.assertDetecta("ci-contenido")

    def test_publicar_no_espera_al_fallo_a_proposito_de_tzdata(self):
        self.cambiar(CI, r"(  publicar:\n.*?    needs: \[[^\]]*?)fallo-tzdata, ", r"\1")
        self.assertDetecta("publicar")

    def test_la_ci_no_corre_el_presupuesto_de_peso(self):
        self.cambiar(CI, r"scripts/presupuesto\.mjs", "scripts/otra.mjs")
        self.assertDetecta("ci-contenido")

    def test_la_ci_no_analiza_vulnerabilidades(self):
        self.cambiar(CI, r"trivy image", "echo image")
        self.assertDetecta("ci-contenido")

    def test_sql_server_distinto_al_de_compose(self):
        self.cambiar(CI, r"mssql/server:2022-CU28-ubuntu-22\.04", "mssql/server:2019-latest")
        self.assertDetecta("ci-contenido")

    # --- Migracion manual con aprobacion ---
    def test_la_migracion_sin_entorno_protegido(self):
        self.cambiar(MIGRAR, r"    environment: [^\n]*\n", "")
        self.assertDetecta("migrar")

    def test_la_migracion_sin_confirmacion_escrita(self):
        self.cambiar(MIGRAR, r"      confirmar:\n", "      confirmacion:\n")
        self.assertDetecta("migrar")

    def test_la_migracion_sin_respaldo_previo(self):
        self.cambiar(MIGRAR, r"Respaldo previo", "Otra cosa")
        self.assertDetecta("migrar")

    def test_la_migracion_no_cierra_el_cortafuegos_si_falla(self):
        self.cambiar(MIGRAR, r"(Cerrar el cortafuegos[^\n]*\n\s+if: )always\(\)", r"\1success()")
        self.assertDetecta("migrar")

    def test_la_migracion_sin_comprobacion_posterior(self):
        self.cambiar(MIGRAR, r"Comprobación posterior", "Otra cosa")
        self.assertDetecta("migrar")

    # --- Revision de seguridad de ECC (16B) ---
    def test_la_migracion_puede_lanzarse_desde_cualquier_rama(self):
        self.cambiar(MIGRAR, r"    if: github\.ref == 'refs/heads/main'\n", "")
        self.assertDetecta("rama-main")

    def test_el_respaldo_puede_correr_desde_cualquier_rama(self):
        self.cambiar(RESPALDO, r"    if: github\.ref == 'refs/heads/main'\n", "")
        self.assertDetecta("rama-main")

    def test_el_cortafuegos_que_no_se_borra_solo_avisa(self):
        self.cambiar(MIGRAR, r'\|\| \{ echo "::error::[^\n]*exit 1; \}', '|| echo "::warning::no se borro"')
        self.assertDetecta("cortafuegos")

    def test_el_respaldo_deja_el_cortafuegos_abierto_sin_avisar(self):
        self.cambiar(RESPALDO, r'\|\| \{ echo "::error::[^\n]*exit 1; \}', '|| echo "::warning::no se borro"')
        self.assertDetecta("cortafuegos")

    def test_la_autoprueba_acepta_cualquier_error_como_deteccion(self):
        self.cambiar(CI, r"--exit-code 42", "--exit-code 1")
        self.assertDetecta("autoprueba-exacta")

    def test_un_secreto_en_el_entorno_de_todo_el_job_de_migracion(self):
        self.cambiar(MIGRAR, r"(    env:\n      API_DIR:[^\n]*\n)", r"\1      SQL_PASSWORD: ${{ secrets.AZURE_SQL_MIGRACION_PASSWORD }}\n")
        self.assertDetecta("secretos-por-paso")

    def test_un_secreto_en_el_entorno_de_todo_el_job_de_respaldo(self):
        self.cambiar(RESPALDO, r"(    env:\n      GRUPO:[^\n]*\n)", r"\1      SQL_PASSWORD: ${{ secrets.AZURE_SQL_RESPALDO_PASSWORD }}\n")
        self.assertDetecta("secretos-por-paso")

    def test_el_migrador_se_usa_sin_comprobar_que_sea_del_commit(self):
        self.cambiar(MIGRAR, r"org\.opencontainers\.image\.revision", "otra.etiqueta")
        self.assertDetecta("migrar")

    def test_la_publicacion_puede_cancelarse_a_la_mitad(self):
        self.cambiar(CI, r"(  publicar:\n.*?)    concurrency:\n      group: publicar-ghcr\n      cancel-in-progress: false\n", r"\1")
        self.assertDetecta("publicar")

    # --- Respaldo diario de 30 dias ---
    def test_respaldo_con_retencion_distinta_de_30_dias(self):
        self.cambiar(RESPALDO, r'RETENCION_DIAS: "30"', 'RETENCION_DIAS: "7"')
        self.assertDetecta("respaldo")

    def test_respaldo_sin_contenedor_privado(self):
        self.cambiar(RESPALDO, r"respaldos", "publico", veces=0)
        self.assertDetecta("respaldo")

    def test_respaldo_sin_limpiar_el_cortafuegos(self):
        self.cambiar(RESPALDO, r"(Cerrar el cortafuegos[^\n]*\n\s+if: )always\(\)", r"\1success()")
        self.assertDetecta("respaldo")

    # --- Escaneres ---
    def test_trivyignore_sin_fecha_de_vencimiento(self):
        self.agregar(".trivyignore", "\nCVE-2099-0001\n")
        self.assertDetecta("trivyignore")

    def test_gitleaks_sin_las_reglas_por_omision(self):
        self.cambiar(".gitleaks.toml", r"useDefault = true", "useDefault = false")
        self.assertDetecta("gitleaks")

    # --- Pruebas del backend ---
    def test_prueba_del_backend_con_datetime_now(self):
        self.agregar(PRUEBA_BACKEND, "\n// var ahora = DateTime.Now;\nclass Mala { object o = DateTime.Now; }\n")
        self.assertDetecta("hora-de-pruebas")


if __name__ == "__main__":
    unittest.main(verbosity=1)
