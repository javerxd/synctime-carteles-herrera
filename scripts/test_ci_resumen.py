"""Fase 16B (RNFO15): pruebas de scripts/ci-resumen.py, el que usa el pipeline para decidir si un resultado de pruebas o de
vulnerabilidades deja pasar. Se escribieron ANTES del script. Uso:  python scripts/test_ci_resumen.py   (solo biblioteca estandar)
"""
import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent


def cargar():
    ruta = RAIZ / "scripts" / "ci-resumen.py"
    especificacion = importlib.util.spec_from_file_location("ci_resumen", ruta)
    modulo = importlib.util.module_from_spec(especificacion)
    especificacion.loader.exec_module(modulo)
    return modulo


TRX = """<?xml version="1.0" encoding="UTF-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="Completed">
    <Counters total="{total}" executed="{ejecutadas}" passed="{pasadas}" failed="{falladas}" error="0" notExecuted="{no_ejecutadas}" />
  </ResultSummary>
</TestRun>
"""


class CiResumen(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.modulo = cargar()

    def setUp(self):
        self.carpeta = Path(tempfile.mkdtemp(prefix="ci-resumen-"))
        self.addCleanup(lambda: __import__("shutil").rmtree(self.carpeta, ignore_errors=True))

    def escribir(self, nombre, texto):
        ruta = self.carpeta / nombre
        ruta.write_text(texto, encoding="utf-8")
        return str(ruta)

    def trx(self, total=749, ejecutadas=None, pasadas=None, falladas=0, no_ejecutadas=0):
        ejecutadas = total - no_ejecutadas if ejecutadas is None else ejecutadas
        pasadas = ejecutadas - falladas if pasadas is None else pasadas
        return self.escribir("r.trx", TRX.format(total=total, ejecutadas=ejecutadas, pasadas=pasadas, falladas=falladas, no_ejecutadas=no_ejecutadas))

    # --- trx (xUnit) ---
    def test_trx_correcto_con_el_minimo(self):
        codigo, texto = self.modulo.evaluar_trx(self.trx(749), "Unitarias", 749)
        self.assertEqual(0, codigo)
        self.assertIn("749", texto)

    def test_trx_con_una_prueba_rota_falla(self):
        codigo, texto = self.modulo.evaluar_trx(self.trx(749, falladas=1), "Unitarias", 749)
        self.assertEqual(1, codigo)
        self.assertIn("1 fallada", texto)

    def test_trx_con_menos_pruebas_que_el_minimo_falla(self):
        # Una suite que «se saltea» pruebas en silencio no puede dar verde.
        codigo, texto = self.modulo.evaluar_trx(self.trx(700), "Unitarias", 749)
        self.assertEqual(1, codigo)
        self.assertIn("749", texto)

    def test_trx_con_pruebas_sin_ejecutar_falla(self):
        codigo, _ = self.modulo.evaluar_trx(self.trx(760, no_ejecutadas=3), "Integracion", 749)
        self.assertEqual(1, codigo)

    def test_trx_sin_pruebas_falla(self):
        codigo, _ = self.modulo.evaluar_trx(self.trx(0), "Unitarias", 0)
        self.assertEqual(1, codigo)

    def test_trx_que_no_existe_falla(self):
        codigo, texto = self.modulo.evaluar_trx(str(self.carpeta / "no-existe.trx"), "Unitarias", 1)
        self.assertEqual(1, codigo)
        self.assertIn("no existe", texto)

    def test_trx_ilegible_falla(self):
        codigo, _ = self.modulo.evaluar_trx(self.escribir("malo.trx", "esto no es xml"), "Unitarias", 1)
        self.assertEqual(1, codigo)

    # --- Vitest (json) ---
    def vitest(self, total=981, falladas=0, pendientes=0, exito=True):
        return self.escribir("v.json", json.dumps({
            "numTotalTests": total, "numPassedTests": total - falladas - pendientes, "numFailedTests": falladas,
            "numPendingTests": pendientes, "numTodoTests": 0, "success": exito,
        }))

    def test_vitest_correcto(self):
        codigo, _ = self.modulo.evaluar_vitest(self.vitest(981), "Vitest", 981)
        self.assertEqual(0, codigo)

    def test_vitest_con_una_rota_falla(self):
        codigo, texto = self.modulo.evaluar_vitest(self.vitest(981, falladas=2), "Vitest", 981)
        self.assertEqual(1, codigo)
        self.assertIn("2 fallada", texto)

    def test_vitest_con_menos_pruebas_que_el_minimo_falla(self):
        codigo, _ = self.modulo.evaluar_vitest(self.vitest(900), "Vitest", 981)
        self.assertEqual(1, codigo)

    def test_vitest_con_pruebas_omitidas_falla(self):
        codigo, _ = self.modulo.evaluar_vitest(self.vitest(981, pendientes=4), "Vitest", 981)
        self.assertEqual(1, codigo)

    # --- Vulnerabilidades de NuGet ---
    def nuget(self, severidades):
        paquetes = [{"id": f"Paquete{i}", "resolvedVersion": "1.0.0", "vulnerabilities": [{"severity": s, "advisoryurl": "https://x/y"}]}
                    for i, s in enumerate(severidades)]
        return self.escribir("n.json", json.dumps({"version": 1, "projects": [{"path": "a.csproj", "frameworks": [
            {"framework": "net10.0", "topLevelPackages": paquetes, "transitivePackages": []}]}]}))

    def test_nuget_sin_vulnerabilidades(self):
        codigo, _ = self.modulo.evaluar_nuget(self.escribir("n.json", json.dumps({"version": 1, "projects": [{"path": "a.csproj"}]})))
        self.assertEqual(0, codigo)

    def test_nuget_con_una_alta_falla(self):
        codigo, texto = self.modulo.evaluar_nuget(self.nuget(["High"]))
        self.assertEqual(1, codigo)
        self.assertIn("Paquete0", texto)

    def test_nuget_con_una_critica_falla(self):
        codigo, _ = self.modulo.evaluar_nuget(self.nuget(["Moderate", "Critical"]))
        self.assertEqual(1, codigo)

    def test_nuget_con_solo_moderadas_avisa_pero_pasa(self):
        codigo, texto = self.modulo.evaluar_nuget(self.nuget(["Moderate", "Low"]))
        self.assertEqual(0, codigo)
        self.assertIn("Moderate", texto)

    def test_nuget_en_las_dependencias_transitivas_tambien_falla(self):
        ruta = self.escribir("t.json", json.dumps({"projects": [{"frameworks": [{"transitivePackages": [
            {"id": "Hondo", "resolvedVersion": "2.0.0", "vulnerabilities": [{"severity": "High"}]}]}]}]}))
        codigo, _ = self.modulo.evaluar_nuget(ruta)
        self.assertEqual(1, codigo)

    def test_nuget_que_no_pudo_consultar_la_fuente_falla_aunque_no_liste_nada(self):
        # Sin red (NU1900) «dotnet list» no encuentra vulnerabilidades: eso NO es lo mismo que no tenerlas.
        ruta = self.escribir("p.json", json.dumps({"version": 1, "problems": [{"level": "warn", "text": "NU1900: no se pudo obtener la informacion de vulnerabilidades"}],
                                                    "projects": [{"path": "a.csproj"}]}))
        codigo, texto = self.modulo.evaluar_nuget(ruta)
        self.assertEqual(1, codigo)
        self.assertIn("NU1900", texto)

    def test_nuget_con_un_problema_de_error_falla(self):
        ruta = self.escribir("e.json", json.dumps({"problems": [{"level": "error", "text": "no se pudo restaurar"}], "projects": [{"path": "a.csproj"}]}))
        codigo, _ = self.modulo.evaluar_nuget(ruta)
        self.assertEqual(1, codigo)

    def test_nuget_sin_ningun_proyecto_falla(self):
        codigo, texto = self.modulo.evaluar_nuget(self.escribir("v.json", json.dumps({"version": 1, "projects": []})))
        self.assertEqual(1, codigo)
        self.assertIn("ningun proyecto", texto)

    def test_nuget_ilegible_falla(self):
        codigo, _ = self.modulo.evaluar_nuget(self.escribir("x.json", "no es json"))
        self.assertEqual(1, codigo)

    # --- Resumen en la pagina del job ---
    def test_el_resumen_se_agrega_al_archivo_de_resumen_de_github(self):
        destino = self.carpeta / "resumen.md"
        anterior = os.environ.get("GITHUB_STEP_SUMMARY")
        os.environ["GITHUB_STEP_SUMMARY"] = str(destino)
        try:
            self.modulo.escribir_resumen("### Unitarias\n749 correctas")
        finally:
            if anterior is None:
                os.environ.pop("GITHUB_STEP_SUMMARY", None)
            else:
                os.environ["GITHUB_STEP_SUMMARY"] = anterior
        self.assertIn("749 correctas", destino.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main(verbosity=1)
