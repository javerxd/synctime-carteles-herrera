"""Convierte las imágenes entregadas por el taller (docs/image/) a WebP liviano para el portal (fase 12C).

Uso, desde frontend/synctimefront:  python scripts/imagenes-portal.py
Los originales quedan en docs/image/. Cada imagen se reduce hasta cumplir su tope de peso (RNFP01):
fondo ≤ 200 KB, carteles ≤ 150 KB, icono de WhatsApp ≤ 20 KB.
"""
from pathlib import Path
from PIL import Image

RAIZ = Path(__file__).resolve().parents[3]
ORIGEN = RAIZ / "docs" / "image"
DESTINO = Path(__file__).resolve().parents[1] / "public" / "imagenes"

# nombre de origen -> (nombre de salida, ancho máximo, tope en bytes)
IMAGENES = {
    "fondo-web.jpg": ("fondo-web.webp", 1920, 200_000),
    "cartel-abierto.png": ("cartel-abierto.webp", 960, 150_000),
    "cartel-cerrado.png": ("cartel-cerrado.webp", 960, 150_000),
    "icono-whatsapp.png": ("icono-whatsapp.webp", 128, 20_000),
}


def convertir(origen: Path, salida: Path, ancho_max: int, tope: int) -> int:
    imagen = Image.open(origen)
    imagen = imagen.convert("RGBA" if imagen.mode in ("RGBA", "LA", "P") else "RGB")
    if imagen.width > ancho_max:
        alto = round(imagen.height * ancho_max / imagen.width)
        imagen = imagen.resize((ancho_max, alto), Image.LANCZOS)
    calidad = 82
    while True:
        imagen.save(salida, "WEBP", quality=calidad, method=6)
        peso = salida.stat().st_size
        if peso <= tope or calidad <= 30:
            return peso
        calidad -= 6


def main() -> None:
    DESTINO.mkdir(parents=True, exist_ok=True)
    for nombre, (salida, ancho, tope) in IMAGENES.items():
        peso = convertir(ORIGEN / nombre, DESTINO / salida, ancho, tope)
        estado = "ok" if peso <= tope else "SUPERA EL TOPE"
        print(f"{salida}: {peso / 1024:.0f} KB (tope {tope / 1024:.0f} KB) {estado}")


if __name__ == "__main__":
    main()
