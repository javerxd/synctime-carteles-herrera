using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;

public sealed class OpcionesAdjuntos
{
    public const string Seccion = "Adjuntos";

    // Carpeta PRIVADA (solo con Almacen:Modo=Disco), distinta de la de las fotos de la vitrina y fuera del repositorio (.gitignore:
    // **/almacen/). Ninguna ruta publica la sirve: solo /api/v1/adjuntos, que exige sesion y permiso.
    public string Carpeta { get; set; } = "almacen/adjuntos";
}

public enum VersionAdjunto { Miniatura, Archivo }

// Archivos de los adjuntos (fase 7): una imagen queda como "{clave}-grande.webp" y
// "{clave}-miniatura.webp" (ya sin metadatos), y un PDF como "{clave}.pdf", tal cual se subio. La clave
// es aleatoria (128 bits) y solo se usan nombres con ese formato exacto: un nombre armado con datos de la
// peticion nunca llega al almacen. Desde la fase 16C-1 el almacen es IAlmacenArchivos (disco o Blob).
public sealed partial class AlmacenAdjuntos(IAlmacenArchivos almacen)
{
    public static string NuevaClave() => RandomNumberGenerator.GetHexString(32, lowercase: true);

    [GeneratedRegex(@"^[0-9a-f]{32}\z")]
    private static partial Regex Clave();

    private static IEnumerable<string> Nombres(string clave) =>
        [$"{clave}-grande.webp", $"{clave}-miniatura.webp", $"{clave}.pdf"];

    public async Task GuardarImagenAsync(string clave, ImagenProcesada imagen, CancellationToken ct)
    {
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{clave}-grande.webp", imagen.Grande, ct);
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{clave}-miniatura.webp", imagen.Miniatura, ct);
    }

    public Task GuardarPdfAsync(string clave, byte[] datos, CancellationToken ct) =>
        almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{clave}.pdf", datos, ct);

    // El archivo pedido abierto para enviarlo, o null si la clave no tiene el formato o el archivo no existe.
    public async Task<ArchivoAbierto?> AbrirAsync(string clave, bool esPdf, VersionAdjunto version, CancellationToken ct)
    {
        if (!Clave().IsMatch(clave))
            return null;
        var nombre = esPdf
            ? (version == VersionAdjunto.Archivo ? $"{clave}.pdf" : null)
            : $"{clave}-{(version == VersionAdjunto.Miniatura ? "miniatura" : "grande")}.webp";
        return nombre is null ? null : await almacen.AbrirAsync(AreasAlmacen.Adjuntos, nombre, ct);
    }

    // Borra todos los archivos de una clave. Nunca lanza: un archivo que no se pudo borrar queda sin registro y lo retira
    // despues la purga diaria. Sin CancellationToken a proposito: la limpieza tras un fallo debe correr aunque la peticion se cancele.
    public async Task BorrarAsync(string clave)
    {
        if (!Clave().IsMatch(clave))
            return;
        foreach (var nombre in Nombres(clave))
            await almacen.BorrarAsync(AreasAlmacen.Adjuntos, nombre);
    }

    // Claves de los archivos mas antiguos que el limite (para retirar los que quedaron sin registro en la base, por ejemplo al
    // borrarse la solicitud). Lo que no tenga el formato se ignora.
    public async Task<IReadOnlyCollection<string>> ClavesAnterioresAAsync(DateTimeOffset limite, CancellationToken ct)
    {
        var claves = new HashSet<string>();
        foreach (var objeto in await almacen.ListarAnterioresAsync(AreasAlmacen.Adjuntos, limite, ct))
            if (objeto.Nombre.Length >= 32 && Clave().IsMatch(objeto.Nombre[..32]))
                claves.Add(objeto.Nombre[..32]);
        return claves;
    }
}
