using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 11, seccion 7: Idempotency-Key en las seis rutas que crean, y version (If-Match) en el horario y el cartel.
[Collection(ApiCollection.Name)]
public sealed class IdempotenciaYVersionFase11Tests(ApiFixture api)
{
    private static object Presencial(int idCliente, string descripcion) => new
    {
        idCliente,
        descripcionGeneral = descripcion,
        detalles = new[] { new { idProducto = 1, descripcion = "Letrero", anchoMm = 1000, altoMm = 500, cantidad = 1 } }
    };

    private Task<int> SolicitudesConAsync(string descripcion) =>
        api.ConsultarAsync(db => db.Solicitudes.CountAsync(s => s.DescripcionGeneral == descripcion));

    [Fact]
    public async Task LaMismaClaveYElMismoCuerpo_DevuelvenLaMismaRespuesta_SinCrearOtro()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var idCliente = await api.CrearClienteAsync();
        var descripcion = $"Idempotente {Guid.NewGuid():N}";
        var clave = Guid.NewGuid().ToString();

        using var primera = await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion)));
        using var segunda = await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion)));

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        Assert.False(primera.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal("true", segunda.Headers.GetValues("Idempotent-Replayed").Single());
        var a = JsonDocument.Parse(await primera.Content.ReadAsStringAsync()).RootElement;
        var b = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(a.GetProperty("idSolicitud").GetInt32(), b.GetProperty("idSolicitud").GetInt32());
        Assert.Equal(1, await SolicitudesConAsync(descripcion));

        // Otro cuerpo con la misma clave: 409 clave_reutilizada, sin crear nada.
        var otro = await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion + " distinto")))
            .EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("clave_reutilizada", Fase11Apoyo.Codigo(otro));
        Assert.Equal(0, await SolicitudesConAsync(descripcion + " distinto"));
    }

    [Fact]
    public async Task DosEnviosEnParalelo_ConLaMismaClave_CreanUnSoloRegistro()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var idCliente = await api.CrearClienteAsync();
        var descripcion = $"Paralelo {Guid.NewGuid():N}";
        var clave = Guid.NewGuid().ToString();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion)))));

        Assert.Equal(1, await SolicitudesConAsync(descripcion));
        Assert.All(respuestas, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict, $"Llegó {(int)r.StatusCode}"));
        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var conflicto in respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal("en_proceso", Fase11Apoyo.Codigo(JsonDocument.Parse(await conflicto.Content.ReadAsStringAsync()).RootElement));
    }

    [Fact]
    public async Task SinLaCabecera_SeComportaComoAntes_YUnaClaveInvalidaEs400()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var idCliente = await api.CrearClienteAsync();
        var descripcion = $"Sin clave {Guid.NewGuid():N}";
        await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", null, Presencial(idCliente, descripcion))).EsperarAsync(HttpStatusCode.Created);
        await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", null, Presencial(idCliente, descripcion))).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal(2, await SolicitudesConAsync(descripcion));

        var invalida = await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", "no-es-un-uuid", Presencial(idCliente, descripcion)))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("clave_invalida", Fase11Apoyo.Codigo(invalida));
        Assert.Equal(2, await SolicitudesConAsync(descripcion));
    }

    // Las seis rutas de creacion leen la cabecera: con una clave invalida responden 400 clave_invalida antes de crear nada.
    [Theory]
    [InlineData("/api/v1/solicitudes", false)]
    [InlineData("/api/v1/mis/solicitudes", true)]
    [InlineData("/api/v1/solicitudes/1/cotizaciones", false)]
    [InlineData("/api/v1/pedidos/directo", false)]
    [InlineData("/api/v1/cotizaciones/1/pedido", false)]
    [InlineData("/api/v1/pedidos/1/pagos", false)]
    public async Task LasSeisRutasDeCreacion_AceptanIdempotencyKey(string ruta, bool delCliente)
    {
        var token = delCliente ? (await api.CrearClienteConfirmadoAsync()).Token : await api.TokenAdminAsync();
        var invalida = await api.Cliente(token).SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, ruta, "x", new { })).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("clave_invalida", Fase11Apoyo.Codigo(invalida));
    }

    [Fact]
    public async Task UnAbonoReintentadoConLaMismaClave_SeRegistraUnaVez()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = (await api.CrearPedidoAsync(90_000)).GetProperty("idPedido").GetInt32();
        var clave = Guid.NewGuid().ToString();
        var cuerpo = new { monto = 12_345, medioPago = "transferencia" };

        await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, $"/api/v1/pedidos/{id}/pagos", clave, cuerpo)).EsperarAsync(HttpStatusCode.Created);
        await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, $"/api/v1/pedidos/{id}/pagos", clave, cuerpo)).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal(1, await api.ConsultarAsync(db => db.Pagos.CountAsync(p => p.IdPedido == id && p.Monto == 12_345)));
        // La misma clave en otro pedido es otro cuerpo: no se reutiliza.
        var otro = (await api.CrearPedidoAsync(90_000)).GetProperty("idPedido").GetInt32();
        var reutilizada = await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, $"/api/v1/pedidos/{otro}/pagos", clave, cuerpo)).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("clave_reutilizada", Fase11Apoyo.Codigo(reutilizada));
    }

    [Fact]
    public async Task UnaRespuestaDeError_NoSeGuarda_YLiberaLaClave()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var idCliente = await api.CrearClienteAsync();
        var clave = Guid.NewGuid().ToString();
        var descripcion = $"Reintento {Guid.NewGuid():N}";
        var invalido = new { idCliente, descripcionGeneral = "corta", detalles = Array.Empty<object>() };
        await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, invalido)).EsperarAsync(HttpStatusCode.BadRequest);

        // Con la misma clave y el formulario corregido, se crea (la clave no quedo tomada por el 400).
        await admin.SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion))).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal(1, await SolicitudesConAsync(descripcion));
    }

    [Fact]
    public async Task LaClave_EsDeCadaUsuario()
    {
        var idCliente = await api.CrearClienteAsync();
        var clave = Guid.NewGuid().ToString();
        var descripcion = $"Por usuario {Guid.NewGuid():N}";
        await api.Cliente(await api.TokenAdminAsync()).SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion)))
            .EsperarAsync(HttpStatusCode.Created);
        using var otro = await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario))
            .SendAsync(Fase11Apoyo.ConClave(HttpMethod.Post, "/api/v1/solicitudes", clave, Presencial(idCliente, descripcion)));

        Assert.Equal(HttpStatusCode.Created, otro.StatusCode);
        Assert.False(otro.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(2, await SolicitudesConAsync(descripcion));
    }

    // ── Version del horario y del cartel ──

    private static object Semana(string cierreLunes) => new
    {
        dias = Enumerable.Range(1, 7).Select(d => d == 7
            ? new { diaSemana = d, abierto = false, horaInicio = (string?)null, horaFin = (string?)null }
            : new { diaSemana = d, abierto = true, horaInicio = (string?)"09:00", horaFin = (string?)(d == 1 ? cierreLunes : d == 6 ? "14:00" : "18:30") })
    };

    [Fact]
    public async Task ElHorario_ExigeLaVersionVista()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var etag = await Fase11Apoyo.EtagAsync(admin, "/api/v1/horarios");
        var publico = await api.Cliente().GetAsync("/api/v1/horarios").EsperarAsync(HttpStatusCode.OK);
        Assert.All(publico.EnumerateArray(), d => Assert.Equal(etag.Trim('"'), d.GetProperty("rowVersion").GetString()));

        var sinVersion = await admin.PutAsJsonAsync("/api/v1/horarios", Semana("18:30")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("sin_version", Fase11Apoyo.Codigo(sinVersion));

        // Dos personas guardan con la misma version: la segunda recibe 409.
        HttpRequestMessage Con(string cierre)
        {
            var peticion = new HttpRequestMessage(HttpMethod.Put, "/api/v1/horarios") { Content = JsonContent.Create(Semana(cierre)) };
            peticion.Headers.TryAddWithoutValidation("If-Match", etag);
            return peticion;
        }
        using var primera = await admin.SendAsync(Con("18:00"));
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.NotEqual(etag, primera.Headers.ETag?.ToString());
        var segunda = await admin.SendAsync(Con("19:00")).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", Fase11Apoyo.Codigo(segunda));
        Assert.Equal(new TimeOnly(18, 0), await api.ConsultarAsync(db => db.HorariosAtencion.Where(h => h.DiaSemana == 1).Select(h => h.HoraFin).SingleAsync()));

        await admin.GuardarHorarioAsync(Semana("18:30"));
    }

    [Fact]
    public async Task ElCartel_ExigeLaVersionVista()
    {
        var personal = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        var etag = await Fase11Apoyo.EtagAsync(personal, "/api/v1/cartel-digital");
        var publico = await api.Cliente().GetAsync("/api/v1/cartel-digital").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(etag.Trim('"'), publico.GetProperty("rowVersion").GetString());

        var sinVersion = await personal.PutAsJsonAsync("/api/v1/cartel-digital", new { abiertoEnLocal = false, mensajeEstado = "Salimos" }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal("sin_version", Fase11Apoyo.Codigo(sinVersion));

        HttpRequestMessage Con(bool abierto)
        {
            var peticion = new HttpRequestMessage(HttpMethod.Put, "/api/v1/cartel-digital") { Content = JsonContent.Create(new { abiertoEnLocal = abierto, mensajeEstado = "Cambio" }) };
            peticion.Headers.TryAddWithoutValidation("If-Match", etag);
            return peticion;
        }
        await personal.SendAsync(Con(false)).EsperarAsync(HttpStatusCode.OK);
        var obsoleta = await personal.SendAsync(Con(true)).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", Fase11Apoyo.Codigo(obsoleta));

        await personal.GuardarCartelAsync(new { abiertoEnLocal = true, mensajeEstado = "Local abierto y atendiendo" });
    }
}
