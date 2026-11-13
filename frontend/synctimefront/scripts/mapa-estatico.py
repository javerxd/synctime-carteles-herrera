"""Genera la imagen estatica del mapa de la portada (public/mapa/ubicacion-taller.webp).

La portada muestra esta imagen y solo carga el mapa interactivo (OpenStreetMap) si el visitante lo pide:
asi nada se pide a terceros sin una accion suya. Se vuelve a ejecutar si cambia la ubicacion:

    python scripts/mapa-estatico.py -37.46787 -72.34819

Descarga una vez unas pocas teselas de tile.openstreetmap.org (uso ocasional, permitido por su politica)
y las combina con Pillow. Datos (c) colaboradores de OpenStreetMap, licencia ODbL: la portada lo indica.
"""
import io
import math
import sys
import urllib.request

from PIL import Image, ImageDraw

ZOOM = 17
ANCHO, ALTO = 1200, 600
TINTA = (15, 23, 42)
AMBAR = (180, 83, 9)


def pixel_global(lat, lon, zoom):
    escala = 256 * 2 ** zoom
    x = (lon + 180) / 360 * escala
    y = (1 - math.log(math.tan(math.radians(lat)) + 1 / math.cos(math.radians(lat))) / math.pi) / 2 * escala
    return x, y


def tesela(x, y, zoom):
    req = urllib.request.Request(f"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png",
                                 headers={"User-Agent": "SyncTime-dev/1.0 (mapa estatico de la portada)"})
    with urllib.request.urlopen(req, timeout=20) as r:
        return Image.open(io.BytesIO(r.read())).convert("RGB")


def main(lat, lon, salida):
    cx, cy = pixel_global(lat, lon, ZOOM)
    izq, arr = cx - ANCHO / 2, cy - ALTO / 2
    tx0, ty0 = int(izq // 256), int(arr // 256)
    tx1, ty1 = int((izq + ANCHO) // 256), int((arr + ALTO) // 256)
    lienzo = Image.new("RGB", ((tx1 - tx0 + 1) * 256, (ty1 - ty0 + 1) * 256))
    for tx in range(tx0, tx1 + 1):
        for ty in range(ty0, ty1 + 1):
            lienzo.paste(tesela(tx, ty, ZOOM), ((tx - tx0) * 256, (ty - ty0) * 256))
    ox, oy = int(izq - tx0 * 256), int(arr - ty0 * 256)
    mapa = lienzo.crop((ox, oy, ox + ANCHO, oy + ALTO))

    # Marcador: circulo en tinta con centro ambar, como el resto del sitio.
    d = ImageDraw.Draw(mapa)
    x, y = ANCHO // 2, ALTO // 2
    d.ellipse((x - 26, y - 26, x + 26, y + 26), fill=(255, 255, 255))
    d.ellipse((x - 20, y - 20, x + 20, y + 20), fill=TINTA)
    d.ellipse((x - 8, y - 8, x + 8, y + 8), fill=AMBAR)
    mapa.save(salida, "WEBP", quality=80, method=6)
    print(f"Mapa guardado en {salida}")


if __name__ == "__main__":
    lat = float(sys.argv[1]) if len(sys.argv) > 1 else -37.46787
    lon = float(sys.argv[2]) if len(sys.argv) > 2 else -72.34819
    main(lat, lon, "public/mapa/ubicacion-taller.webp")
