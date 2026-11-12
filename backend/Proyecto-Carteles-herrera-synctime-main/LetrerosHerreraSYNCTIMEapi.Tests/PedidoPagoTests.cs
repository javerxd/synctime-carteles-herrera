using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Revision de la profesora guia (14-11-2026): el pedido tiene un estado operativo y un estado de pago
// independientes. Estas pruebas fijan las reglas que la base garantiza por si sola.
[Collection(ApiCollection.Name)]
public sealed class PedidoPagoTests(ApiFixture api)
{
    private const int MontoBase = 100_000;

    [Fact]
    public async Task EstadoDePago_SeDerivaDelMontoAbonado()
    {
        var id = await CrearPedidoAsync();

        Assert.Equal(("no_pagado", MontoBase), await PagoAsync(id));

        await AbonarAsync(id, 40_000);
        Assert.Equal(("abonado", 60_000), await PagoAsync(id));

        await AbonarAsync(id, MontoBase);
        Assert.Equal(("pagado_total", 0), await PagoAsync(id));
    }

    [Fact]
    public async Task UnAbonoMayorAlMontoFinal_LoRechazaLaBase()
    {
        var id = await CrearPedidoAsync();

        await Assert.ThrowsAnyAsync<DbException>(() => AbonarAsync(id, MontoBase + 1));
        Assert.Equal(("no_pagado", MontoBase), await PagoAsync(id));
    }

    [Fact]
    public async Task UnPedido_SePuedeEntregarSinEstarPagado()
    {
        var (cliente, url) = await PedidoEnApiAsync();
        var creado = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var entregado = await cliente.PutAsJsonAsync(url, Cuerpo("entregado", creado)).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("entregado", entregado.GetProperty("estadoPedido").GetString());
        Assert.Equal("no_pagado", entregado.GetProperty("estadoPago").GetString());
        Assert.Equal(MontoBase, entregado.GetProperty("saldoPendiente").GetInt32());
    }

    [Fact]
    public async Task ElEstadoCancelado_SeLlamaAnulado()
    {
        var (cliente, url) = await PedidoEnApiAsync();
        var creado = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        await cliente.PutAsJsonAsync(url, Cuerpo("cancelado", creado)).EsperarAsync(HttpStatusCode.BadRequest);
        var anulado = await cliente.PutAsJsonAsync(url, Cuerpo("anulado", creado)).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal("anulado", anulado.GetProperty("estadoPedido").GetString());
    }

    private static object Cuerpo(string estadoPedido, System.Text.Json.JsonElement pedido) => new
    {
        estadoPedido,
        fechaEntregaEstimada = DateTime.Now.AddDays(5),
        modalidadEntrega = "retiro_local",
        montoBaseTrabajo = MontoBase,
        rowVersion = pedido.GetProperty("rowVersion").GetString()
    };

    private async Task<(HttpClient Cliente, string Url)> PedidoEnApiAsync()
    {
        var id = await CrearPedidoAsync();
        return (api.Cliente(await api.TokenAdminAsync()), $"/api/v1/pedidos/{id}");
    }

    private async Task<int> CrearPedidoAsync()
    {
        var cliente = api.Cliente(await api.TokenAdminAsync());
        var idSolicitud = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(idSolicitud);
        var creado = await cliente.PostAsJsonAsync("/api/v1/pedidos/", new
        {
            idSolicitud,
            idEmpleadoResponsable = await api.IdAdminAsync(),
            fechaEntregaEstimada = DateTime.Now.AddDays(5),
            montoBaseTrabajo = MontoBase
        }).EsperarAsync(HttpStatusCode.Created);
        return creado.GetProperty("idPedido").GetInt32();
    }

    // El registro de abonos por la API llega en la fase 8; aqui se escribe directo en la base para
    // probar las reglas que ella misma impone.
    private Task<int> AbonarAsync(int idPedido, int monto) =>
        api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE pedidos SET monto_abonado = {monto} WHERE id_pedido = {idPedido}"));

    private Task<(string, int)> PagoAsync(int idPedido) =>
        api.ConsultarAsync(async db =>
        {
            var p = await db.Pedidos.AsNoTracking().SingleAsync(x => x.IdPedido == idPedido);
            return (p.EstadoPago, p.SaldoPendiente);
        });
}
