using System.Net;
using System.Text.Json;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 16C-1: la API para la nube gratuita. Estas pruebas levantan la API completa (contra la base aislada de las pruebas, nunca la real) en
// el entorno de produccion y comprueban lo que decide Program.cs: el almacen de archivos, el correo por HTTPS y el arranque con la base en pausa.
// Nada llama a Azure, a Brevo ni a ninguna nube: la cadena de Blob del emulador solo se arma (no se conecta al arrancar) y la base «dormida» es un
// puerto cerrado de este mismo equipo.
[Collection(ApiCollection.Name)]
public sealed class Fase16c1Tests(ApiFixture api)
{
    private const string OrigenVercel = "https://synctime.vercel.app";

    private static Dictionary<string, string> Produccion(params (string Clave, string? Valor)[] cambios)
    {
        var variables = new Dictionary<string, string>
        {
            ["Cors__AllowedOrigins__0"] = OrigenVercel,
            ["Sitio__UrlPublica"] = OrigenVercel,
        };
        foreach (var (clave, valor) in cambios)
        {
            if (valor is null)
                variables.Remove(clave);
            else
                variables[clave] = valor;
        }
        return variables;
    }

    private static string Mensajes(Exception e) => ProxyFase16aTests.Mensajes(e);

    // ── El almacen de archivos (H12) ──

    [Fact]
    public async Task EnProduccion_ConElAlmacenEnDisco_Arranca()
    {
        await using var otra = api.ConEntorno("Production", Produccion(("Almacen__Modo", "Disco")));

        using var respuesta = await otra.Cliente().GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // Fallo a proposito: Render gratuito no tiene disco persistente. Si alguien olvida declarar el modo, la API NO puede quedar guardando los
    // adjuntos de los clientes en un disco que se pierde al reiniciar.
    [Fact]
    public void EnProduccion_SinDeclararElModoDelAlmacen_NoArranca()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Almacen__Modo", ""))));

        Assert.Contains("Almacen:Modo", Mensajes(error));
    }

    [Theory]
    [InlineData("Nube")]
    [InlineData("S3")]
    public void UnModoDeAlmacenDesconocido_NoArranca(string modo)
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Almacen__Modo", modo))));

        Assert.Contains("Almacen:Modo", Mensajes(error));
    }

    // Fallo a proposito: «una variable de Blob faltante en produccion» (como CORS): la API no arranca y el mensaje dice cual.
    [Fact]
    public void EnProduccion_ConBlobSinCadenaDeConexion_NoArranca()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Almacen__Modo", "Blob"), ("Almacen__Blob__CadenaDeConexion", ""))));

        Assert.Contains("Almacen:Blob:CadenaDeConexion", Mensajes(error));
    }

    // Una cadena mal pegada debe impedir arrancar, no fallar recien cuando un cliente sube su primer adjunto.
    [Fact]
    public void ConBlobYUnaCadenaQueNoEsDeAzure_NoArranca_YElMensajeNoLaRepite()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(
            ("Almacen__Modo", "Blob"), ("Almacen__Blob__CadenaDeConexion", "esto-no-es-una-cadena-SECRETISIMA"))));

        Assert.Contains("Almacen:Blob:CadenaDeConexion", Mensajes(error));
        Assert.DoesNotContain("SECRETISIMA", Mensajes(error));
    }

    [Fact]
    public void ConBlobYUnContenedorMalEscrito_NoArranca_YElMensajeNoRepiteLaCadena()
    {
        const string cadena = "DefaultEndpointsProtocol=https;AccountName=cuenta;AccountKey=SECRETISIMA-NO-REAL";

        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(
            ("Almacen__Modo", "Blob"), ("Almacen__Blob__CadenaDeConexion", cadena), ("Almacen__Blob__Contenedor", "Mis Archivos"))));

        Assert.Contains("Almacen:Blob:Contenedor", Mensajes(error));
        Assert.DoesNotContain("SECRETISIMA", Mensajes(error));
    }

    [Fact]
    public async Task ConBlobBienConfigurado_ArrancaSinConectarseALaNube()
    {
        // «UseDevelopmentStorage=true» es el emulador local (Azurite): no hace falta que este corriendo para que la API arranque, porque
        // el contenedor se comprueba al primer uso, no al arrancar.
        await using var otra = api.ConEntorno("Production", Produccion(("Almacen__Modo", "Blob"), ("Almacen__Blob__CadenaDeConexion", "UseDevelopmentStorage=true")));

        using var respuesta = await otra.Cliente().GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // ── El correo por HTTPS (H11, D8) ──

    [Fact]
    public async Task ElCorreoPorApi_SinLaClave_ArrancaPeroSeDeclaraDegradado()
    {
        // Igual que el SMTP sin cuenta: la API sigue en pie y /salud/correo avisa que los avisos no pueden salir.
        await using var otra = api.ConEntorno("Production", Produccion(("Correo__Modo", "Api"), ("Correo__Api__Clave", "")));

        using var respuesta = await otra.Cliente().GetAsync("/salud/correo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("degradado", json.RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task ElCorreoPorApi_ConLaClave_ArrancaYLaRutaNoLaMuestra()
    {
        const string clave = "clave-de-prueba-que-no-es-real";
        await using var otra = api.ConEntorno("Production", Produccion(("Correo__Modo", "Api"), ("Correo__Api__Clave", clave), ("Correo__AvisosTaller", ApiFixture.AvisosTaller)));

        using var respuesta = await otra.Cliente().GetAsync("/salud/correo");

        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.DoesNotContain(clave, texto);
    }

    [Fact]
    public void ElCorreoPorApi_ConUnProveedorDesconocido_NoArranca()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Correo__Modo", "Api"), ("Correo__Api__Proveedor", "Resend"))));

        Assert.Contains("Correo:Api:Proveedor", Mensajes(error));
    }

    [Fact]
    public void ElCorreoPorApi_ConElContactoPublicoComoRemitente_NoArranca()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(
            ("Correo__Modo", "Api"), ("Correo__Remitente__Direccion", "contacto.letrerosherrera@gmail.com"))));

        Assert.Contains("Correo:ContactoTaller", Mensajes(error));
    }

    // ── El arranque con la base en pausa (H14) ──

    // Fallo a proposito: la base de Azure SQL esta en pausa (aqui, un puerto cerrado). Antes la API NO levantaba (la revision de migraciones
    // al arrancar lanzaba); ahora el proceso abre el puerto, /salud responde, /salud/lista dice que la base no esta lista y la API NO se cae.
    [Fact]
    public async Task ConLaBaseEnPausa_LaApiLevanta_SaludResponde_YListaDiceQueNoEstaLista()
    {
        const string baseDormida = "Server=127.0.0.1,1;Database=synctime_pruebas_dormida;User Id=sa;Password=no-es-real;Connect Timeout=2;TrustServerCertificate=True";
        await using var otra = api.ConEntorno("Production", Produccion(
            ("ConnectionStrings__DefaultConnection", baseDormida),
            ("BaseDeDatos__ReintentosMaximos", "0"),
            ("BaseDeDatos__ArranqueEsperaInicialSegundos", "1"),
            ("BaseDeDatos__ArranqueEsperaMaximaSegundos", "2")));
        var cliente = otra.Cliente();

        using var viva = await cliente.GetAsync("/salud");
        Assert.Equal(HttpStatusCode.OK, viva.StatusCode);

        using var lista = await cliente.GetAsync("/salud/lista");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, lista.StatusCode);

        // Los pasos de arranque reintentan en segundo plano: tras unos segundos de intentos fallidos, el servidor sigue respondiendo.
        await Task.Delay(TimeSpan.FromSeconds(4));
        using var sigueViva = await cliente.GetAsync("/salud");
        Assert.Equal(HttpStatusCode.OK, sigueViva.StatusCode);
    }

    [Theory]
    [InlineData("BaseDeDatos__ArranqueMinutosMaximos", "0")]
    [InlineData("BaseDeDatos__ArranqueMinutosMaximos", "999")]
    [InlineData("BaseDeDatos__ArranqueEsperaInicialSegundos", "0")]
    public void UnPlazoDeArranqueFueraDeRango_NoArranca(string variable, string valor)
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion((variable, valor))));

        Assert.Contains(variable.Replace("__", ":"), Mensajes(error));
    }

    // La configuracion invalida del primer administrador sigue impidiendo arrancar aunque la base este dormida: no necesita la base para saberlo.
    [Fact]
    public void UnaClaveDelPrimerAdministradorInvalida_NoArranca_SinTocarLaBase()
    {
        var error = Assert.ThrowsAny<Exception>(() => api.ConEntorno("Production", Produccion(("Bootstrap__Admin__Password", "corta"))));

        Assert.Contains("Bootstrap:Admin:Password", Mensajes(error));
    }
}
