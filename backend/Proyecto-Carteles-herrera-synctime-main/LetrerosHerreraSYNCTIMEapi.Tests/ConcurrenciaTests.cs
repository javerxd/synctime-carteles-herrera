using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Dos personas editan lo mismo: la segunda debe enterarse en vez de borrar el trabajo de la primera.
[Collection(ApiCollection.Name)]
public sealed class ConcurrenciaTests(ApiFixture api)
{
    private static string Version(JsonElement registro) => registro.GetProperty("rowVersion").GetString()!;

    [Fact]
    public async Task Solicitud_Put_WithStaleVersion_ReturnsConflictAndKeepsTheFirstEdit()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var primera = await cliente.PutAsJsonAsync(url, new { descripcionGeneral = "Primera", rowVersion = Version(abierta) }).EsperarAsync(HttpStatusCode.OK);
        var rechazo = await cliente.PutAsJsonAsync(url, new { descripcionGeneral = "Segunda", rowVersion = Version(abierta) }).EsperarAsync(HttpStatusCode.Conflict);

        Assert.NotEqual(Version(abierta), Version(primera));
        Assert.False(string.IsNullOrEmpty(rechazo.GetProperty("error").GetString()));
        var actual = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Primera", actual.GetProperty("descripcionGeneral").GetString());

        // Con la version nueva, que devuelve la propia respuesta, ya puede guardar.
        var reintento = await cliente.PutAsJsonAsync(url, new { descripcionGeneral = "Segunda", rowVersion = Version(primera) }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Segunda", reintento.GetProperty("descripcionGeneral").GetString());
    }

    [Fact]
    public async Task Solicitud_Estado_WithStaleVersion_ReturnsConflict()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "aceptado", rowVersion = Version(abierta) }).EsperarAsync(HttpStatusCode.OK);
        await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "rechazado", motivoRechazo = "x", rowVersion = Version(abierta) }).EsperarAsync(HttpStatusCode.Conflict);

        var actual = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("aceptado", actual.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Pedido_Put_WithStaleVersion_ReturnsConflict()
    {
        var (cliente, url, creado) = await CrearPedidoAsync();
        object Cuerpo(string estado, string rowVersion) => new { estado, fechaEntregaEstimada = DateTime.Now.AddDays(5), modalidadEntrega = "retiro_local", rowVersion };

        await cliente.PutAsJsonAsync(url, Cuerpo("en_proceso", Version(creado))).EsperarAsync(HttpStatusCode.OK);
        await cliente.PutAsJsonAsync(url, Cuerpo("cancelado", Version(creado))).EsperarAsync(HttpStatusCode.Conflict);

        var actual = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("en_proceso", actual.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Writes_WithoutVersion_ReturnBadRequest()
    {
        var (cliente, urlPedido, _) = await CrearPedidoAsync();
        var urlSolicitud = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";

        await cliente.PutAsJsonAsync(urlSolicitud, new { descripcionGeneral = "x" }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync(urlSolicitud, new { descripcionGeneral = "x", rowVersion = "" }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync(urlSolicitud, new { descripcionGeneral = "x", rowVersion = "no-es-base64!" }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PatchAsJsonAsync($"{urlSolicitud}/estado", new { estado = "aceptado" }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync(urlPedido, new { estado = "en_proceso", fechaEntregaEstimada = DateTime.Now.AddDays(5), modalidadEntrega = "retiro_local" }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    // Otra persona guarda justo despues de la comprobacion de version: aqui la frena la base, no Verificar.
    // El interceptor fuerza ese momento exacto, asi que este test recorre siempre ese camino.
    [Fact]
    public async Task Solicitud_ChangedBetweenCheckAndSave_ReturnsConflict()
    {
        var token = await api.TokenAdminAsync();
        var id = await api.CrearSolicitudAsync();
        var url = $"/api/v1/solicitudes/{id}";
        var abierta = await api.Cliente(token).GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var rechazo = await api.ClienteConInterceptor(new OtraPersonaGuardaAntes(id), token)
            .PutAsJsonAsync(url, new { descripcionGeneral = "Mia", rowVersion = Version(abierta) })
            .EsperarAsync(HttpStatusCode.Conflict);

        Assert.False(string.IsNullOrEmpty(rechazo.GetProperty("error").GetString()));
        var actual = await api.Cliente(token).GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Otra persona", actual.GetProperty("descripcionGeneral").GetString());
    }

    // Cualquiera sea el orden en que lleguen las escrituras, nunca se pisan entre si.
    [Fact]
    public async Task Solicitud_ConcurrentWrites_OnlyOneWins()
    {
        var token = await api.TokenAdminAsync();
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await api.Cliente(token).GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            api.Cliente(token).PutAsJsonAsync(url, new { descripcionGeneral = $"Edicion {i}", rowVersion = Version(abierta) })));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(7, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        foreach (var conflicto in respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            var cuerpo = await conflicto.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrEmpty(cuerpo.GetProperty("error").GetString()));
        }

        var ganadora = await respuestas.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<JsonElement>();
        var id = ganadora.GetProperty("idSolicitud").GetInt32();
        var enBase = await api.ConsultarAsync(db => db.Solicitudes.AsNoTracking().Where(s => s.IdSolicitud == id).Select(s => s.DescripcionGeneral).SingleAsync());
        Assert.Equal(ganadora.GetProperty("descripcionGeneral").GetString(), enBase);
    }

    private async Task<(HttpClient Cliente, string Url, JsonElement Creado)> CrearPedidoAsync()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var idSolicitud = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(idSolicitud);
        var creado = await cliente.PostAsJsonAsync("/api/v1/pedidos/", new { idSolicitud, idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = DateTime.Now.AddDays(5) })
            .EsperarAsync(HttpStatusCode.Created);
        return (cliente, $"/api/v1/pedidos/{creado.GetProperty("idPedido").GetInt32()}", creado);
    }

    private sealed class OtraPersonaGuardaAntes(int idSolicitud) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await eventData.Context!.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE solicitudes SET descripcion_general = N'Otra persona' WHERE id_solicitud = {idSolicitud}", cancellationToken);
            return result;
        }
    }
}
