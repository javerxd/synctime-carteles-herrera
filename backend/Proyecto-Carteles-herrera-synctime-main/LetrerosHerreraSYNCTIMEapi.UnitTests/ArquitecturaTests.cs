namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Reglas de la arquitectura en capas (RNFO10), comprobadas sobre el codigo fuente:
//   Api (Endpoints, Middleware, Api)  ->  Negocio (Negocio, Services)  ->  Datos (Datos, Repositories, Models)
// Cada capa solo conoce a las que tiene a su derecha.
public class ArquitecturaTests
{
    // Archivos de Endpoints/ que todavia consultan la base directamente, sin pasar por un servicio de
    // negocio y un repositorio. Vienen de antes del plan y se migran en la fase que rehace cada modulo.
    // Esta lista solo puede achicarse: al terminar la fase 17 debe estar vacia.
    private static readonly string[] EndpointsPendientesDeMigrar =
    [
        "AuthApi.cs",          // fase 6
        "CatalogoApi.cs",      // fases 5 y 9
        "CotizacionApi.cs",    // fase 8
        "PedidoApi.cs",        // fase 8
        "PortalClienteApi.cs", // fases 7 y 8
        "Reautenticacion.cs",  // fase 6
        "RostroApi.cs",        // fase 10
        "SolicitudApi.cs"      // fase 7
    ];

    private static readonly string Proyecto = UbicarProyecto();

    private static string UbicarProyecto()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "LetrerosHerreraSYNCTIMEapi.slnx")))
            carpeta = carpeta.Parent;
        Assert.True(carpeta is not null, "No se encontro la carpeta de la solucion.");
        return Path.Combine(carpeta!.FullName, "LetrerosHerreraSYNCTIMEapi");
    }

    private static IEnumerable<(string Nombre, string Texto)> Fuentes(string carpeta) =>
        Directory.EnumerateFiles(Path.Combine(Proyecto, carpeta), "*.cs", SearchOption.AllDirectories)
            .Select(ruta => (Path.GetFileName(ruta), File.ReadAllText(ruta)));

    private static bool UsaLaBase(string texto) => texto.Contains("LetrerosHerreradbContext");

    [Fact]
    public void UnEndpointNuevo_NoConsultaLaBaseDirectamente()
    {
        var infractores = Fuentes("Endpoints")
            .Where(f => UsaLaBase(f.Texto) && !EndpointsPendientesDeMigrar.Contains(f.Nombre))
            .Select(f => f.Nombre);

        Assert.Empty(infractores);
    }

    [Fact]
    public void LaListaDePendientes_NoConservaModulosYaMigrados()
    {
        var fuentes = Fuentes("Endpoints").ToDictionary(f => f.Nombre, f => f.Texto);

        // Si un archivo ya no usa la base, hay que quitarlo de la lista para que no pueda volver atras.
        var obsoletos = EndpointsPendientesDeMigrar.Where(nombre => !fuentes.TryGetValue(nombre, out var texto) || !UsaLaBase(texto));

        Assert.Empty(obsoletos);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("LetrerosHerreradbContext")]
    [InlineData("IResult")]
    public void ElNegocio_NoConoceHttpNiLaBase(string prohibido)
    {
        var infractores = Fuentes("Negocio").Where(f => f.Texto.Contains(prohibido)).Select(f => f.Nombre);
        Assert.Empty(infractores);
    }

    [Theory]
    [InlineData("LetrerosHerreraSYNCTIMEapi.Api")]
    [InlineData("LetrerosHerreraSYNCTIMEapi.Endpoints")]
    [InlineData("Microsoft.AspNetCore.Http")]
    [InlineData("IResult")]
    public void LosDatos_NoConocenLaCapaDeApi(string prohibido)
    {
        var infractores = Fuentes("Datos").Concat(Fuentes("Repositories"))
            .Where(f => f.Texto.Contains(prohibido)).Select(f => f.Nombre);
        Assert.Empty(infractores);
    }
}
