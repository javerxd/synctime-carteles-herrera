using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RNFP18): los archivos de las exportaciones, en almacen/exportaciones/ (privada, fuera del repositorio). El nombre sale
// SOLO del identificador del trabajo y del formato permitido ({32 hex}.xlsx|csv|pdf): texto de la persona nunca llega al disco, y
// un identificador o formato fuera de lo esperado no produce ninguna ruta (no hay recorrido de carpetas posible).
public sealed partial class AlmacenExportaciones(IOptions<OpcionesReportes> opciones, IHostEnvironment entorno)
{
    public string Carpeta => Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(entorno.ContentRootPath, opciones.Value.Carpeta)));

    [GeneratedRegex(@"^[0-9a-f]{32}\.(xlsx|csv|pdf)(\.tmp)?\z")]
    private static partial Regex NombreDeArchivo();

    private static string Nombre(Guid id, string formato)
    {
        if (!FormatosReporte.Existe(formato))
            throw new ArgumentException("Formato desconocido.", nameof(formato));
        return $"{id:N}.{formato}";
    }

    // Se escribe en un temporal y se renombra: nadie puede leer un archivo a medio escribir.
    public async Task GuardarAsync(Guid id, string formato, byte[] datos, CancellationToken ct)
    {
        Directory.CreateDirectory(Carpeta);
        var final = Path.Combine(Carpeta, Nombre(id, formato));
        var temporal = final + ".tmp";
        await File.WriteAllBytesAsync(temporal, datos, ct);
        File.Move(temporal, final, overwrite: true);
    }

    /// <summary>La ruta del archivo, o null si no existe (nunca se inventa una ruta fuera de la carpeta).</summary>
    public string? Ruta(Guid id, string formato)
    {
        if (!FormatosReporte.Existe(formato))
            return null;
        var ruta = Path.GetFullPath(Path.Combine(Carpeta, Nombre(id, formato)));
        return ruta.StartsWith(Carpeta + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(ruta) ? ruta : null;
    }

    /// <summary>Borra el archivo de un trabajo (y su temporal). Nunca lanza: lo que quede lo retira la purga de huerfanos.</summary>
    public void Borrar(Guid id, string formato)
    {
        if (!FormatosReporte.Existe(formato))
            return;
        foreach (var nombre in new[] { Nombre(id, formato), Nombre(id, formato) + ".tmp" })
        {
            try
            {
                File.Delete(Path.Combine(Carpeta, nombre));
            }
            catch (IOException)
            {
                // Se reintenta en la proxima purga.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Borra los archivos con nombre de exportacion modificados antes de <paramref name="limite"/>: lo que quedo sin registro (el proceso
    /// se cayo entre escribir el archivo y anotarlo) y los temporales abandonados. Solo toca nombres con el formato exacto.
    /// </summary>
    public int PurgarHuerfanos(DateTime limiteUtc)
    {
        if (!Directory.Exists(Carpeta))
            return 0;
        var borrados = 0;
        foreach (var ruta in Directory.EnumerateFiles(Carpeta))
        {
            if (!NombreDeArchivo().IsMatch(Path.GetFileName(ruta)) || File.GetLastWriteTimeUtc(ruta) >= limiteUtc)
                continue;
            try
            {
                File.Delete(ruta);
                borrados++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return borrados;
    }
}

// Despierta al proceso en segundo plano en cuanto hay un trabajo nuevo, para que el archivo salga en segundos y no al proximo
// sondeo. Si nadie espera, el aviso queda guardado (como mucho uno) y no se pierde.
public sealed class SenalExportaciones
{
    private readonly SemaphoreSlim senal = new(0, 1);

    public void Avisar()
    {
        try
        {
            senal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Ya habia un aviso pendiente: basta con uno.
        }
    }

    public Task<bool> EsperarAsync(TimeSpan espera, CancellationToken ct) => senal.WaitAsync(espera, ct);
}
