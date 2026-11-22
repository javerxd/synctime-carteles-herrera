using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 15A (RNFP01, RFU27): con mas de mil clientes, ningun listado trae todo, la busqueda la hace el servidor y responde rapido.
// Los clientes son SINTETICOS (nombres inventados, telefonos que empiezan con 8) y se borran al terminar: el resto de las
// pruebas comparte esta base, y la coleccion corre una prueba a la vez, asi que nadie los ve.
// Se insertan con una sola sentencia SQL y no con EF: el interceptor de auditoria escribiria 1.500 filas de bitacora que no se pueden borrar.
[Collection(ApiCollection.Name)]
public sealed class RendimientoFase15aTests(ApiFixture api)
{
    private const string MarcaSintetica = "sintetico-15a";
    private const int CantidadDeClientes = 1500;
    // El ultimo por orden alfabetico de apellido: nunca esta entre los primeros 100.
    private const string ApellidoDelUltimo = "Zzsintetico";
    private const string ApellidoConDatosValidos = "Zzvalido";
    private const string TelefonoValido = "987654321";
    private const string RutValido = "12345678-5";

    private async Task<T> ConClientesSinteticosAsync<T>(Func<HttpClient, Task<T>> prueba)
    {
        await api.ConsultarAsync(db => db.Database.ExecuteSqlRawAsync($"""
            ;WITH numeros AS (SELECT value AS i FROM GENERATE_SERIES(1, {CantidadDeClientes}))
            INSERT INTO clientes (nombre, apellido, telefono, rut, direccion)
            SELECT CHOOSE((i % 12) + 1, N'Ana', N'Luis', N'María', N'Pedro', N'Carla', N'Jorge', N'Paula', N'Diego', N'Rosa', N'Mario', N'Elena', N'Hugo'),
                   CASE WHEN i = {CantidadDeClientes} THEN N'{ApellidoDelUltimo}'
                        ELSE CHOOSE((i % 15) + 1, N'González', N'Muñoz', N'Rojas', N'Díaz', N'Pérez', N'Soto', N'Contreras', N'Silva', N'Martínez',
                                    N'Sepúlveda', N'Morales', N'Rodríguez', N'López', N'Fuentes', N'Hernández') END,
                   '8' + RIGHT('00000000' + CAST(i AS varchar(8)), 8),
                   CASE WHEN i % 2 = 0 THEN CAST(40000000 + i AS varchar(8)) + '-0' END,   -- la mitad con un RUT inventado (sin digito verificador valido)
                   N'{MarcaSintetica}'
            FROM numeros;
            -- Un cliente con un telefono y un RUT validos, para buscarlos escritos de varias formas.
            INSERT INTO clientes (nombre, apellido, telefono, rut, direccion) VALUES (N'Olga', N'{ApellidoConDatosValidos}', '{TelefonoValido}', '{RutValido}', N'{MarcaSintetica}');
            """));
        try
        {
            return await prueba(api.Cliente(await api.TokenAdminAsync()));
        }
        finally
        {
            await api.ConsultarAsync(db => db.Database.ExecuteSqlRawAsync($"DELETE FROM clientes WHERE direccion = N'{MarcaSintetica}'"));
        }
    }

    [Fact]
    public Task ElListado_ConMilQuinientosClientes_NuncaTraeMasDeUnaPagina() => ConClientesSinteticosAsync(async admin =>
    {
        var completo = await admin.GetAsync("/api/v1/clientes?tamano=100000&pagina=1").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(100, completo.GetProperty("elementos").GetArrayLength());
        Assert.True(completo.GetProperty("total").GetInt32() >= CantidadDeClientes);
        Assert.True(completo.GetProperty("totalPaginas").GetInt32() >= CantidadDeClientes / 100);

        var porOmision = await admin.GetAsync("/api/v1/clientes").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(20, porOmision.GetProperty("elementos").GetArrayLength());
        return true;
    });

    [Fact]
    public Task LaBusqueda_EncuentraAUnClienteQueNoEstaEnLosPrimeros100() => ConClientesSinteticosAsync(async admin =>
    {
        // La lista de 100 que usaba el filtro «Cliente» (RFU27) no lo trae...
        var primeros = await admin.GetAsync("/api/v1/clientes?tamano=100").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(primeros.GetProperty("elementos").EnumerateArray(),
            c => c.GetProperty("apellido").GetString() == ApellidoDelUltimo);

        // ...y la busqueda en el servidor si, por nombre y por telefono.
        var porNombre = await admin.GetAsync($"/api/v1/clientes?q={ApellidoDelUltimo}&tamano=10").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, porNombre.GetProperty("total").GetInt32());
        Assert.Equal(ApellidoDelUltimo, porNombre.GetProperty("elementos")[0].GetProperty("apellido").GetString());

        var telefono = porNombre.GetProperty("elementos")[0].GetProperty("telefono").GetString()!;
        var porTelefono = await admin.GetAsync($"/api/v1/clientes?q={telefono}&tamano=10").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, porTelefono.GetProperty("total").GetInt32());
        return true;
    });

    [Fact]
    public Task LaBusqueda_ConVariosResultados_PaginaYCuentaElTotalDelServidor() => ConClientesSinteticosAsync(async admin =>
    {
        // Un nombre de pila repetido (1 de cada 12 clientes, por el CHOOSE de arriba): no cabe en una pagina de 10 y el total lo cuenta el servidor.
        var pagina = await admin.GetAsync("/api/v1/clientes?q=Hugo&tamano=10&pagina=2").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(10, pagina.GetProperty("elementos").GetArrayLength());
        Assert.True(pagina.GetProperty("total").GetInt32() > 100);
        Assert.All(pagina.GetProperty("elementos").EnumerateArray(), c => Assert.Equal("Hugo", c.GetProperty("nombre").GetString()));
        return true;
    });

    [Fact]
    public Task RecorrerTodasLasPaginas_ListaCadaClienteUnaSolaVez() => ConClientesSinteticosAsync(async admin =>
    {
        var vistos = new HashSet<int>();
        int total;
        var pagina = 1;
        do
        {
            var resultado = await admin.GetAsync($"/api/v1/clientes?tamano=100&pagina={pagina}").EsperarAsync(HttpStatusCode.OK);
            total = resultado.GetProperty("total").GetInt32();
            foreach (var cliente in resultado.GetProperty("elementos").EnumerateArray())
                Assert.True(vistos.Add(cliente.GetProperty("idCliente").GetInt32()), "Un cliente aparecio en dos paginas.");
            pagina++;
        }
        while (vistos.Count < total && pagina <= (total / 100) + 2);

        Assert.Equal(total, vistos.Count);
        Assert.True(total >= CantidadDeClientes);
        return true;
    });

    [Theory]
    [InlineData("987654321")]
    [InlineData("9 8765 4321")]
    [InlineData("+56 9 8765 4321")]
    [InlineData("+56987654321")]
    public Task LaBusqueda_EncuentraUnTelefonoEscritoConONoConElPrefijoDeChile(string escrito) => ConClientesSinteticosAsync(async admin =>
    {
        var resultado = await admin.GetAsync($"/api/v1/clientes?q={Uri.EscapeDataString(escrito)}&tamano=10").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, resultado.GetProperty("total").GetInt32());
        Assert.Equal(ApellidoConDatosValidos, resultado.GetProperty("elementos")[0].GetProperty("apellido").GetString());
        return true;
    });

    [Theory]
    [InlineData("12.345.678-5")]
    [InlineData("12345678-5")]
    [InlineData("123456785")]
    public Task LaBusqueda_EncuentraUnRutEscritoConPuntosConGuionOJunto(string escrito) => ConClientesSinteticosAsync(async admin =>
    {
        var resultado = await admin.GetAsync($"/api/v1/clientes?q={Uri.EscapeDataString(escrito)}&tamano=10").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, resultado.GetProperty("total").GetInt32());
        Assert.Equal(ApellidoConDatosValidos, resultado.GetProperty("elementos")[0].GetProperty("apellido").GetString());
        return true;
    });

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("[a-z]")]
    [InlineData("Ana%")]
    [InlineData(".")]
    [InlineData("...")]
    public Task LaBusqueda_TrataLosComodinesDeSqlComoTexto(string texto) => ConClientesSinteticosAsync(async admin =>
    {
        // Quien escribe «%» en el filtro no debe ver a todos los clientes: es un caracter mas.
        var resultado = await admin.GetAsync($"/api/v1/clientes?q={Uri.EscapeDataString(texto)}&tamano=10").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, resultado.GetProperty("total").GetInt32());
        return true;
    });

    [Fact]
    public Task LaBusqueda_ConMilQuinientosClientes_RespondeEnMenosDeDosSegundos() => ConClientesSinteticosAsync(async admin =>
    {
        // Mediana de varias llamadas con la primera descartada (calienta la conexion y el plan): es solo una red contra algo
        // catastrofico, no una medicion: quien de verdad vigila que no se traiga todo a memoria son los asserts de tope (100) y de total de arriba.
        // La medicion es scripts/carga. GENERATE_SERIES (arriba) requiere SQL Server 2022 o superior; el proyecto exige 2025.
        _ = await admin.GetAsync("/api/v1/clientes?q=Gonz&tamano=10").EsperarAsync(HttpStatusCode.OK);
        var tiempos = new List<double>();
        foreach (var q in new[] { "Gonz", "Mari", "8000", "Zzsint", "Rodr" })
        {
            var reloj = Stopwatch.StartNew();
            await admin.GetAsync($"/api/v1/clientes?q={q}&tamano=10").EsperarAsync(HttpStatusCode.OK);
            tiempos.Add(reloj.Elapsed.TotalMilliseconds);
        }
        tiempos.Sort();
        Assert.True(tiempos[tiempos.Count / 2] < 2000, $"La mediana de la busqueda fue de {tiempos[tiempos.Count / 2]:F0} ms (tiempos: {string.Join(", ", tiempos.Select(t => t.ToString("F0")))}).");
        return true;
    });
}
