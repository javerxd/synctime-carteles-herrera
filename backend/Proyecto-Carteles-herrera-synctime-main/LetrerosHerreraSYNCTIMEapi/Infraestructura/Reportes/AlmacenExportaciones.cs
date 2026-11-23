using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RNFP18): los archivos de las exportaciones (privados, fuera del repositorio). El nombre sale SOLO del identificador del trabajo
// y del formato permitido ({32 hex}.xlsx|csv|pdf): texto de la persona nunca llega al almacen, y un identificador o formato fuera de lo
// esperado no produce ningun nombre (no hay recorrido de carpetas posible). Desde la fase 16C-1 el almacen es IAlmacenArchivos (disco o Blob).
public sealed partial class AlmacenExportaciones(IAlmacenArchivos almacen)
{
    [GeneratedRegex(@"^[0-9a-f]{32}\.(xlsx|csv|pdf)(\.tmp)?\z")]
    private static partial Regex NombreDeArchivo();

    private static string Nombre(Guid id, string formato)
    {
        if (!FormatosReporte.Existe(formato))
            throw new ArgumentException("Formato desconocido.", nameof(formato));
        return $"{id:N}.{formato}";
    }

    public Task GuardarAsync(Guid id, string formato, byte[] datos, CancellationToken ct) =>
        almacen.GuardarAsync(AreasAlmacen.Exportaciones, Nombre(id, formato), datos, ct);

    /// <summary>El archivo abierto para enviarlo, o null si no existe (nunca se inventa un nombre fuera del formato).</summary>
    public Task<ArchivoAbierto?> AbrirAsync(Guid id, string formato, CancellationToken ct) =>
        FormatosReporte.Existe(formato)
            ? almacen.AbrirAsync(AreasAlmacen.Exportaciones, Nombre(id, formato), ct)
            : Task.FromResult<ArchivoAbierto?>(null);

    /// <summary>Borra el archivo de un trabajo. Nunca lanza: lo que quede lo retira la purga de huerfanos.</summary>
    public async Task BorrarAsync(Guid id, string formato)
    {
        if (FormatosReporte.Existe(formato))
            await almacen.BorrarAsync(AreasAlmacen.Exportaciones, Nombre(id, formato));
    }

    /// <summary>
    /// Borra los archivos con nombre de exportacion modificados antes de <paramref name="limiteUtc"/>: lo que quedo sin registro (el proceso
    /// se cayo entre escribir el archivo y anotarlo) y los temporales abandonados. Solo toca nombres con el formato exacto.
    /// </summary>
    public async Task<int> PurgarHuerfanosAsync(DateTime limiteUtc, CancellationToken ct)
    {
        var borrados = 0;
        foreach (var objeto in await almacen.ListarAnterioresAsync(AreasAlmacen.Exportaciones, new DateTimeOffset(limiteUtc, TimeSpan.Zero), ct))
            if (NombreDeArchivo().IsMatch(objeto.Nombre) && await almacen.BorrarAsync(AreasAlmacen.Exportaciones, objeto.Nombre))
                borrados++;
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
