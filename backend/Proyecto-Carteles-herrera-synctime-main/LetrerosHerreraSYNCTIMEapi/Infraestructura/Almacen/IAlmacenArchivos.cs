using System.Text.RegularExpressions;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;

// Las carpetas logicas del almacen. En disco son carpetas (las de Adjuntos:Carpeta, Imagenes:Carpeta y Reportes:Carpeta); en Blob, prefijos
// dentro de un unico contenedor privado. Un area fuera de esta lista no se acepta.
public static class AreasAlmacen
{
    public const string Adjuntos = "adjuntos";
    public const string Imagenes = "imagenes";
    public const string Exportaciones = "exportaciones";

    public static readonly IReadOnlyList<string> Todas = [Adjuntos, Imagenes, Exportaciones];

    public static bool Existe(string? area) => area is not null && Todas.Contains(area, StringComparer.Ordinal);
}

// Los unicos nombres que el almacen acepta: los que genera la aplicacion ({32 hex} con un sufijo conocido). Texto de una persona (un nombre de
// archivo subido, una ruta) nunca llega al disco ni a la nube: no hay recorrido de carpetas posible, en ninguna de las dos implementaciones.
// \z y no $: $ tambien aceptaria un salto de linea al final.
public static partial class NombresDeArchivo
{
    [GeneratedRegex(@"^[0-9a-f]{32}(-grande\.webp|-miniatura\.webp|\.pdf|\.xlsx|\.csv)(\.tmp)?\z")]
    private static partial Regex Formato();

    /// <param name="permitirTemporal">Solo el disco usa «.tmp» (escribe y renombra); se admite para listar y borrar los que quedaron a medias.</param>
    public static bool EsValido(string? nombre, bool permitirTemporal = false) =>
        nombre is not null && Formato().IsMatch(nombre) && (permitirTemporal || !nombre.EndsWith(".tmp", StringComparison.Ordinal));
}

public sealed record ObjetoAlmacenado(string Nombre, DateTimeOffset UltimaModificacion);

// Un archivo abierto para leerlo. Quien lo recibe es responsable de cerrarlo (ASP.NET lo cierra al terminar de enviar la respuesta).
public sealed class ArchivoAbierto(Stream flujo, long longitud, DateTimeOffset ultimaModificacion) : IDisposable, IAsyncDisposable
{
    public Stream Flujo { get; } = flujo;
    public long Longitud { get; } = longitud;
    public DateTimeOffset UltimaModificacion { get; } = ultimaModificacion;

    public void Dispose() => Flujo.Dispose();

    public ValueTask DisposeAsync() => Flujo.DisposeAsync();
}

// Donde viven los archivos de la aplicacion (H12). Dos implementaciones con el MISMO contrato (las pruebas se escriben una vez):
//   AlmacenEnDisco  desarrollo y compose.
//   AlmacenEnBlob   la nube gratuita (Render no tiene disco persistente): Azure Blob Storage, contenedor privado.
public interface IAlmacenArchivos
{
    /// <summary>Guarda (o reemplaza) un archivo. Nadie puede leerlo a medias. Lanza ArgumentException si el area o el nombre no son validos.</summary>
    Task GuardarAsync(string area, string nombre, byte[] datos, CancellationToken ct);

    /// <summary>Abre un archivo para leerlo como flujo, o null si no existe o si el area o el nombre no son validos.</summary>
    Task<ArchivoAbierto?> AbrirAsync(string area, string nombre, CancellationToken ct);

    /// <summary>
    /// Borra un archivo (y, en disco, su temporal). true si existia y se borro. NUNCA lanza por un problema del almacen (un archivo que no se
    /// pudo borrar queda sin registro y lo retira la purga diaria): no debe tapar el error original de quien limpia tras un fallo.
    /// </summary>
    Task<bool> BorrarAsync(string area, string nombre);

    /// <summary>
    /// Los archivos del area modificados antes del limite (para retirar los que quedaron sin registro en la base). En disco incluye los
    /// temporales abandonados. Lo que no tenga el formato de la aplicacion se ignora.
    /// </summary>
    Task<IReadOnlyList<ObjetoAlmacenado>> ListarAnterioresAsync(string area, DateTimeOffset limite, CancellationToken ct);
}
