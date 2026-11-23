using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 16C-1 (H12, D1): los archivos de los clientes viven en un almacen con dos implementaciones (disco para desarrollo y compose; Blob para la
// nube gratuita, donde Render no tiene disco). Las dos cumplen EXACTAMENTE el mismo contrato: las pruebas del contrato se escriben una sola vez
// (PruebasDeContrato) y las ejecutan las dos. Las de Blob corren contra Azurite (el emulador oficial de Microsoft), nunca contra la nube.
//
// Las pruebas de Blob se omiten SOLO con SYNCTIME_OMITIR_BLOB=1 (el equipo sin Azurite); la CI no define esa variable y las exige: sin Azurite
// FALLAN, no se saltan. SYNCTIME_AZURITE_CADENA es la cadena de conexion del emulador (por omision «UseDevelopmentStorage=true», que apunta a
// 127.0.0.1:10000 y no lleva ninguna clave escrita aqui).

// Una prueba de Blob: se omite solo si se pide expresamente.
public sealed class FactBlobAttribute : FactAttribute
{
    public FactBlobAttribute()
    {
        if (Environment.GetEnvironmentVariable(AzuriteParaPruebas.VariableOmitir) == "1")
            Skip = "Omitida a pedido (SYNCTIME_OMITIR_BLOB=1): la CI no omite estas pruebas.";
    }
}

public static class AzuriteParaPruebas
{
    public const string VariableOmitir = "SYNCTIME_OMITIR_BLOB";
    public const string VariableCadena = "SYNCTIME_AZURITE_CADENA";

    public static string Cadena => Environment.GetEnvironmentVariable(VariableCadena) is { Length: > 0 } c ? c : "UseDevelopmentStorage=true";

    public static OpcionesAlmacen Opciones(string contenedor) => new()
    {
        Modo = OpcionesAlmacen.ModoBlob,
        Blob = new OpcionesBlob { CadenaDeConexion = Cadena, Contenedor = contenedor, TiempoEsperaSegundos = 10 }
    };

    public static AlmacenEnBlob Crear(string contenedor) => new(Options.Create(Opciones(contenedor)), NullLogger<AlmacenEnBlob>.Instance);

    public static string ContenedorNuevo() => $"pruebas-{Guid.NewGuid():N}"[..30];
}

internal static class PruebasDeContrato
{
    public static string Clave() => AlmacenAdjuntos.NuevaClave();

    public static async Task GuardarYAbrir_DevuelvenLosMismosBytes(IAlmacenArchivos almacen)
    {
        var nombre = $"{Clave()}.pdf";
        var datos = Encoding.UTF8.GetBytes("%PDF-1.4 contenido de prueba");
        var antes = DateTimeOffset.UtcNow.AddMinutes(-2);

        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, nombre, datos, CancellationToken.None);

        using var abierto = await almacen.AbrirAsync(AreasAlmacen.Adjuntos, nombre, CancellationToken.None);
        Assert.NotNull(abierto);
        Assert.Equal(datos.Length, abierto.Longitud);
        using var copia = new MemoryStream();
        await abierto.Flujo.CopyToAsync(copia);
        Assert.Equal(datos, copia.ToArray());
        Assert.True(abierto.UltimaModificacion > antes && abierto.UltimaModificacion < DateTimeOffset.UtcNow.AddMinutes(2));
    }

    public static async Task Abrir_DeUnArchivoQueNoExiste_DevuelveNull(IAlmacenArchivos almacen)
    {
        Assert.Null(await almacen.AbrirAsync(AreasAlmacen.Adjuntos, $"{Clave()}.pdf", CancellationToken.None));
    }

    public static async Task GuardarDosVeces_ConservaLaUltimaVersion(IAlmacenArchivos almacen)
    {
        var nombre = $"{Clave()}.csv";
        await almacen.GuardarAsync(AreasAlmacen.Exportaciones, nombre, "uno"u8.ToArray(), CancellationToken.None);
        await almacen.GuardarAsync(AreasAlmacen.Exportaciones, nombre, "dos"u8.ToArray(), CancellationToken.None);

        using var abierto = await almacen.AbrirAsync(AreasAlmacen.Exportaciones, nombre, CancellationToken.None);
        Assert.Equal("dos", await new StreamReader(abierto!.Flujo).ReadToEndAsync());
    }

    public static async Task LasAreasNoSeMezclan(IAlmacenArchivos almacen)
    {
        var nombre = $"{Clave()}-grande.webp";
        await almacen.GuardarAsync(AreasAlmacen.Imagenes, nombre, [1, 2, 3], CancellationToken.None);

        Assert.Null(await almacen.AbrirAsync(AreasAlmacen.Adjuntos, nombre, CancellationToken.None));
        using var propia = await almacen.AbrirAsync(AreasAlmacen.Imagenes, nombre, CancellationToken.None);
        Assert.NotNull(propia);
    }

    // Un nombre que arma la peticion nunca llega al almacen: ni al disco ni a la nube (no hay recorrido de carpetas posible).
    public static async Task UnNombreOUnAreaInvalidos_NoSeAceptan(IAlmacenArchivos almacen)
    {
        foreach (var nombre in new[] { "../appsettings.json", "..\\secreto.pdf", "ABC.pdf", "x.pdf", $"{Clave()}.exe", $"{Clave()}.pdf.tmp", $"{Clave()}.pdf\n", "", $"/{Clave()}.pdf" })
        {
            Assert.Null(await almacen.AbrirAsync(AreasAlmacen.Adjuntos, nombre, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => almacen.GuardarAsync(AreasAlmacen.Adjuntos, nombre, [1], CancellationToken.None));
            Assert.False(await almacen.BorrarAsync(AreasAlmacen.Adjuntos, nombre));
        }
        foreach (var area in new[] { "otra", "", "../adjuntos", "Adjuntos" })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => almacen.GuardarAsync(area, $"{Clave()}.pdf", [1], CancellationToken.None));
            Assert.Null(await almacen.AbrirAsync(area, $"{Clave()}.pdf", CancellationToken.None));
        }
    }

    public static async Task Borrar_QuitaElArchivo_YNuncaLanzaSiYaNoEsta(IAlmacenArchivos almacen)
    {
        var nombre = $"{Clave()}-miniatura.webp";
        await almacen.GuardarAsync(AreasAlmacen.Imagenes, nombre, [9], CancellationToken.None);

        Assert.True(await almacen.BorrarAsync(AreasAlmacen.Imagenes, nombre));
        Assert.Null(await almacen.AbrirAsync(AreasAlmacen.Imagenes, nombre, CancellationToken.None));
        Assert.False(await almacen.BorrarAsync(AreasAlmacen.Imagenes, nombre));
    }

    // Los archivos se listan por su fecha (la purga retira los que quedaron sin registro): con un limite futuro aparece todo lo guardado;
    // con uno pasado, nada. (Una fecha antigua no se puede fabricar en Blob; en disco se prueba aparte.)
    public static async Task Listar_RespetaElLimiteDeAntiguedad_YElArea(IAlmacenArchivos almacen)
    {
        var propio = $"{Clave()}.xlsx";
        var ajeno = $"{Clave()}.pdf";
        await almacen.GuardarAsync(AreasAlmacen.Exportaciones, propio, [1], CancellationToken.None);
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, ajeno, [1], CancellationToken.None);

        var futuros = await almacen.ListarAnterioresAsync(AreasAlmacen.Exportaciones, DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);
        Assert.Contains(futuros, o => o.Nombre == propio);
        Assert.DoesNotContain(futuros, o => o.Nombre == ajeno);

        var pasados = await almacen.ListarAnterioresAsync(AreasAlmacen.Exportaciones, DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);
        Assert.DoesNotContain(pasados, o => o.Nombre == propio);
    }

    public static async Task UnaPeticionCancelada_NoDejaElArchivoAMedias(IAlmacenArchivos almacen)
    {
        var nombre = $"{Clave()}.pdf";
        using var cancelada = new CancellationTokenSource();
        await cancelada.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => almacen.GuardarAsync(AreasAlmacen.Adjuntos, nombre, new byte[100_000], cancelada.Token));

        Assert.Null(await almacen.AbrirAsync(AreasAlmacen.Adjuntos, nombre, CancellationToken.None));
    }
}

public sealed class AlmacenEnDiscoTests : IDisposable
{
    private readonly string raiz = Path.Combine(Path.GetTempPath(), $"synctime-almacen-{Guid.NewGuid():N}");
    private readonly AlmacenEnDisco almacen;

    public AlmacenEnDiscoTests()
    {
        almacen = new AlmacenEnDisco(area => Path.Combine(raiz, area));
    }

    public void Dispose()
    {
        if (Directory.Exists(raiz))
            Directory.Delete(raiz, recursive: true);
    }

    [Fact] public Task GuardarYAbrir() => PruebasDeContrato.GuardarYAbrir_DevuelvenLosMismosBytes(almacen);
    [Fact] public Task AbrirInexistente() => PruebasDeContrato.Abrir_DeUnArchivoQueNoExiste_DevuelveNull(almacen);
    [Fact] public Task Sobrescribir() => PruebasDeContrato.GuardarDosVeces_ConservaLaUltimaVersion(almacen);
    [Fact] public Task Areas() => PruebasDeContrato.LasAreasNoSeMezclan(almacen);
    [Fact] public Task NombresInvalidos() => PruebasDeContrato.UnNombreOUnAreaInvalidos_NoSeAceptan(almacen);
    [Fact] public Task Borrar() => PruebasDeContrato.Borrar_QuitaElArchivo_YNuncaLanzaSiYaNoEsta(almacen);
    [Fact] public Task Listar() => PruebasDeContrato.Listar_RespetaElLimiteDeAntiguedad_YElArea(almacen);
    [Fact] public Task Cancelada() => PruebasDeContrato.UnaPeticionCancelada_NoDejaElArchivoAMedias(almacen);

    [Fact]
    public async Task GuardarNoDejaTemporales_NiAunQueSeSobrescriba()
    {
        var nombre = $"{PruebasDeContrato.Clave()}.pdf";
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, nombre, [1], CancellationToken.None);
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, nombre, [2], CancellationToken.None);

        Assert.Equal([nombre], Directory.GetFiles(Path.Combine(raiz, AreasAlmacen.Adjuntos)).Select(Path.GetFileName));
    }

    [Fact]
    public async Task LaPurga_VeYBorraLosTemporalesAbandonados_YNoTocaOtrosArchivos()
    {
        var carpeta = Path.Combine(raiz, AreasAlmacen.Exportaciones);
        Directory.CreateDirectory(carpeta);
        var viejo = $"{PruebasDeContrato.Clave()}.xlsx.tmp";
        var ajeno = "notas.txt";
        File.WriteAllText(Path.Combine(carpeta, viejo), "a medias");
        File.WriteAllText(Path.Combine(carpeta, ajeno), "no es de la API");
        File.SetLastWriteTimeUtc(Path.Combine(carpeta, viejo), DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(Path.Combine(carpeta, ajeno), DateTime.UtcNow.AddDays(-3));

        var antiguos = await almacen.ListarAnterioresAsync(AreasAlmacen.Exportaciones, DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);

        Assert.Equal([viejo], antiguos.Select(o => o.Nombre));
        Assert.True(await almacen.BorrarAsync(AreasAlmacen.Exportaciones, viejo));
        Assert.True(File.Exists(Path.Combine(carpeta, ajeno)));
    }
}

[Trait("Almacen", "Blob")]
public sealed class AlmacenEnBlobTests : IAsyncLifetime
{
    private readonly string contenedor = AzuriteParaPruebas.ContenedorNuevo();
    private AlmacenEnBlob almacen = null!;

    public Task InitializeAsync()
    {
        almacen = AzuriteParaPruebas.Crear(contenedor);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Environment.GetEnvironmentVariable(AzuriteParaPruebas.VariableOmitir) == "1")
            return;
        await new BlobContainerClient(AzuriteParaPruebas.Cadena, contenedor).DeleteIfExistsAsync();
    }

    [FactBlob] public Task GuardarYAbrir() => PruebasDeContrato.GuardarYAbrir_DevuelvenLosMismosBytes(almacen);
    [FactBlob] public Task AbrirInexistente() => PruebasDeContrato.Abrir_DeUnArchivoQueNoExiste_DevuelveNull(almacen);
    [FactBlob] public Task Sobrescribir() => PruebasDeContrato.GuardarDosVeces_ConservaLaUltimaVersion(almacen);
    [FactBlob] public Task Areas() => PruebasDeContrato.LasAreasNoSeMezclan(almacen);
    [FactBlob] public Task NombresInvalidos() => PruebasDeContrato.UnNombreOUnAreaInvalidos_NoSeAceptan(almacen);
    [FactBlob] public Task Borrar() => PruebasDeContrato.Borrar_QuitaElArchivo_YNuncaLanzaSiYaNoEsta(almacen);
    [FactBlob] public Task Listar() => PruebasDeContrato.Listar_RespetaElLimiteDeAntiguedad_YElArea(almacen);
    [FactBlob] public Task Cancelada() => PruebasDeContrato.UnaPeticionCancelada_NoDejaElArchivoAMedias(almacen);

    // Fallo a proposito (plan 16C, seccion 7): lo que se guardo sobrevive a que el proceso se reinicie (en Render gratuito, cada
    // reinicio borra el disco). Una instancia NUEVA del almacen, igual que tras un reinicio, lee lo que guardo la anterior.
    [FactBlob]
    public async Task UnAdjunto_SobreviveAlReinicioDelProceso()
    {
        var nombre = $"{PruebasDeContrato.Clave()}.pdf";
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, nombre, "%PDF-1.4 del cliente"u8.ToArray(), CancellationToken.None);

        var trasElReinicio = AzuriteParaPruebas.Crear(contenedor);

        using var abierto = await trasElReinicio.AbrirAsync(AreasAlmacen.Adjuntos, nombre, CancellationToken.None);
        Assert.NotNull(abierto);
        Assert.Equal("%PDF-1.4 del cliente", await new StreamReader(abierto.Flujo).ReadToEndAsync());
    }

    [FactBlob]
    public async Task ElContenedorSeCreaPrivado_SinAccesoPublico()
    {
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{PruebasDeContrato.Clave()}.pdf", [1], CancellationToken.None);

        var propiedades = await new BlobContainerClient(AzuriteParaPruebas.Cadena, contenedor).GetPropertiesAsync();

        Assert.True(propiedades.Value.PublicAccess is null or PublicAccessType.None);
    }

    // Fallo a proposito: un contenedor que alguien dejo PUBLICO (cualquiera con la direccion bajaria los adjuntos de los clientes) hace que
    // el almacen se niegue a usarlo, en vez de seguir guardando en silencio.
    [FactBlob]
    public async Task UnContenedorPublico_SeRechaza_YNoSeGuardaNada()
    {
        var publico = AzuriteParaPruebas.ContenedorNuevo();
        var cliente = new BlobContainerClient(AzuriteParaPruebas.Cadena, publico);
        await cliente.CreateIfNotExistsAsync(PublicAccessType.Blob);
        try
        {
            var inseguro = AzuriteParaPruebas.Crear(publico);
            var nombre = $"{PruebasDeContrato.Clave()}.pdf";

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => inseguro.GuardarAsync(AreasAlmacen.Adjuntos, nombre, [1], CancellationToken.None));

            Assert.Contains("publico", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False((await cliente.GetBlobClient($"{AreasAlmacen.Adjuntos}/{nombre}").ExistsAsync()).Value);
            // Tambien al leer: no se sirve nada desde un contenedor inseguro.
            await Assert.ThrowsAsync<InvalidOperationException>(() => inseguro.AbrirAsync(AreasAlmacen.Adjuntos, nombre, CancellationToken.None));
        }
        finally
        {
            await cliente.DeleteIfExistsAsync();
        }
    }

}

// No necesitan Azurite: apuntan a un puerto sin servidor.
public sealed class AlmacenEnBlobSinNubeTests
{
    // Fallo a proposito: la nube no responde (puerto cerrado). El error es claro y NO repite la cadena de conexion (lleva la clave de la cuenta).
    [Fact]
    public async Task SiLaNubeNoResponde_ElErrorNoMuestraLaCadenaDeConexion()
    {
        const string cadenaFalsa = "DefaultEndpointsProtocol=http;AccountName=cuentafalsa;AccountKey=Y2xhdmUtZmFsc2EtZGUtcHJ1ZWJhcw==;BlobEndpoint=http://127.0.0.1:1/cuentafalsa;";
        var opciones = new OpcionesAlmacen
        {
            Modo = OpcionesAlmacen.ModoBlob,
            Blob = new OpcionesBlob { CadenaDeConexion = cadenaFalsa, Contenedor = "archivos", TiempoEsperaSegundos = 2, ReintentosMaximos = 0 }
        };
        var almacenCaido = new AlmacenEnBlob(Options.Create(opciones), NullLogger<AlmacenEnBlob>.Instance);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => almacenCaido.GuardarAsync(AreasAlmacen.Adjuntos, $"{PruebasDeContrato.Clave()}.pdf", [1], CancellationToken.None));

        Assert.DoesNotContain("Y2xhdmUtZmFsc2E", error.ToString());
        Assert.DoesNotContain("AccountKey", error.ToString());
        // Borrar y listar nunca lanzan por un problema de la nube: la purga lo reintenta al dia siguiente.
        Assert.False(await almacenCaido.BorrarAsync(AreasAlmacen.Adjuntos, $"{PruebasDeContrato.Clave()}.pdf"));
    }
}

public sealed class OpcionesAlmacenTests
{
    [Fact]
    public void PorOmision_ElModoEsDisco_YEsValido()
    {
        var o = new OpcionesAlmacen();

        Assert.Equal("Disco", o.Modo);
        Assert.Null(o.Problema());
        Assert.False(o.EsBlob);
    }

    [Theory]
    [InlineData("Disco")]
    [InlineData("disco")]
    [InlineData("BLOB")]
    public void LosModosConocidos_Validan(string modo)
    {
        var o = new OpcionesAlmacen { Modo = modo, Blob = new OpcionesBlob { CadenaDeConexion = "UseDevelopmentStorage=true" } };

        Assert.Null(o.Problema());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Nube")]
    [InlineData("S3")]
    public void UnModoDesconocido_EsUnProblema(string modo)
    {
        Assert.Contains("Almacen:Modo", new OpcionesAlmacen { Modo = modo }.Problema());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ModoBlobSinCadenaDeConexion_EsUnProblema(string? cadena)
    {
        var o = new OpcionesAlmacen { Modo = "Blob", Blob = new OpcionesBlob { CadenaDeConexion = cadena } };

        Assert.Contains("Almacen:Blob:CadenaDeConexion", o.Problema());
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("Archivos")]
    [InlineData("-archivos")]
    [InlineData("archivos-")]
    [InlineData("arch--ivos")]
    [InlineData("arch_ivos")]
    [InlineData("a/b")]
    public void UnNombreDeContenedorInvalido_EsUnProblema(string contenedor)
    {
        var o = new OpcionesAlmacen { Modo = "Blob", Blob = new OpcionesBlob { CadenaDeConexion = "UseDevelopmentStorage=true", Contenedor = contenedor } };

        Assert.Contains("Almacen:Blob:Contenedor", o.Problema());
    }

    [Fact]
    public void ElProblemaNuncaRepiteLaCadenaDeConexion()
    {
        const string secreta = "AccountKey=SECRETISIMA";
        var o = new OpcionesAlmacen { Modo = "Blob", Blob = new OpcionesBlob { CadenaDeConexion = secreta, Contenedor = "MAL NOMBRE" } };

        Assert.DoesNotContain("SECRETISIMA", o.Problema());
        Assert.DoesNotContain("SECRETISIMA", o.Blob.ToString());
        Assert.DoesNotContain("SECRETISIMA", o.ToString());
    }
}

public sealed class NombresDeArchivoTests
{
    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef-grande.webp")]
    [InlineData("0123456789abcdef0123456789abcdef-miniatura.webp")]
    [InlineData("0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("0123456789abcdef0123456789abcdef.xlsx")]
    [InlineData("0123456789abcdef0123456789abcdef.csv")]
    public void LosNombresDeLaAplicacion_SonValidos(string nombre)
    {
        Assert.True(NombresDeArchivo.EsValido(nombre));
        Assert.False(NombresDeArchivo.EsValido(nombre + ".tmp"));
        Assert.True(NombresDeArchivo.EsValido(nombre + ".tmp", permitirTemporal: true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF.pdf")]
    [InlineData("0123456789abcdef0123456789abcde.pdf")]
    [InlineData("0123456789abcdef0123456789abcdef.exe")]
    [InlineData("0123456789abcdef0123456789abcdef.pdf\n")]
    [InlineData("0123456789abcdef0123456789abcdef.pdf/../x")]
    public void CualquierOtroNombre_NoSeAcepta(string nombre)
    {
        Assert.False(NombresDeArchivo.EsValido(nombre, permitirTemporal: true));
    }
}

// Las tres clases que conocen los nombres de la aplicacion hablan con el almacen: aqui se comprueba con el almacen en disco que siguen
// guardando, sirviendo y purgando lo mismo que antes.
public sealed class FachadasDelAlmacenTests : IDisposable
{
    private readonly string raiz = Path.Combine(Path.GetTempPath(), $"synctime-fachadas-{Guid.NewGuid():N}");
    private readonly IAlmacenArchivos almacen;

    public FachadasDelAlmacenTests()
    {
        almacen = new AlmacenEnDisco(area => Path.Combine(raiz, area));
    }

    public void Dispose()
    {
        if (Directory.Exists(raiz))
            Directory.Delete(raiz, recursive: true);
    }

    [Fact]
    public async Task Adjuntos_GuardaElPdf_ElClienteSoloLoAbreConSuClaveExacta()
    {
        var adjuntos = new AlmacenAdjuntos(almacen);
        var clave = AlmacenAdjuntos.NuevaClave();
        await adjuntos.GuardarPdfAsync(clave, "%PDF"u8.ToArray(), CancellationToken.None);

        using var archivo = await adjuntos.AbrirAsync(clave, esPdf: true, VersionAdjunto.Archivo, CancellationToken.None);
        Assert.NotNull(archivo);
        // Un PDF no tiene miniatura, y una clave con formato ajeno no llega al almacen.
        Assert.Null(await adjuntos.AbrirAsync(clave, esPdf: true, VersionAdjunto.Miniatura, CancellationToken.None));
        Assert.Null(await adjuntos.AbrirAsync("../x", esPdf: true, VersionAdjunto.Archivo, CancellationToken.None));
        Assert.Null(await adjuntos.AbrirAsync(clave.ToUpperInvariant(), esPdf: true, VersionAdjunto.Archivo, CancellationToken.None));
    }

    [Fact]
    public async Task Adjuntos_BorrarQuitaLasTresVersiones_YLasAntiguasSeListanPorClave()
    {
        var adjuntos = new AlmacenAdjuntos(almacen);
        var clave = AlmacenAdjuntos.NuevaClave();
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{clave}-grande.webp", [1], CancellationToken.None);
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{clave}-miniatura.webp", [1], CancellationToken.None);
        await almacen.GuardarAsync(AreasAlmacen.Adjuntos, $"{clave}.pdf", [1], CancellationToken.None);

        var claves = await adjuntos.ClavesAnterioresAAsync(DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);
        Assert.Equal([clave], claves);

        await adjuntos.BorrarAsync(clave);

        Assert.Empty(await adjuntos.ClavesAnterioresAAsync(DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None));
    }

    [Fact]
    public async Task Imagenes_SoloSirveNombresConElFormatoExacto()
    {
        var imagenes = new AlmacenImagenes(almacen);
        var clave = AlmacenImagenes.NuevaClave();
        await almacen.GuardarAsync(AreasAlmacen.Imagenes, AlmacenImagenes.NombreGrande(clave), [1, 2], CancellationToken.None);

        using var servida = await imagenes.AbrirAsync(AlmacenImagenes.NombreGrande(clave), CancellationToken.None);
        Assert.NotNull(servida);
        Assert.Null(await imagenes.AbrirAsync("../appsettings.json", CancellationToken.None));
        Assert.Null(await imagenes.AbrirAsync($"{clave}.pdf", CancellationToken.None));
    }

    [Fact]
    public async Task Exportaciones_GuardaAbreYBorra_YUnFormatoDesconocidoNoProduceNada()
    {
        var exportaciones = new AlmacenExportaciones(almacen);
        var id = Guid.NewGuid();
        await exportaciones.GuardarAsync(id, "csv", "a,b"u8.ToArray(), CancellationToken.None);

        using (var abierto = await exportaciones.AbrirAsync(id, "csv", CancellationToken.None))
            Assert.NotNull(abierto);
        Assert.Null(await exportaciones.AbrirAsync(id, "exe", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => exportaciones.GuardarAsync(id, "exe", [1], CancellationToken.None));

        await exportaciones.BorrarAsync(id, "csv");
        Assert.Null(await exportaciones.AbrirAsync(id, "csv", CancellationToken.None));
    }

    [Fact]
    public async Task Exportaciones_LaPurgaBorraSoloLoVencido_YDevuelveCuantos()
    {
        var exportaciones = new AlmacenExportaciones(almacen);
        var viejo = Guid.NewGuid();
        var nuevo = Guid.NewGuid();
        await exportaciones.GuardarAsync(viejo, "xlsx", [1], CancellationToken.None);
        await exportaciones.GuardarAsync(nuevo, "xlsx", [1], CancellationToken.None);
        File.SetLastWriteTimeUtc(Path.Combine(raiz, AreasAlmacen.Exportaciones, $"{viejo:N}.xlsx"), DateTime.UtcNow.AddDays(-2));

        var borrados = await exportaciones.PurgarHuerfanosAsync(DateTime.UtcNow.AddHours(-1), CancellationToken.None);

        Assert.Equal(1, borrados);
        Assert.Null(await exportaciones.AbrirAsync(viejo, "xlsx", CancellationToken.None));
        Assert.NotNull(await exportaciones.AbrirAsync(nuevo, "xlsx", CancellationToken.None));
    }
}
