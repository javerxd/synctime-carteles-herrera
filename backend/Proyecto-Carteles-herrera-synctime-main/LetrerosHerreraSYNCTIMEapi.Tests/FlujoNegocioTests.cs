using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Recorre con escrituras reales el flujo Solicitud -> Cotizacion -> Pedido.
[Collection(ApiCollection.Name)]
public sealed class FlujoNegocioTests(ApiFixture api)
{
    private static object FechaEntrega => DateTime.Now.AddDays(5);

    // ── Cotizaciones y trigger ──

    [Fact]
    public async Task Cotizacion_WithMaterials_ReturnsTotalsFromTrigger()
    {
        var cotizacion = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());

        Assert.Equal(1000m, cotizacion.GetProperty("costoMaterialesTotal").GetDecimal());
        Assert.Equal(2000m, cotizacion.GetProperty("totalCotizacion").GetDecimal());
        Assert.Equal(await api.IdAdminAsync(), cotizacion.GetProperty("idEmpleado").GetInt32());
    }

    [Fact]
    public async Task Cotizacion_Put_ReplacesKeepsOrClearsMaterials()
    {
        var token = await api.TokenAdminAsync();
        var cotizacion = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());
        var url = $"/api/v1/cotizaciones/{cotizacion.GetProperty("idCotizacion").GetInt32()}";

        var reemplazada = await api.Cliente(token).PutAsJsonAsync(url, new
        {
            costoManoObra = 1000m, otrosCostos = 0m, estado = "pendiente",
            materiales = new[] { new { idMaterial = 2, cantidad = 3m, costoUnitario = 100m } }
        }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1300m, reemplazada.GetProperty("totalCotizacion").GetDecimal());

        var sinLista = await api.Cliente(token).PutAsJsonAsync(url, new { costoManoObra = 2000m, otrosCostos = 0m, estado = "pendiente" })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(2300m, sinLista.GetProperty("totalCotizacion").GetDecimal());

        var vacia = await api.Cliente(token).PutAsJsonAsync(url, new { costoManoObra = 2000m, otrosCostos = 0m, estado = "pendiente", materiales = Array.Empty<object>() })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(2000m, vacia.GetProperty("totalCotizacion").GetDecimal());
    }

    [Fact]
    public async Task Cotizacion_Delete_RemovesItsMaterials()
    {
        var token = await api.TokenAdminAsync();
        var id = (await api.CrearCotizacionAsync(await api.CrearSolicitudAsync())).GetProperty("idCotizacion").GetInt32();

        await api.Cliente(token).DeleteAsync($"/api/v1/cotizaciones/{id}").EsperarAsync(HttpStatusCode.NoContent);

        Assert.False(await api.ConsultarAsync(db => db.CotizacionMateriales.AnyAsync(m => m.IdCotizacion == id)));
    }

    [Fact]
    public async Task Cotizacion_InvalidData_ReturnsBadRequest()
    {
        var token = await api.TokenAdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        var id = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();

        await api.Cliente(token).PutAsJsonAsync($"/api/v1/cotizaciones/{id}", new { costoManoObra = 1m, otrosCostos = 0m, estado = "inventado" })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/cotizaciones/", new { idSolicitud, costoManoObra = -1m, otrosCostos = 0m })
            .EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/cotizaciones/", new { idSolicitud, costoManoObra = 1m, otrosCostos = 0m, materiales = new[] { new { idMaterial = 999999, cantidad = 1m, costoUnitario = 1m } } })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Pedidos ──

    [Fact]
    public async Task Pedido_FromApprovedQuote_TakesItsTotal()
    {
        var token = await api.TokenAdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);
        await api.AceptarSolicitudAsync(idSolicitud);

        var pedido = await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", new
        {
            idSolicitud, idCotizacion,
            idEmpleadoResponsable = await api.IdAdminAsync(),
            fechaEntregaEstimada = FechaEntrega,
            modalidadEntrega = "instalacion_terreno",
            requiereInstalacion = true,
            direccionInstalacion = "Calle 1",
            costoInstalacionAdicional = 500m
        }).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal(2000m, pedido.GetProperty("montoBaseTrabajo").GetDecimal());
        Assert.Equal(2500m, pedido.GetProperty("montoTotalFinal").GetDecimal());
        Assert.True(pedido.GetProperty("requiereInstalacion").GetBoolean());

        var leido = await api.Cliente(token).GetAsync($"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}").EsperarAsync(HttpStatusCode.OK);
        Assert.True(leido.GetProperty("requiereInstalacion").GetBoolean());
    }

    [Fact]
    public async Task Pedido_EnforcesWorkflowRules()
    {
        var token = await api.TokenAdminAsync();
        var idAdmin = await api.IdAdminAsync();
        object Cuerpo(int idSolicitud, int? idCotizacion) => new { idSolicitud, idCotizacion, idEmpleadoResponsable = idAdmin, fechaEntregaEstimada = FechaEntrega };

        var sinAceptar = await api.CrearSolicitudAsync();
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", Cuerpo(sinAceptar, null)).EsperarAsync(HttpStatusCode.BadRequest);

        var aceptada = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(aceptada);

        var pendiente = (await api.CrearCotizacionAsync(aceptada)).GetProperty("idCotizacion").GetInt32();
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", Cuerpo(aceptada, pendiente)).EsperarAsync(HttpStatusCode.BadRequest);

        var ajena = (await api.CrearCotizacionAsync(sinAceptar)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(ajena);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", Cuerpo(aceptada, ajena)).EsperarAsync(HttpStatusCode.BadRequest);

        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", Cuerpo(aceptada, null)).EsperarAsync(HttpStatusCode.Created);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", Cuerpo(aceptada, null)).EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Pedido_Put_RejectsInvalidValues()
    {
        var token = await api.TokenAdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(idSolicitud);
        var pedido = await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", new { idSolicitud, idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = FechaEntrega })
            .EsperarAsync(HttpStatusCode.Created);
        var url = $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}";

        await api.Cliente(token).PutAsJsonAsync(url, new { estado = "inventado", fechaEntregaEstimada = FechaEntrega, modalidadEntrega = "retiro_local" }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PutAsJsonAsync(url, new { estado = "en_proceso", fechaEntregaEstimada = FechaEntrega, modalidadEntrega = "inventada" }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PutAsJsonAsync(url, new { estado = "en_proceso", fechaEntregaEstimada = FechaEntrega, modalidadEntrega = "retiro_local", rowVersion = pedido.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Solicitudes y eliminacion logica ──

    [Fact]
    public async Task Solicitud_ByEmployee_IsPresencialEvenIfBodySaysOtherwise()
    {
        var creada = await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/solicitudes/", new
        {
            idCliente = await api.CrearClienteAsync(),
            idEmpleadoRegistra = (int?)null,
            descripcionGeneral = "Encargo en el local"
        }).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal("presencial", creada.GetProperty("origen").GetString());
        Assert.Equal(await api.IdAdminAsync(), creada.GetProperty("idEmpleadoRegistra").GetInt32());
    }

    [Fact]
    public async Task Solicitud_InvalidBodies_ReturnBadRequest()
    {
        var token = await api.TokenAdminAsync();
        var idCliente = await api.CrearClienteAsync();
        object[] cuerpos =
        [
            // Lo que todavia envia el formulario del frontend: antes daba 201 y perdia todos los datos.
            new { idCliente, idProducto = 1, cantidad = 3, telefonoContacto = "+56912345678", tamano = "2m x 1m", materialSolicitado = "Acrilico", notasWhatsapp = "nota" },
            new { idCliente, detalles = new[] { new { idProducto = 1, descripcion = "d", cantidad = 0 } } },
            new { idCliente, detalles = new[] { new { idProducto = 999999, descripcion = "d", cantidad = 1 } } },
            new { idCliente, detalles = new[] { new { idProducto = 1, descripcion = "d", cantidad = 1, anchoMm = 100 } } },
            new { idCliente, detalles = new[] { new { idProducto = 1, descripcion = " ", cantidad = 1 } } },
            new { idCliente = 999999, descripcionGeneral = "x" }
        ];

        foreach (var cuerpo in cuerpos)
            await api.Cliente(token).PostAsJsonAsync("/api/v1/solicitudes/", cuerpo).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Solicitud_WithoutQuantity_DefaultsToOne()
    {
        var creada = await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/solicitudes/", new
        {
            idCliente = await api.CrearClienteAsync(),
            detalles = new[] { new { idProducto = 1, descripcion = "d" } }
        }).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal(1, creada.GetProperty("detalles")[0].GetProperty("cantidad").GetInt32());
    }

    [Fact]
    public async Task Solicitud_Estado_RejectsInvalidState()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var rechazo = await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "inventado", rowVersion = abierta.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Contains("Estado", rechazo.GetProperty("error").GetString());
    }

    // El PATCH de estado esta reservado a gestion: el PUT, abierto a todo el personal, no puede saltarse esa regla.
    [Fact]
    public async Task Solicitud_Put_ByOperario_CannotChangeTheState()
    {
        var operario = api.Cliente(await api.TokenEmpleadoAsync("operario"));
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await operario.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var editada = await operario.PutAsJsonAsync(url, new { descripcionGeneral = "Editada", estado = "aceptado", motivoRechazo = "x", rowVersion = abierta.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("Editada", editada.GetProperty("descripcionGeneral").GetString());
        Assert.Equal("solicitado", editada.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, editada.GetProperty("motivoRechazo").ValueKind);
    }

    [Fact]
    public async Task Solicitud_Put_RequiresADescriptionIfItHasNoDetails()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var creada = await cliente.PostAsJsonAsync("/api/v1/solicitudes/", new { idCliente = await api.CrearClienteAsync(), descripcionGeneral = "Solo texto" }).EsperarAsync(HttpStatusCode.Created);
        var url = $"/api/v1/solicitudes/{creada.GetProperty("idSolicitud").GetInt32()}";

        await cliente.PutAsJsonAsync(url, new { descripcionGeneral = " ", rowVersion = creada.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Solicitud_Estado_ClearsTheReasonWhenLeavingRejected()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var rechazada = await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "rechazado", motivoRechazo = "Sin stock", rowVersion = abierta.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Sin stock", rechazada.GetProperty("motivoRechazo").GetString());

        var aceptada = await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "aceptado", rowVersion = rechazada.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, aceptada.GetProperty("motivoRechazo").ValueKind);
    }

    [Fact]
    public async Task Solicitud_TooManyDetails_ReturnsBadRequest()
    {
        var detalles = Enumerable.Range(0, 51).Select(_ => new { idProducto = 1, descripcion = "d", cantidad = 1 }).ToArray();

        var rechazo = await api.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", new
        {
            cliente = new { nombre = "Ana", apellido = "Web", telefono = api.Telefono() },
            detalles
        }).EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Contains("50", rechazo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Solicitud_Delete_IsLogicalAndKeepsTheMetric()
    {
        var token = await api.TokenAdminAsync();
        var creada = await EnviarSolicitudOnlineAsync(api.Telefono());
        var id = creada.GetProperty("idSolicitud").GetInt32();
        var totalAntes = (await MetricaDelMesAsync(token)).Total;

        await api.Cliente(token).DeleteAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.NoContent);

        await api.Cliente(token).GetAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.NotFound);
        var lista = await api.Cliente(token).GetAsync("/api/v1/solicitudes/").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(lista.EnumerateArray(), s => s.GetProperty("idSolicitud").GetInt32() == id);

        var fila = await api.ConsultarAsync(db => db.Solicitudes.AsNoTracking().SingleAsync(s => s.IdSolicitud == id));
        Assert.True(fila.EliminadoLogico);
        Assert.NotNull(fila.FechaEliminacionLogica);
        Assert.Equal(totalAntes, (await MetricaDelMesAsync(token)).Total);
    }

    [Fact]
    public async Task Solicitud_WithPedido_CannotBeDeleted()
    {
        var token = await api.TokenAdminAsync();
        var id = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(id);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", new { idSolicitud = id, idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = FechaEntrega })
            .EsperarAsync(HttpStatusCode.Created);

        await api.Cliente(token).DeleteAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Solicitud_LogicallyDeleted_CannotBeQuoted()
    {
        var token = await api.TokenAdminAsync();
        var id = await api.CrearSolicitudAsync();
        await api.Cliente(token).PatchAsync($"/api/v1/solicitudes/{id}/eliminar-logico", null).EsperarAsync(HttpStatusCode.OK);

        await api.Cliente(token).PostAsJsonAsync("/api/v1/cotizaciones/", new { idSolicitud = id, costoManoObra = 1m, otrosCostos = 0m })
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Flujo online y metrica ──

    [Fact]
    public async Task OnlineRequest_ReturnsOnlyAReceipt()
    {
        var recibo = await EnviarSolicitudOnlineAsync(api.Telefono());

        Assert.Equal("online", recibo.GetProperty("origen").GetString());
        Assert.False(recibo.TryGetProperty("cliente", out _));
        Assert.False(recibo.TryGetProperty("idCliente", out _));
    }

    [Fact]
    public async Task OnlineRequest_WithExistingPhone_DoesNotOverwriteTheClient()
    {
        var telefono = api.Telefono();
        await EnviarSolicitudOnlineAsync(telefono, "Original");

        await EnviarSolicitudOnlineAsync(telefono, "Impostor");

        var cliente = await api.ConsultarAsync(db => db.Clientes.AsNoTracking().SingleAsync(c => c.Telefono == telefono));
        Assert.Equal("Original", cliente.Nombre);
    }

    // Registrar un telefono ajeno no debe servir para leer lo que su titular envie despues por el formulario.
    [Fact]
    public async Task OnlineRequest_WithThePhoneOfAnAccount_IsRejectedAndStaysOutOfIt()
    {
        var (_, telefono, tokenCuenta) = await api.RegistrarClienteAsync();

        await EnviarAsync(telefono).EsperarAsync(HttpStatusCode.Conflict);

        var deLaCuenta = await api.Cliente(tokenCuenta).GetAsync("/api/v1/mis/solicitudes").EsperarAsync(HttpStatusCode.OK);
        Assert.Empty(deLaCuenta.EnumerateArray());

        Task<HttpResponseMessage> EnviarAsync(string numero) =>
            api.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", new
            {
                cliente = new { nombre = "Titular", apellido = "Real", telefono = numero },
                descripcionGeneral = "Instalar en mi casa"
            });
    }

    [Fact]
    public async Task OnlineRequest_InvalidBody_ReturnsBadRequest()
    {
        await api.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", new { descripcionGeneral = "sin contacto" }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", new { cliente = new { nombre = "A", apellido = "B", telefono = api.Telefono() } }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Metric_CountsOnlineRequestsConvertedIntoOrders()
    {
        var token = await api.TokenAdminAsync();
        var antes = await MetricaDelMesAsync(token);
        var id = (await EnviarSolicitudOnlineAsync(api.Telefono())).GetProperty("idSolicitud").GetInt32();
        await api.AceptarSolicitudAsync(id);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", new { idSolicitud = id, idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = FechaEntrega })
            .EsperarAsync(HttpStatusCode.Created);

        var despues = await MetricaDelMesAsync(token);

        Assert.Equal(antes.Total + 1, despues.Total);
        Assert.Equal(antes.Concretadas + 1, despues.Concretadas);
    }

    // ── Rechazos de la base traducidos a 400/409 ──

    [Fact]
    public async Task Delete_WithDependents_ReturnsConflictAndKeepsData()
    {
        var token = await api.TokenAdminAsync();
        var n = api.Siguiente();
        var idCategoria = (await api.Cliente(token).PostAsJsonAsync("/api/v1/categorias/", new { nombre = $"Categoria {n}" }).EsperarAsync(HttpStatusCode.Created)).GetProperty("idCategoria").GetInt32();
        var idProducto = (await api.Cliente(token).PostAsJsonAsync("/api/v1/productos/", new { idCategoria, nombre = $"Producto {n}" }).EsperarAsync(HttpStatusCode.Created)).GetProperty("idProducto").GetInt32();
        var idCliente = await api.CrearClienteAsync();
        await api.CrearSolicitudAsync(idCliente, idProducto);

        await api.Cliente(token).DeleteAsync($"/api/v1/categorias/{idCategoria}").EsperarAsync(HttpStatusCode.Conflict);
        await api.Cliente(token).DeleteAsync($"/api/v1/productos/{idProducto}").EsperarAsync(HttpStatusCode.Conflict);
        await api.Cliente(token).DeleteAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.Conflict);

        await api.Cliente().GetAsync($"/api/v1/productos/{idProducto}").EsperarAsync(HttpStatusCode.OK);
        await api.Cliente(token).GetAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_QuoteUsedByOrder_ReturnsConflict()
    {
        var token = await api.TokenAdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);
        await api.AceptarSolicitudAsync(idSolicitud);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/pedidos/", new { idSolicitud, idCotizacion, idEmpleadoResponsable = await api.IdAdminAsync(), fechaEntregaEstimada = FechaEntrega })
            .EsperarAsync(HttpStatusCode.Created);

        await api.Cliente(token).DeleteAsync($"/api/v1/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cliente_DatabaseRejections_ReturnClientErrors()
    {
        var token = await api.TokenAdminAsync();
        var email = $"cliente{api.Siguiente()}@test.local";
        await api.Cliente(token).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "A", apellido = "B", telefono = api.Telefono(), email }).EsperarAsync(HttpStatusCode.Created);

        await api.Cliente(token).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "A", apellido = "B", telefono = api.Telefono(), email }).EsperarAsync(HttpStatusCode.Conflict);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/clientes/", new { nombre = "A", apellido = "B", telefono = api.Telefono(), email = "correo-invalido" }).EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).PostAsJsonAsync("/api/v1/clientes/", new { nombre = new string('x', 200), apellido = "B", telefono = api.Telefono() }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        using var contenido = new StringContent("{esto no es json", Encoding.UTF8, "application/json");

        await api.Cliente().PostAsync("/api/v1/auth/login", contenido).EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Valores false y catalogo oculto ──

    [Fact]
    public async Task Categoria_CreatedInactive_StaysInactive()
    {
        var token = await api.TokenAdminAsync();
        var creada = await api.Cliente(token).PostAsJsonAsync("/api/v1/categorias/", new { nombre = $"Inactiva {api.Siguiente()}", activo = false }).EsperarAsync(HttpStatusCode.Created);

        var leida = await api.Cliente(token).GetAsync($"/api/v1/categorias/{creada.GetProperty("idCategoria").GetInt32()}").EsperarAsync(HttpStatusCode.OK);

        Assert.False(leida.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Catalogo_HiddenItem_IsOnlyVisibleToStaff()
    {
        var token = await api.TokenAdminAsync();
        var titulo = $"Oculto {api.Siguiente()}";
        var creado = await api.Cliente(token).PostAsJsonAsync("/api/v1/catalogo/", new { idProducto = 1, titulo, descripcion = "d", imagenUrl = "u", visible = false }).EsperarAsync(HttpStatusCode.Created);
        var id = creado.GetProperty("idCatalogo").GetInt32();

        var vitrina = await api.Cliente().GetAsync("/api/v1/catalogo/").EsperarAsync(HttpStatusCode.OK);

        Assert.DoesNotContain(vitrina.EnumerateArray(), c => c.GetProperty("titulo").GetString() == titulo);
        await api.Cliente().GetAsync($"/api/v1/catalogo/{id}").EsperarAsync(HttpStatusCode.NotFound);
        await api.Cliente(token).GetAsync($"/api/v1/catalogo/{id}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(await api.IdAdminAsync(), creado.GetProperty("idEmpleadoCreador").GetInt32());
    }

    [Fact]
    public async Task CartelDigital_RecordsTheEmployeeFromTheToken()
    {
        var cartel = await api.Cliente(await api.TokenAdminAsync()).PutAsJsonAsync("/api/v1/cartel-digital", new
        {
            abiertoEnLocal = false,
            mensajeEstado = "Volvemos pronto",
            idEmpleadoModifica = 999999
        }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(await api.IdAdminAsync(), cartel.GetProperty("idEmpleadoModifica").GetInt32());
        Assert.False(cartel.GetProperty("abiertoEnLocal").GetBoolean());

        var publico = await api.Cliente().GetAsync("/api/v1/cartel-digital").EsperarAsync(HttpStatusCode.OK);
        Assert.False(publico.GetProperty("abiertoEnLocal").GetBoolean());
        Assert.False(publico.TryGetProperty("idEmpleadoModifica", out _));
    }

    private Task<JsonElement> EnviarSolicitudOnlineAsync(string telefono, string nombre = "Visitante") =>
        api.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", new
        {
            cliente = new { nombre, apellido = "Web", telefono },
            detalles = new[] { new { idProducto = 1, descripcion = "Letrero para mi local", cantidad = 1 } }
        }).EsperarAsync(HttpStatusCode.Created);

    private async Task<(int Total, int Concretadas)> MetricaDelMesAsync(string token)
    {
        var filas = await api.Cliente(token).GetAsync("/api/v1/metricas/conversion-online").EsperarAsync(HttpStatusCode.OK);
        var hoy = DateTime.Now;
        foreach (var fila in filas.EnumerateArray())
        {
            if (fila.GetProperty("anio").GetInt32() == hoy.Year && fila.GetProperty("mes").GetInt32() == hoy.Month)
                return (fila.GetProperty("totalSolicitudesOnline").GetInt32(), fila.GetProperty("solicitudesConcretadasEnPedido").GetInt32());
        }
        return (0, 0);
    }
}
