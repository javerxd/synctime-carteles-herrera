using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Revision de la profesora guia (12-11-2026): el pedido tiene un estado operativo y un estado de pago
// independientes. Cada abono queda en la tabla pagos con su fecha y su medio de pago, y el monto
// abonado del pedido es la suma de ellos. Estas pruebas fijan las reglas que la base garantiza por si sola.
[Collection(ApiCollection.Name)]
public sealed class PedidoPagoTests(ApiFixture api)
{
    private const int MontoBase = 100_000;

    [Fact]
    public async Task EstadoDePago_SeDerivaDeLosAbonos()
    {
        var id = await CrearPedidoAsync();

        Assert.Equal(("no_pagado", MontoBase), await PagoAsync(id));

        await AbonarAsync(id, 40_000);
        Assert.Equal(("abonado", 60_000), await PagoAsync(id));

        await AbonarAsync(id, 60_000);
        Assert.Equal(("pagado_total", 0), await PagoAsync(id));
    }

    [Fact]
    public async Task CadaAbono_QuedaEnElHistorialConFechaMedioYQuienLoRegistro()
    {
        var id = await CrearPedidoAsync();
        var idAdmin = await api.IdAdminAsync();

        await AbonarAsync(id, 30_000, "transferencia", new DateOnly(2026, 10, 5));
        await AbonarAsync(id, 20_000, "efectivo", new DateOnly(2026, 11, 2));

        var historial = await api.ConsultarAsync(db => db.Pagos.AsNoTracking()
            .Where(p => p.IdPedido == id)
            .OrderBy(p => p.FechaPago)
            .Select(p => new { p.Monto, p.MedioPago, p.FechaPago, p.IdEmpleadoRegistra })
            .ToListAsync());

        Assert.Equal([30_000, 20_000], historial.Select(h => h.Monto));
        Assert.Equal(["transferencia", "efectivo"], historial.Select(h => h.MedioPago));
        Assert.Equal([new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 2)], historial.Select(h => h.FechaPago));
        Assert.All(historial, h => Assert.Equal(idAdmin, h.IdEmpleadoRegistra));
        Assert.Equal(("abonado", 50_000), await PagoAsync(id));
    }

    [Fact]
    public async Task UnAbonoSinFecha_TomaLaFechaDeHoy()
    {
        var id = await CrearPedidoAsync();
        var idPago = await AbonarAsync(id, 10_000);

        var (fecha, hoyEnElServidor) = await api.ConsultarAsync(async db => (
            await db.Pagos.AsNoTracking().Where(p => p.IdPago == idPago).Select(p => p.FechaPago).SingleAsync(),
            DateOnly.FromDateTime(await db.Database.SqlQueryRaw<DateTime>("SELECT SYSDATETIME() AS Value").SingleAsync())));

        Assert.Equal(hoyEnElServidor, fecha);
    }

    [Fact]
    public async Task CorregirSoloLaObservacion_NoCambiaLaVersionDelPedido()
    {
        var id = await CrearPedidoAsync();
        var idPago = await AbonarAsync(id, 10_000);
        var antes = await VersionAsync(id);

        await api.ConsultarAsync(db => db.Pagos.Where(p => p.IdPago == idPago)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Observacion, "Transferencia 4471")));

        Assert.Equal(antes, await VersionAsync(id));
    }

    [Fact]
    public async Task AbonosSimultaneos_DejanElTotalIgualALaSuma()
    {
        var id = await CrearPedidoAsync();

        // Seis abonos a la vez sobre el mismo pedido: ninguno puede fallar por interbloqueo ni perderse.
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => AbonarAsync(id, 10_000)));

        var (abonado, suma) = await api.ConsultarAsync(async db => (
            await db.Pedidos.AsNoTracking().Where(p => p.IdPedido == id).Select(p => p.MontoAbonado).SingleAsync(),
            await db.Pagos.Where(p => p.IdPedido == id).SumAsync(p => p.Monto)));

        Assert.Equal(60_000, suma);
        Assert.Equal(suma, abonado);
    }

    [Fact]
    public async Task MoverUnAbonoAOtroPedido_AjustaLosDos()
    {
        var origen = await CrearPedidoAsync();
        var destino = await CrearPedidoAsync();
        var idPago = await AbonarAsync(origen, 25_000);

        await api.ConsultarAsync(db => db.Pagos.Where(p => p.IdPago == idPago)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IdPedido, destino)));

        Assert.Equal(("no_pagado", MontoBase), await PagoAsync(origen));
        Assert.Equal(("abonado", 75_000), await PagoAsync(destino));
    }

    [Fact]
    public async Task EfNuncaEscribeElMontoAbonado()
    {
        var idSolicitud = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(idSolicitud);
        var idAdmin = await api.IdAdminAsync();

        var id = await api.ConsultarAsync(async db =>
        {
            var pedido = new Pedido
            {
                IdSolicitud = idSolicitud,
                IdEmpleadoResponsable = idAdmin,
                FechaEntregaEstimada = DateTime.Now.AddDays(5),
                MontoBaseTrabajo = MontoBase,
                MontoAbonado = 50_000
            };
            db.Pedidos.Add(pedido);
            await db.SaveChangesAsync();
            return pedido.IdPedido;
        });
        Assert.Equal(("no_pagado", MontoBase), await PagoAsync(id));

        await api.ConsultarAsync(async db =>
        {
            var pedido = await db.Pedidos.SingleAsync(p => p.IdPedido == id);
            pedido.MontoAbonado = 99_000;
            pedido.EstadoPedido = "en_proceso";
            return await db.SaveChangesAsync();
        });
        Assert.Equal(("no_pagado", MontoBase), await PagoAsync(id));
    }

    [Fact]
    public async Task UnMontoConDecimales_LoRechazaLaBase()
    {
        var id = await CrearPedidoAsync();

        await Assert.ThrowsAnyAsync<DbException>(() => api.ConsultarAsync(db => db.Pedidos.Where(p => p.IdPedido == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.MontoBaseTrabajo, 100_000.50m))));
    }

    [Fact]
    public async Task UnMontoConDecimales_LoRechazaLaApi()
    {
        var (cliente, url) = await PedidoEnApiAsync();
        var actual = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var cuerpo = new
        {
            estadoPedido = "en_proceso",
            fechaEntregaEstimada = DateTime.Now.AddDays(5),
            modalidadEntrega = "retiro_local",
            montoBaseTrabajo = 100_000.50m,
            rowVersion = actual.GetProperty("rowVersion").GetString()
        };
        // La API lo detecta antes que la base, con un mensaje claro (nivel "api", no "base_de_datos").
        var alEditar = await cliente.PutAsJsonAsync(url, cuerpo).EsperarAsync(HttpStatusCode.BadRequest);
        AssertErrorDeApi(alEditar, "datos_invalidos", "decimales");

        var idSolicitud = await api.CrearSolicitudAsync();
        await api.AceptarSolicitudAsync(idSolicitud);
        var alCrear = await cliente.PostAsJsonAsync("/api/v1/pedidos/", new
        {
            idSolicitud,
            idEmpleadoResponsable = await api.IdAdminAsync(),
            fechaEntregaEstimada = DateTime.Now.AddDays(5),
            montoBaseTrabajo = 50_000,
            costoInstalacionAdicional = 9_990.90m
        }).EsperarAsync(HttpStatusCode.BadRequest);
        AssertErrorDeApi(alCrear, "datos_invalidos", "decimales");
    }

    [Fact]
    public async Task BajarElMontoFinalBajoLoAbonado_EsConflicto()
    {
        var (cliente, url) = await PedidoEnApiAsync();
        var id = IdDe(url);
        await AbonarAsync(id, 30_000);
        var actual = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        var cuerpo = new
        {
            estadoPedido = "en_proceso",
            fechaEntregaEstimada = DateTime.Now.AddDays(5),
            modalidadEntrega = "retiro_local",
            montoBaseTrabajo = 20_000,
            rowVersion = actual.GetProperty("rowVersion").GetString()
        };
        var error = await cliente.PutAsJsonAsync(url, cuerpo).EsperarAsync(HttpStatusCode.Conflict);

        AssertErrorDeApi(error, "conflicto", "abonado");
        Assert.Equal(("abonado", 70_000), await PagoAsync(id));
    }

    [Fact]
    public async Task UnAbono_InvalidaLaVersionQueTeniaAbiertaElPersonal()
    {
        var (cliente, url) = await PedidoEnApiAsync();
        var abierto = await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);

        await AbonarAsync(IdDe(url), 30_000);

        await cliente.PutAsJsonAsync(url, Cuerpo("en_proceso", abierto)).EsperarAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task BorrarUnPedidoConAbonos_EsConflicto()
    {
        var (cliente, url) = await PedidoEnApiAsync();
        await AbonarAsync(IdDe(url), 10_000);

        var error = await cliente.DeleteAsync(url).EsperarAsync(HttpStatusCode.Conflict);

        AssertErrorDeApi(error, "conflicto", "anular");
        await cliente.GetAsync(url).EsperarAsync(HttpStatusCode.OK);
    }

    private static void AssertErrorDeApi(System.Text.Json.JsonElement error, string codigo, string palabraDelMensaje)
    {
        Assert.Equal(codigo, error.GetProperty("codigo").GetString());
        Assert.Equal("api", error.GetProperty("nivel").GetString());
        Assert.Contains(palabraDelMensaje, error.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CorregirUnAbono_RecalculaElMontoAbonado()
    {
        var id = await CrearPedidoAsync();
        var idPago = await AbonarAsync(id, 10_000);

        await api.ConsultarAsync(db => db.Pagos.Where(p => p.IdPago == idPago)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Monto, 15_000)));

        Assert.Equal(("abonado", 85_000), await PagoAsync(id));
    }

    [Fact]
    public async Task BorrarUnAbonoRegistradoPorError_RecalculaElMontoAbonado()
    {
        var id = await CrearPedidoAsync();
        await AbonarAsync(id, 40_000);
        var idErroneo = await AbonarAsync(id, 25_000);

        await api.ConsultarAsync(db => db.Pagos.Where(p => p.IdPago == idErroneo).ExecuteDeleteAsync());

        Assert.Equal(("abonado", 60_000), await PagoAsync(id));
    }

    [Fact]
    public async Task VariosAbonosEnUnaSolaOperacion_ActualizanCadaPedido()
    {
        var a = await CrearPedidoAsync();
        var b = await CrearPedidoAsync();
        var idAdmin = await api.IdAdminAsync();

        await api.ConsultarAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.pagos (id_pedido, monto, medio_pago, id_empleado_registra)
            VALUES ({a}, 10000, N'efectivo', {idAdmin}),
                   ({a}, 5000, N'efectivo', {idAdmin}),
                   ({b}, 70000, N'transferencia', {idAdmin})
            """));

        Assert.Equal(("abonado", 85_000), await PagoAsync(a));
        Assert.Equal(("abonado", 30_000), await PagoAsync(b));
    }

    [Fact]
    public async Task AbonosQueSuperanElMontoFinal_LosRechazaLaBase()
    {
        var id = await CrearPedidoAsync();
        await AbonarAsync(id, 60_000);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => AbonarAsync(id, 40_001));

        Assert.Equal(("abonado", 40_000), await PagoAsync(id));
        Assert.Equal(1, await ContarAbonosAsync(id));
    }

    [Theory]
    [InlineData(0, "efectivo")]
    [InlineData(-5_000, "efectivo")]
    [InlineData(10_000, "bitcoin")]
    public async Task UnAbonoInvalido_LoRechazaLaBase(int monto, string medio)
    {
        var id = await CrearPedidoAsync();

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => AbonarAsync(id, monto, medio));

        Assert.Equal(0, await ContarAbonosAsync(id));
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

    // El registro de abonos por la API llega en la fase 8; aqui se guarda con EF, que ademas comprueba
    // que el modelo declara el trigger de la tabla (sin HasTrigger, SQL Server rechaza el INSERT de EF).
    private async Task<int> AbonarAsync(int idPedido, int monto, string medio = "efectivo", DateOnly? fecha = null)
    {
        var idEmpleado = await api.IdAdminAsync();
        return await api.ConsultarAsync(async db =>
        {
            var pago = new Pago { IdPedido = idPedido, Monto = monto, MedioPago = medio, IdEmpleadoRegistra = idEmpleado };
            if (fecha is { } f)
                pago.FechaPago = f;
            db.Pagos.Add(pago);
            await db.SaveChangesAsync();
            return pago.IdPago;
        });
    }

    private static int IdDe(string url) => int.Parse(url[(url.LastIndexOf('/') + 1)..]);

    private Task<byte[]> VersionAsync(int idPedido) =>
        api.ConsultarAsync(db => db.Pedidos.AsNoTracking().Where(p => p.IdPedido == idPedido)
            .Select(p => p.RowVersion).SingleAsync());

    private Task<int> ContarAbonosAsync(int idPedido) =>
        api.ConsultarAsync(db => db.Pagos.CountAsync(p => p.IdPedido == idPedido));

    private Task<(string, int)> PagoAsync(int idPedido) =>
        api.ConsultarAsync(async db =>
        {
            var p = await db.Pedidos.AsNoTracking().SingleAsync(x => x.IdPedido == idPedido);
            return (p.EstadoPago, p.SaldoPendiente);
        });
}
