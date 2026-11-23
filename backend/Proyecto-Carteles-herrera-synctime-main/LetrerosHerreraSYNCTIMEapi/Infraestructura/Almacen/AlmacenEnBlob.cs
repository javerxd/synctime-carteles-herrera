using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;

// Los archivos en Azure Blob Storage (H12, D1): lo que usa la nube gratuita, donde Render no tiene disco persistente. Un unico contenedor
// PRIVADO; cada area es un prefijo («adjuntos/», «imagenes/», «exportaciones/») y el nombre del blob es el mismo nombre aleatorio de siempre
// ({32 hex}.ext): ningun dato del cliente va en la direccion del archivo.
//
// El contenedor no puede ser publico: si alguien lo deja con acceso anonimo, esta clase se niega a usarlo (no guarda ni sirve nada) en vez de
// exponer en silencio los adjuntos de los clientes. Los archivos solo salen por la API, que exige sesion y permiso.
public sealed class AlmacenEnBlob : IAlmacenArchivos
{
    private readonly BlobContainerClient contenedor;
    private readonly ILogger<AlmacenEnBlob> logger;
    private readonly SemaphoreSlim inicio = new(1, 1);
    private volatile bool verificado;

    public AlmacenEnBlob(IOptions<OpcionesAlmacen> opciones, ILogger<AlmacenEnBlob> logger)
    {
        this.logger = logger;
        var o = opciones.Value;
        if (o.Problema() is { } problema)
            throw new InvalidOperationException(problema);

        var cliente = new BlobClientOptions();
        cliente.Retry.Mode = RetryMode.Exponential;
        cliente.Retry.MaxRetries = o.Blob.ReintentosMaximos;
        cliente.Retry.Delay = TimeSpan.FromMilliseconds(500);
        cliente.Retry.MaxDelay = TimeSpan.FromSeconds(10);
        cliente.Retry.NetworkTimeout = TimeSpan.FromSeconds(o.Blob.TiempoEsperaSegundos);
        try
        {
            contenedor = new BlobContainerClient(o.Blob.CadenaDeConexion, o.Blob.Contenedor, cliente);
        }
        catch (Exception)
        {
            // Sin la excepcion original: su texto podria repetir parte de la cadena de conexion, que lleva la clave de la cuenta.
            throw new InvalidOperationException("Almacen:Blob:CadenaDeConexion no es una cadena de conexion valida de Azure Storage.");
        }
    }

    private static string NombreDelBlob(string area, string nombre) => $"{area}/{nombre}";

    private static void Validar(string area, string nombre)
    {
        if (!AreasAlmacen.Existe(area))
            throw new ArgumentException("Area desconocida.", nameof(area));
        if (!NombresDeArchivo.EsValido(nombre))
            throw new ArgumentException("Nombre de archivo no valido.", nameof(nombre));
    }

    // La primera vez que se usa: crea el contenedor (privado) si no existe y comprueba que NO sea publico.
    private async Task AsegurarContenedorAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (verificado)
            return;
        await inicio.WaitAsync(ct);
        try
        {
            if (verificado)
                return;
            await contenedor.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            var propiedades = await contenedor.GetPropertiesAsync(cancellationToken: ct);
            if (propiedades.Value.PublicAccess is { } acceso && acceso != PublicAccessType.None)
                throw new InvalidOperationException(
                    $"El contenedor «{contenedor.Name}» de Azure Storage tiene acceso publico: no se usa. Cambielo a privado (sin acceso anonimo) y reinicie la API.");
            verificado = true;
        }
        finally
        {
            inicio.Release();
        }
    }

    public async Task GuardarAsync(string area, string nombre, byte[] datos, CancellationToken ct)
    {
        Validar(area, nombre);
        await AsegurarContenedorAsync(ct);
        // Un blob se confirma de una vez: nadie lo lee a medias, y una subida cancelada no deja nada.
        await contenedor.GetBlobClient(NombreDelBlob(area, nombre)).UploadAsync(BinaryData.FromBytes(datos), overwrite: true, ct);
    }

    public async Task<ArchivoAbierto?> AbrirAsync(string area, string nombre, CancellationToken ct)
    {
        if (!AreasAlmacen.Existe(area) || !NombresDeArchivo.EsValido(nombre))
            return null;
        await AsegurarContenedorAsync(ct);
        try
        {
            var respuesta = await contenedor.GetBlobClient(NombreDelBlob(area, nombre)).DownloadStreamingAsync(cancellationToken: ct);
            return new ArchivoAbierto(respuesta.Value.Content, respuesta.Value.Details.ContentLength, respuesta.Value.Details.LastModified);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> BorrarAsync(string area, string nombre)
    {
        if (!AreasAlmacen.Existe(area) || !NombresDeArchivo.EsValido(nombre))
            return false;
        try
        {
            return await contenedor.GetBlobClient(NombreDelBlob(area, nombre)).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);
        }
        catch (Exception e)
        {
            // Nunca lanza (lo que quede lo retira la purga). Solo el tipo y el codigo: el texto de la excepcion lleva direcciones de la cuenta.
            logger.LogWarning("No se pudo borrar un archivo del almacen en Blob ({Tipo}, estado {Estado}); lo retirara la purga.",
                e.GetType().Name, (e as RequestFailedException)?.Status);
            return false;
        }
    }

    public async Task<IReadOnlyList<ObjetoAlmacenado>> ListarAnterioresAsync(string area, DateTimeOffset limite, CancellationToken ct)
    {
        if (!AreasAlmacen.Existe(area))
            throw new ArgumentException("Area desconocida.", nameof(area));
        await AsegurarContenedorAsync(ct);
        var prefijo = area + "/";
        var antiguos = new List<ObjetoAlmacenado>();
        await foreach (var blob in contenedor.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefijo, ct))
        {
            var nombre = blob.Name[prefijo.Length..];
            if (!NombresDeArchivo.EsValido(nombre))
                continue;
            if (blob.Properties.LastModified is { } modificado && modificado < limite)
                antiguos.Add(new ObjetoAlmacenado(nombre, modificado));
        }
        return antiguos;
    }
}
