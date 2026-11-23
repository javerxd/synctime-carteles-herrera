using LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;

// Los archivos en carpetas privadas del servidor (desarrollo y compose). Es lo que hacian por su cuenta AlmacenAdjuntos, AlmacenImagenes y
// AlmacenExportaciones antes de la fase 16C-1: las mismas carpetas, el mismo «escribir en un temporal y renombrar» y los mismos nombres.
public sealed class AlmacenEnDisco(Func<string, string> carpetaDeArea) : IAlmacenArchivos
{
    // Las carpetas de siempre (Adjuntos:Carpeta, Imagenes:Carpeta, Reportes:Carpeta), relativas a la carpeta de la API.
    public static AlmacenEnDisco Desde(
        IOptions<OpcionesAdjuntos> adjuntos, IOptions<OpcionesImagenes> imagenes, IOptions<OpcionesReportes> reportes, IHostEnvironment entorno) =>
        new(area => area switch
        {
            AreasAlmacen.Adjuntos => Resolver(entorno, adjuntos.Value.Carpeta),
            AreasAlmacen.Imagenes => Resolver(entorno, imagenes.Value.Carpeta),
            AreasAlmacen.Exportaciones => Resolver(entorno, reportes.Value.Carpeta),
            _ => throw new ArgumentException("Area desconocida.", nameof(area))
        });

    private static string Resolver(IHostEnvironment entorno, string carpeta) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(entorno.ContentRootPath, carpeta)));

    private string Carpeta(string area) =>
        AreasAlmacen.Existe(area)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(carpetaDeArea(area)))
            : throw new ArgumentException("Area desconocida.", nameof(area));

    // La ruta completa, solo si el nombre tiene el formato exacto y la ruta queda DENTRO de la carpeta del area (doble seguro).
    private string? Ruta(string area, string nombre, bool permitirTemporal)
    {
        if (!AreasAlmacen.Existe(area) || !NombresDeArchivo.EsValido(nombre, permitirTemporal))
            return null;
        var carpeta = Carpeta(area);
        var ruta = Path.GetFullPath(Path.Combine(carpeta, nombre));
        return ruta.StartsWith(carpeta + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? ruta : null;
    }

    public async Task GuardarAsync(string area, string nombre, byte[] datos, CancellationToken ct)
    {
        _ = Carpeta(area);
        var final = Ruta(area, nombre, permitirTemporal: false) ?? throw new ArgumentException("Nombre de archivo no valido.", nameof(nombre));
        Directory.CreateDirectory(Path.GetDirectoryName(final)!);

        // Se escribe en un temporal y se renombra: nadie puede leer un archivo a medio escribir.
        var temporal = final + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporal, datos, ct);
            File.Move(temporal, final, overwrite: true);
        }
        catch
        {
            // Una escritura cancelada o fallida no deja el temporal: ocuparia espacio hasta la purga.
            TryBorrar(temporal);
            throw;
        }
    }

    public Task<ArchivoAbierto?> AbrirAsync(string area, string nombre, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Ruta(area, nombre, permitirTemporal: false) is not { } ruta)
            return Task.FromResult<ArchivoAbierto?>(null);
        try
        {
            // FileShare.Read: una purga que borra el archivo mientras se envia no rompe la descarga en curso en Linux, y en Windows espera.
            var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult<ArchivoAbierto?>(new ArchivoAbierto(flujo, flujo.Length, new DateTimeOffset(File.GetLastWriteTimeUtc(ruta), TimeSpan.Zero)));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<ArchivoAbierto?>(null);
        }
    }

    public Task<bool> BorrarAsync(string area, string nombre)
    {
        // El temporal de un archivo se llama como el archivo mas «.tmp»: se borran los dos.
        if (Ruta(area, nombre, permitirTemporal: true) is not { } ruta)
            return Task.FromResult(false);
        var borrado = TryBorrar(ruta);
        if (!nombre.EndsWith(".tmp", StringComparison.Ordinal))
            TryBorrar(ruta + ".tmp");
        return Task.FromResult(borrado);
    }

    // true solo si el archivo existia y se pudo borrar. Nunca lanza: lo que quede lo retira la purga.
    private static bool TryBorrar(string ruta)
    {
        try
        {
            if (!File.Exists(ruta))
                return false;
            File.Delete(ruta);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public Task<IReadOnlyList<ObjetoAlmacenado>> ListarAnterioresAsync(string area, DateTimeOffset limite, CancellationToken ct)
    {
        var carpeta = Carpeta(area);
        if (!Directory.Exists(carpeta))
            return Task.FromResult<IReadOnlyList<ObjetoAlmacenado>>([]);
        var antiguos = new List<ObjetoAlmacenado>();
        foreach (var ruta in Directory.EnumerateFiles(carpeta))
        {
            ct.ThrowIfCancellationRequested();
            var nombre = Path.GetFileName(ruta);
            if (!NombresDeArchivo.EsValido(nombre, permitirTemporal: true))
                continue;
            var modificado = new DateTimeOffset(File.GetLastWriteTimeUtc(ruta), TimeSpan.Zero);
            if (modificado < limite)
                antiguos.Add(new ObjetoAlmacenado(nombre, modificado));
        }
        return Task.FromResult<IReadOnlyList<ObjetoAlmacenado>>(antiguos);
    }
}
