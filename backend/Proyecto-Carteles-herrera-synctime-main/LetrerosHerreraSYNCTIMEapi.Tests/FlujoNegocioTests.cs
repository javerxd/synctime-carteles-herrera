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
    private static object FechaEntrega => ApiFixture.HoyChile().AddDays(5);

    // ── Cotizaciones y trigger ──

    [Fact]
    public async Task Cotizacion_WithMaterials_ReturnsTotalsFromTrigger()
    {
        var cotizacion = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());

        Assert.Equal(1000m, cotizacion.GetProperty("costoMaterialesTotal").GetDecimal());
        Assert.Equal(2000m, cotizacion.GetProperty("total").GetDecimal());
        Assert.Equal(2000, cotizacion.GetProperty("totalRedondeado").GetInt32());
        Assert.Equal(await api.IdAdminAsync(), cotizacion.GetProperty("elaboradaPor").GetProperty("idEmpleado").GetInt32());
    }

    // Fase 8: editar reemplaza las lineas (una lista vacia o ausente las quita) y exige la version vista.
    [Fact]
    public async Task Cotizacion_Put_ReplacesOrClearsMaterialsWithTheSeenVersion()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var cotizacion = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());
        var url = $"/api/v1/cotizaciones/{cotizacion.GetProperty("idCotizacion").GetInt32()}";

        var reemplazada = await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, cotizacion.GetProperty("rowVersion").GetString()!, new
        {
            costoManoObra = 1000m, otrosCostos = 0m,
            materiales = new[] { new { idMaterial = 2, cantidad = 3m, costoUnitario = 100m } }
        })).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1300m, reemplazada.GetProperty("total").GetDecimal());
        Assert.Equal("pendiente", reemplazada.GetProperty("estado").GetString());

        var vacia = await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, reemplazada.GetProperty("rowVersion").GetString()!,
            new { costoManoObra = 2000m, otrosCostos = 0m, materiales = Array.Empty<object>() })).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(2000m, vacia.GetProperty("total").GetDecimal());
        Assert.Empty(vacia.GetProperty("materiales").EnumerateArray());

        // Con la version de antes: otra persona ya la cambio.
        await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, reemplazada.GetProperty("rowVersion").GetString()!,
            new { costoManoObra = 3000m, otrosCostos = 0m })).EsperarAsync(HttpStatusCode.Conflict);
    }

    // Fase 8: el borrado es logico; la cotizacion y sus lineas quedan en la base.
    [Fact]
    public async Task Cotizacion_Delete_IsLogical()
    {
        var token = await api.TokenAdminAsync();
        var creada = await api.CrearCotizacionAsync(await api.CrearSolicitudAsync());
        var id = creada.GetProperty("idCotizacion").GetInt32();

        await api.Cliente(token).DeleteAsync($"/api/v1/cotizaciones/{id}").EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).SendAsync(ApiFixture.ConVersion(HttpMethod.Delete, $"/api/v1/cotizaciones/{id}", creada.GetProperty("rowVersion").GetString()!))
            .EsperarAsync(HttpStatusCode.NoContent);

        await api.Cliente(token).GetAsync($"/api/v1/cotizaciones/{id}").EsperarAsync(HttpStatusCode.NotFound);
        Assert.True(await api.ConsultarAsync(db => db.Cotizaciones.Where(c => c.IdCotizacion == id).Select(c => c.EliminadoLogico).SingleAsync()));
        Assert.True(await api.ConsultarAsync(db => db.CotizacionMateriales.AnyAsync(m => m.IdCotizacion == id)));
    }

    [Fact]
    public async Task Cotizacion_InvalidData_ReturnsBadRequest()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var idSolicitud = await api.CrearSolicitudAsync();
        var url = $"/api/v1/solicitudes/{idSolicitud}/cotizaciones";

        await cliente.PostAsJsonAsync(url, new { costoManoObra = -1m, otrosCostos = 0m }).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PostAsJsonAsync(url, new { costoManoObra = 1m, otrosCostos = 0m, materiales = new[] { new { idMaterial = 999999, cantidad = 1m, costoUnitario = 1m } } })
            .EsperarAsync(HttpStatusCode.BadRequest);
        var decimales = await cliente.PostAsJsonAsync(url, new { costoManoObra = 1000.5m, otrosCostos = 0m }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(decimales.GetProperty("errores").TryGetProperty("costoManoObra", out _));

        var creada = await api.CrearCotizacionAsync(idSolicitud);
        await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, $"/api/v1/cotizaciones/{creada.GetProperty("idCotizacion").GetInt32()}",
            creada.GetProperty("rowVersion").GetString()!, new { costoManoObra = 0m, otrosCostos = 0m })).EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Pedidos ──

    [Fact]
    public async Task Pedido_FromApprovedQuote_TakesItsTotal()
    {
        var token = await api.TokenAdminAsync();
        var idSolicitud = await api.CrearSolicitudAsync();
        var idCotizacion = (await api.CrearCotizacionAsync(idSolicitud)).GetProperty("idCotizacion").GetInt32();
        await api.AprobarCotizacionAsync(idCotizacion);

        var pedido = await api.Cliente(token).PostAsJsonAsync($"/api/v1/cotizaciones/{idCotizacion}/pedido", new
        {
            idEmpleadoResponsable = await api.IdAdminAsync(),
            fechaEntregaEstimada = FechaEntrega,
            modalidadEntrega = "instalacion_terreno",
            requiereInstalacion = true,
            direccionInstalacion = "Calle 1",
            costoInstalacionAdicional = 500m
        }).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal(2000, pedido.GetProperty("montoBaseTrabajo").GetInt32());
        Assert.Equal(2500, pedido.GetProperty("montoTotalFinal").GetInt32());
        Assert.True(pedido.GetProperty("requiereInstalacion").GetBoolean());
        Assert.Equal(idSolicitud, pedido.GetProperty("solicitud").GetProperty("idSolicitud").GetInt32());
        Assert.Equal(idCotizacion, pedido.GetProperty("cotizacion").GetProperty("idCotizacion").GetInt32());

        var leido = await api.Cliente(token).GetAsync($"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}").EsperarAsync(HttpStatusCode.OK);
        Assert.True(leido.GetProperty("requiereInstalacion").GetBoolean());
        Assert.Equal("Calle 1", leido.GetProperty("direccionInstalacion").GetString());
    }

    [Fact]
    public async Task Pedido_EnforcesWorkflowRules()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var datos = ApiFixture.DatosPedido(await api.IdAdminAsync());

        // Solo una cotizacion aprobada da un pedido, y uno solo.
        var pendiente = (await api.CrearCotizacionAsync(await api.CrearSolicitudAsync())).GetProperty("idCotizacion").GetInt32();
        var rechazo = await cliente.PostAsJsonAsync($"/api/v1/cotizaciones/{pendiente}/pedido", datos).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("transicion_invalida", rechazo.GetProperty("codigo").GetString());

        await api.AprobarCotizacionAsync(pendiente);
        await cliente.PostAsJsonAsync($"/api/v1/cotizaciones/{pendiente}/pedido", datos).EsperarAsync(HttpStatusCode.Created);
        var repetido = await cliente.PostAsJsonAsync($"/api/v1/cotizaciones/{pendiente}/pedido", datos).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("cotizacion_con_pedido", repetido.GetProperty("codigo").GetString());

        await cliente.PostAsJsonAsync("/api/v1/cotizaciones/999999/pedido", datos).EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pedido_Put_RejectsInvalidValues()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var pedido = await api.CrearPedidoAsync();
        var url = $"/api/v1/pedidos/{pedido.GetProperty("idPedido").GetInt32()}";
        var version = pedido.GetProperty("rowVersion").GetString()!;
        object Cuerpo(string modalidad) => new
        {
            idEmpleadoResponsable = pedido.GetProperty("responsable").GetProperty("idEmpleado").GetInt32(),
            fechaEntregaEstimada = FechaEntrega,
            modalidadEntrega = modalidad,
            requiereInstalacion = false,
            costoInstalacionAdicional = 0,
            montoBaseTrabajo = 100_000
        };

        await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, version, Cuerpo("inventada"))).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.PutAsJsonAsync(url, Cuerpo("retiro_local")).EsperarAsync(HttpStatusCode.BadRequest);
        await cliente.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, url, version, Cuerpo("retiro_local"))).EsperarAsync(HttpStatusCode.OK);
    }

    // ── Solicitudes y eliminacion logica ──

    [Fact]
    public async Task Solicitud_ByEmployee_IsPresencialEvenIfBodySaysOtherwise()
    {
        var creada = await api.Cliente(await api.TokenAdminAsync()).PostAsJsonAsync("/api/v1/solicitudes/", new
        {
            idCliente = await api.CrearClienteAsync(),
            idEmpleadoRegistra = (int?)null,
            descripcionGeneral = "Encargo en el local",
            detalles = new[] { new { idProducto = 1, cantidad = 1 } }
        }).EsperarAsync(HttpStatusCode.Created);

        Assert.Equal("presencial", creada.GetProperty("origen").GetString());
        Assert.Equal("Admin Pruebas", creada.GetProperty("registradaPor").GetString());
        var id = creada.GetProperty("idSolicitud").GetInt32();
        Assert.Equal(await api.IdAdminAsync(), await api.ConsultarAsync(db => db.Solicitudes.Where(s => s.IdSolicitud == id).Select(s => s.IdEmpleadoRegistra).SingleAsync()));
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
            new { idCliente, descripcionGeneral = Descripcion, detalles = new[] { new { idProducto = 1, descripcion = "d", cantidad = 0 } } },
            new { idCliente, descripcionGeneral = Descripcion, detalles = new[] { new { idProducto = 999999, descripcion = "d", cantidad = 1 } } },
            new { idCliente, descripcionGeneral = Descripcion, detalles = new[] { new { idProducto = 1, descripcion = "d", cantidad = 1, anchoMm = 100 } } },
            new { idCliente, descripcionGeneral = Descripcion, detalles = new[] { new { idProducto = 1, descripcion = new string('d', 501), cantidad = 1 } } },
            new { idCliente, descripcionGeneral = "corta", detalles = new[] { new { idProducto = 1, descripcion = "d", cantidad = 1 } } },
            new { idCliente = 999999, descripcionGeneral = Descripcion, detalles = new[] { new { idProducto = 1, descripcion = "d", cantidad = 1 } } }
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
            descripcionGeneral = Descripcion,
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

        var editada = await operario.PutAsJsonAsync(url, new { descripcionGeneral = "Editada por el operario", estado = "aceptado", motivoRechazo = "x", rowVersion = abierta.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("Editada por el operario", editada.GetProperty("descripcionGeneral").GetString());
        Assert.Equal("solicitado", editada.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, editada.GetProperty("motivoRechazo").ValueKind);
    }

    // Fase 7: la descripcion general es obligatoria (10 a 2000 caracteres), tambien al editar.
    [Fact]
    public async Task Solicitud_Put_RequiresADescription()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var rechazo = await cliente.PutAsJsonAsync(url, new { descripcionGeneral = " ", rowVersion = abierta.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(rechazo.GetProperty("errores").TryGetProperty("descripcionGeneral", out _));
    }

    // Fase 7: una solicitud rechazada ya no cambia de estado (409) y conserva su motivo.
    [Fact]
    public async Task Solicitud_Rechazada_YaNoCambiaDeEstado()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var url = $"/api/v1/solicitudes/{await api.CrearSolicitudAsync()}";
        var abierta = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var rechazada = await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "rechazado", motivo = "Sin stock del material", rowVersion = abierta.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("Sin stock del material", rechazada.GetProperty("motivoRechazo").GetString());

        await cliente.PatchAsJsonAsync($"{url}/estado", new { estado = "solicitado", rowVersion = rechazada.GetProperty("rowVersion").GetString() }).EsperarAsync(HttpStatusCode.Conflict);
        var actual = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("rechazado", actual.GetProperty("estado").GetString());
        Assert.Equal("Sin stock del material", actual.GetProperty("motivoRechazo").GetString());
    }

    [Fact]
    public async Task Solicitud_TooManyDetails_ReturnsBadRequest()
    {
        var detalles = Enumerable.Range(0, 11).Select(_ => new { idProducto = 1, descripcion = "d", cantidad = 1 }).ToArray();
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();

        var rechazo = await api.Cliente(token).PostAsJsonAsync("/api/v1/mis/solicitudes", new { descripcionGeneral = Descripcion, detalles })
            .EsperarAsync(HttpStatusCode.BadRequest);

        Assert.Contains("10", rechazo.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Solicitud_Delete_IsLogicalAndKeepsTheMetric()
    {
        var token = await api.TokenAdminAsync();
        var creada = await EnviarSolicitudOnlineAsync();
        var id = creada.GetProperty("idSolicitud").GetInt32();
        var totalAntes = (await MetricaDelMesAsync(token)).Total;

        // Fase 7: sin la version vigente no se borra.
        await api.Cliente(token).DeleteAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.BadRequest);
        await api.Cliente(token).SendAsync(ApiFixture.ConVersion(HttpMethod.Delete, $"/api/v1/solicitudes/{id}", await api.VersionAsync(id))).EsperarAsync(HttpStatusCode.NoContent);

        await api.Cliente(token).GetAsync($"/api/v1/solicitudes/{id}").EsperarAsync(HttpStatusCode.NotFound);
        var lista = await api.Cliente(token).GetAsync($"/api/v1/solicitudes/?q={id}").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(lista.GetProperty("elementos").EnumerateArray(), s => s.GetProperty("idSolicitud").GetInt32() == id);

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
        await api.CrearPedidoAsync(idSolicitud: id);

        await api.Cliente(token).SendAsync(ApiFixture.ConVersion(HttpMethod.Delete, $"/api/v1/solicitudes/{id}", await api.VersionAsync(id))).EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Solicitud_LogicallyDeleted_CannotBeQuoted()
    {
        var token = await api.TokenAdminAsync();
        var id = await api.CrearSolicitudAsync();
        await api.Cliente(token).SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/eliminar-logico", await api.VersionAsync(id))).EsperarAsync(HttpStatusCode.OK);

        // Fase 8: una solicitud eliminada ya no existe para cotizar.
        await api.Cliente(token).PostAsJsonAsync($"/api/v1/solicitudes/{id}/cotizaciones", ApiFixture.CuerpoCotizacion())
            .EsperarAsync(HttpStatusCode.NotFound);
    }

    // ── Flujo online y metrica ──
    // (La solicitud publica sin cuenta se elimino en la fase 7: el flujo en linea se prueba en SolicitudesFase7Tests.)

    [Fact]
    public async Task Metric_CountsOnlineRequestsConvertedIntoOrders()
    {
        var token = await api.TokenAdminAsync();
        var antes = await MetricaDelMesAsync(token);
        var id = (await EnviarSolicitudOnlineAsync()).GetProperty("idSolicitud").GetInt32();
        await api.CrearPedidoAsync(idSolicitud: id);

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

        // Fase 9: no hay borrado fisico (se desactiva): las rutas DELETE se retiraron.
        await api.Cliente(token).DeleteAsync($"/api/v1/categorias/{idCategoria}").EsperarAsync(HttpStatusCode.MethodNotAllowed);
        await api.Cliente(token).DeleteAsync($"/api/v1/productos/{idProducto}").EsperarAsync(HttpStatusCode.MethodNotAllowed);
        await api.Cliente(token).DeleteAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.MethodNotAllowed);
        // Y la base sigue impidiendo borrar lo que tiene registros asociados.
        await Assert.ThrowsAnyAsync<Exception>(() => api.ConsultarAsync(db => db.Productos.Where(p => p.IdProducto == idProducto).ExecuteDeleteAsync()));

        await api.Cliente().GetAsync($"/api/v1/productos/{idProducto}").EsperarAsync(HttpStatusCode.OK);
        await api.Cliente(token).GetAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_QuoteUsedByOrder_ReturnsConflict()
    {
        var token = await api.TokenAdminAsync();
        var pedido = await api.CrearPedidoAsync();
        var idCotizacion = pedido.GetProperty("cotizacion").GetProperty("idCotizacion").GetInt32();
        var version = (await api.Cliente(token).GetAsync($"/api/v1/cotizaciones/{idCotizacion}").EsperarAsync(HttpStatusCode.OK))
            .GetProperty("rowVersion").GetString()!;

        await api.Cliente(token).SendAsync(ApiFixture.ConVersion(HttpMethod.Delete, $"/api/v1/cotizaciones/{idCotizacion}", version))
            .EsperarAsync(HttpStatusCode.Conflict);
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

        var vitrina = await api.Cliente().GetAsync("/api/v1/catalogo/?tamano=100").EsperarAsync(HttpStatusCode.OK);

        Assert.DoesNotContain(vitrina.GetProperty("elementos").EnumerateArray(), c => c.GetProperty("titulo").GetString() == titulo);
        await api.Cliente().GetAsync($"/api/v1/catalogo/{id}").EsperarAsync(HttpStatusCode.NotFound);
        await api.Cliente(token).GetAsync($"/api/v1/catalogo/{id}").EsperarAsync(HttpStatusCode.OK);
        // La respuesta ya no dice quien publico (fase 5), pero la base si lo guarda.
        var creador = await api.ConsultarAsync(db => db.Catalogos.Where(c => c.IdCatalogo == id).Select(c => c.IdEmpleadoCreador).SingleAsync());
        Assert.Equal(await api.IdAdminAsync(), creador);
    }

    [Fact]
    public async Task CartelDigital_RecordsTheEmployeeFromTheToken()
    {
        var cartel = await api.Cliente(await api.TokenAdminAsync()).GuardarCartelAsync(new
        {
            abiertoEnLocal = false,
            mensajeEstado = "Volvemos pronto",
            idEmpleadoModifica = 999999
        });

        Assert.Equal(await api.IdAdminAsync(), cartel.GetProperty("idEmpleadoModifica").GetInt32());
        Assert.False(cartel.GetProperty("abiertoEnLocal").GetBoolean());

        var publico = await api.Cliente().GetAsync("/api/v1/cartel-digital").EsperarAsync(HttpStatusCode.OK);
        Assert.False(publico.GetProperty("abiertoEnLocal").GetBoolean());
        Assert.False(publico.TryGetProperty("idEmpleadoModifica", out _));
    }

    private const string Descripcion = "Letrero para la vitrina del local";

    // Fase 7: la solicitud en linea la envia un cliente con sesion y correo confirmado.
    private async Task<JsonElement> EnviarSolicitudOnlineAsync()
    {
        var (_, _, token) = await api.CrearClienteConfirmadoAsync();
        return await api.Cliente(token).PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud()).EsperarAsync(HttpStatusCode.Created);
    }

    private async Task<(int Total, int Concretadas)> MetricaDelMesAsync(string token)
    {
        var filas = await api.Cliente(token).GetAsync("/api/v1/metricas/conversion-online").EsperarAsync(HttpStatusCode.OK);
        // Fase 16B: el mes que cuenta la API es el de Chile; DateTime.Now seria el del equipo (UTC en un contenedor o en CI).
        var hoy = Negocio.Local.HoraChile.Convertir(DateTimeOffset.UtcNow);
        foreach (var fila in filas.EnumerateArray())
        {
            if (fila.GetProperty("anio").GetInt32() == hoy.Year && fila.GetProperty("mes").GetInt32() == hoy.Month)
                return (fila.GetProperty("totalSolicitudesOnline").GetInt32(), fila.GetProperty("solicitudesConcretadasEnPedido").GetInt32());
        }
        return (0, 0);
    }
}
