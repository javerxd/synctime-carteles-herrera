#!/usr/bin/env python3
"""Fase 16B (RNFO15): decide en el pipeline si un resultado de pruebas o de vulnerabilidades deja pasar.

  python scripts/ci-resumen.py trx    <archivo.trx>  --nombre Unitarias --minimo 749
  python scripts/ci-resumen.py vitest <archivo.json> --nombre Vitest    --minimo 981
  python scripts/ci-resumen.py nuget  <archivo.json>                      (salida de «dotnet list package --vulnerable --format json»)

Falla (codigo 1) si hay pruebas falladas u omitidas, si se ejecutaron MENOS que el minimo (una suite que se saltea pruebas en silencio no
puede dar verde) o si NuGet informa una vulnerabilidad alta o critica. Escribe un resumen en la pagina del job (GITHUB_STEP_SUMMARY).
Solo biblioteca estandar. Las pruebas son scripts/test_ci_resumen.py.
"""
import argparse
import json
import os
import sys
import xml.etree.ElementTree as ET

GRAVES = {"high", "critical"}


def escribir_resumen(markdown):
    destino = os.environ.get("GITHUB_STEP_SUMMARY")
    if destino:
        with open(destino, "a", encoding="utf-8") as archivo:
            archivo.write(markdown + "\n\n")


def _veredicto(nombre, total, falladas, omitidas, minimo):
    problemas = []
    if total <= 0:
        problemas.append("no se ejecuto ninguna prueba")
    if falladas:
        problemas.append(f"{falladas} fallada(s)")
    if omitidas:
        problemas.append(f"{omitidas} omitida(s) o sin ejecutar")
    if total < minimo:
        problemas.append(f"se ejecutaron {total} y se esperaban al menos {minimo}")
    estado = "FALLO" if problemas else "OK"
    texto = f"{estado} {nombre}: {total} pruebas, {falladas} fallada(s), {omitidas} omitida(s) (minimo {minimo})"
    if problemas:
        texto += " - " + "; ".join(problemas)
    return (1 if problemas else 0), texto


def evaluar_trx(ruta, nombre, minimo):
    if not os.path.exists(ruta):
        return 1, f"FALLO {nombre}: el archivo de resultados no existe ({ruta}); las pruebas no llegaron a ejecutarse"
    try:
        raiz = ET.parse(ruta).getroot()
    except ET.ParseError as error:
        return 1, f"FALLO {nombre}: el archivo de resultados no se puede leer ({error})"
    contadores = next((e for e in raiz.iter() if e.tag.endswith("Counters")), None)
    if contadores is None:
        return 1, f"FALLO {nombre}: el archivo de resultados no trae contadores"
    total = int(contadores.get("total", 0))
    falladas = int(contadores.get("failed", 0)) + int(contadores.get("error", 0)) + int(contadores.get("timeout", 0)) + int(contadores.get("aborted", 0))
    omitidas = int(contadores.get("notExecuted", 0)) + max(0, total - int(contadores.get("executed", total)))
    return _veredicto(nombre, total, falladas, omitidas, minimo)


def evaluar_vitest(ruta, nombre, minimo):
    if not os.path.exists(ruta):
        return 1, f"FALLO {nombre}: el archivo de resultados no existe ({ruta}); las pruebas no llegaron a ejecutarse"
    try:
        with open(ruta, encoding="utf-8") as archivo:
            datos = json.load(archivo)
    except (OSError, json.JSONDecodeError) as error:
        return 1, f"FALLO {nombre}: el archivo de resultados no se puede leer ({error})"
    total = int(datos.get("numTotalTests", 0))
    falladas = int(datos.get("numFailedTests", 0))
    omitidas = int(datos.get("numPendingTests", 0)) + int(datos.get("numTodoTests", 0))
    codigo, texto = _veredicto(nombre, total, falladas, omitidas, minimo)
    if datos.get("success") is False and codigo == 0:
        return 1, f"FALLO {nombre}: Vitest informa success=false"
    return codigo, texto


def _paquetes(nodo):
    """Todo diccionario con «vulnerabilities» dentro del informe, a cualquier profundidad (el formato cambia entre versiones del SDK)."""
    if isinstance(nodo, dict):
        if isinstance(nodo.get("vulnerabilities"), list):
            yield nodo
        for valor in nodo.values():
            yield from _paquetes(valor)
    elif isinstance(nodo, list):
        for valor in nodo:
            yield from _paquetes(valor)


def evaluar_nuget(ruta):
    try:
        with open(ruta, encoding="utf-8") as archivo:
            datos = json.load(archivo)
    except (OSError, json.JSONDecodeError) as error:
        return 1, f"FALLO nuget: el informe no se puede leer ({error})"
    # Sin poder consultar la fuente (NU1900, sin red) «dotnet list» no lista nada: eso NO significa que no haya vulnerabilidades.
    problemas = [p for p in (datos.get("problems") or []) if isinstance(p, dict)] if isinstance(datos, dict) else []
    if problemas:
        return 1, "\n".join(["FALLO nuget: el analisis no pudo hacerse completo, asi que su silencio no vale", *[f"  {p.get('level', '?')}: {p.get('text', '')}" for p in problemas]])
    if not (isinstance(datos, dict) and datos.get("projects")):
        return 1, "FALLO nuget: el informe no trae ningun proyecto analizado"
    graves, leves = [], []
    for paquete in _paquetes(datos):
        for vulnerabilidad in paquete["vulnerabilities"]:
            severidad = str(vulnerabilidad.get("severity", "desconocida"))
            linea = f"{paquete.get('id', '?')} {paquete.get('resolvedVersion', '?')}: {severidad} {vulnerabilidad.get('advisoryurl', '')}".strip()
            (graves if severidad.lower() in GRAVES else leves).append(linea)
    lineas = [f"AVISO nuget {linea}" for linea in leves]
    if graves:
        return 1, "\n".join([f"FALLO nuget: {len(graves)} vulnerabilidad(es) alta(s) o critica(s)", *[f"  {g}" for g in graves], *lineas])
    return 0, "\n".join(["OK nuget: sin vulnerabilidades altas ni criticas", *lineas])


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("tipo", choices=["trx", "vitest", "nuget"])
    parser.add_argument("archivo")
    parser.add_argument("--nombre", default="Pruebas")
    parser.add_argument("--minimo", type=int, default=1)
    argumentos = parser.parse_args(argv)
    if argumentos.tipo == "trx":
        codigo, texto = evaluar_trx(argumentos.archivo, argumentos.nombre, argumentos.minimo)
    elif argumentos.tipo == "vitest":
        codigo, texto = evaluar_vitest(argumentos.archivo, argumentos.nombre, argumentos.minimo)
    else:
        codigo, texto = evaluar_nuget(argumentos.archivo)
    print(texto)
    escribir_resumen(("### " if codigo == 0 else "### :x: ") + texto.replace("\n", "\n\n"))
    return codigo


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
