using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;

public sealed class OpcionesAdjuntos
{
    public const string Seccion = "Adjuntos";

    // Carpeta PRIVADA, distinta de la de las fotos de la vitrina y fuera del repositorio (.gitignore:
    // **/almacen/). Ninguna ruta publica la sirve: solo /api/v1/adjuntos, que exige sesion y permiso.
    public string Carpeta { get; set; } = "almacen/adjuntos";
}

public enum VersionAdjunto { Miniatura, Archivo }

// Archivos de los adjuntos (fase 7): una imagen queda como "{clave}-grande.webp" y
// "{clave}-miniatura.webp" (ya sin metadatos), y un PDF como "{clave}.pdf", tal cual se subio. La clave
// es aleatoria (128 bits) y solo se usan nombres con ese formato exacto: un nombre armado con datos de la
// peticion nunca llega al disco.
public sealed partial class AlmacenAdjuntos(IOptions<OpcionesAdjuntos> opciones, IHostEnvironment entorno)
{
    public string Carpeta => Path.GetFullPath(Path.Combine(entorno.ContentRootPath, opciones.Value.Carpeta));

    public static string NuevaClave() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    [GeneratedRegex(@"^[0-9a-f]{32}\z")]
    private static partial Regex Clave();

    [GeneratedRegex(@"^([0-9a-f]{32})(-grande\.webp|-miniatura\.webp|\.pdf)(\.tmp)?\z")]
    private static partial Regex NombreDeArchivo();

    private static IEnumerable<string> Nombres(string clave) =>
        [$"{clave}-grande.webp", $"{clave}-miniatura.webp", $"{clave}.pdf"];

    public async Task GuardarImagenAsync(string clave, ImagenProcesada imagen, CancellationToken ct)
    {
        Directory.CreateDirectory(Carpeta);
        await EscribirAsync($"{clave}-grande.webp", imagen.Grande, ct);
        await EscribirAsync($"{clave}-miniatura.webp", imagen.Miniatura, ct);
    }

    public async Task GuardarPdfAsync(string clave, byte[] datos, CancellationToken ct)
    {
        Directory.CreateDirectory(Carpeta);
        await EscribirAsync($"{clave}.pdf", datos, ct);
    }

    // Se escribe en un temporal y se renombra: nadie puede leer un archivo a medio escribir.
    private async Task EscribirAsync(string nombre, byte[] datos, CancellationToken ct)
    {
        var final = Path.Combine(Carpeta, nombre);
        var temporal = final + ".tmp";
        await File.WriteAllBytesAsync(temporal, datos, ct);
        File.Move(temporal, final, overwrite: false);
    }

    // Ruta del archivo pedido, o null si la clave no tiene el formato o el archivo no existe.
    public string? Ruta(string clave, bool esPdf, VersionAdjunto version)
    {
        if (!Clave().IsMatch(clave))
            return null;
        var nombre = esPdf
            ? (version == VersionAdjunto.Archivo ? $"{clave}.pdf" : null)
            : $"{clave}-{(version == VersionAdjunto.Miniatura ? "miniatura" : "grande")}.webp";
        if (nombre is null)
            return null;
        var ruta = Path.Combine(Carpeta, nombre);
        return File.Exists(ruta) ? ruta : null;
    }

    // Borra todos los archivos de una clave (tambien temporales). Nunca lanza: un archivo que no se pudo
    // borrar queda sin registro y lo retira despues la purga diaria.
    public void Borrar(string clave)
    {
        if (!Clave().IsMatch(clave))
            return;
        foreach (var nombre in Nombres(clave))
        {
            foreach (var ruta in new[] { Path.Combine(Carpeta, nombre), Path.Combine(Carpeta, nombre + ".tmp") })
            {
                try
                {
                    File.Delete(ruta);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Queda para la purga: nunca se sirve sin su registro en la base.
                }
            }
        }
    }

    // Claves de los archivos en disco mas antiguos que el limite (para retirar los que quedaron sin
    // registro en la base, por ejemplo al borrarse la solicitud). Lo que no tenga el formato se ignora.
    public IReadOnlyCollection<string> ClavesAnterioresA(DateTimeOffset limite)
    {
        if (!Directory.Exists(Carpeta))
            return [];
        var claves = new HashSet<string>();
        foreach (var ruta in Directory.EnumerateFiles(Carpeta))
        {
            var coincide = NombreDeArchivo().Match(Path.GetFileName(ruta));
            if (coincide.Success && File.GetLastWriteTimeUtc(ruta) < limite.UtcDateTime)
                claves.Add(coincide.Groups[1].Value);
        }
        return claves;
    }
}
