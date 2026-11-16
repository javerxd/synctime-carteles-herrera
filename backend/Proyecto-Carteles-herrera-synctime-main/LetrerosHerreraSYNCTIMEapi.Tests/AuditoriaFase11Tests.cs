using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 11 (RFS12, RFU28, S20, E25): la bitacora registra sola quien creo, modifico o elimino las entidades
// principales, con los nombres de los campos y solo los valores de una lista permitida; no se puede cambiar
// ni borrar; gestion la consulta con filtros.
[Collection(ApiCollection.Name)]
public sealed class AuditoriaFase11Tests(ApiFixture api)
{
    private static JsonElement Detalle(RegistroAuditoria registro) => JsonDocument.Parse(registro.Detalle!).RootElement;

    private static string[] Campos(RegistroAuditoria registro) =>
        Detalle(registro).GetProperty("campos").EnumerateArray().Select(c => c.GetString()!).ToArray();

    [Fact]
    public async Task CrearYModificar_QuedanConQuienRolYCampos()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var idAdmin = await api.IdAdminAsync();
        var categoria = await admin.PostAsJsonAsync("/api/v1/categorias", new { nombre = $"Auditada {api.Siguiente()}", descripcion = "x" })
            .EsperarAsync(HttpStatusCode.Created);
        var id = categoria.GetProperty("idCategoria").GetInt32();

        var creada = Assert.Single(await api.AuditoriaAsync("categoria", id.ToString(), "crear"));
        Assert.Equal(idAdmin, creada.IdEmpleado);
        Assert.Null(creada.IdCliente);
        Assert.Equal(Roles.Administrador, creada.Rol);
        Assert.True(DateTimeOffset.UtcNow - creada.Fecha < TimeSpan.FromMinutes(5));

        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/categorias/{id}/activo", categoria.GetProperty("rowVersion").GetString()!,
            new { activo = false })).EsperarAsync(HttpStatusCode.OK);
        var modificada = Assert.Single(await api.AuditoriaAsync("categoria", id.ToString(), "modificar"));
        Assert.Contains("activo", Campos(modificada));
        var valor = Detalle(modificada).GetProperty("valores").GetProperty("activo");
        Assert.True(valor.GetProperty("antes").GetBoolean());
        Assert.False(valor.GetProperty("despues").GetBoolean());
    }

    [Fact]
    public async Task UnCambioDeEstado_GuardaElAntesYElDespues_YUnTextoLibreSoloSuNombre()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var id = await api.CrearSolicitudAsync();
        await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Patch, $"/api/v1/solicitudes/{id}/estado", await api.VersionAsync(id),
            new { estado = "rechazado", motivo = "Motivo-libre-que-no-se-copia" })).EsperarAsync(HttpStatusCode.OK);

        var registro = (await api.AuditoriaAsync("solicitud", id.ToString(), "modificar")).Last();
        Assert.Contains("estado", Campos(registro));
        Assert.Contains("motivoRechazo", Campos(registro));
        var estado = Detalle(registro).GetProperty("valores").GetProperty("estado");
        Assert.Equal("solicitado", estado.GetProperty("antes").GetString());
        Assert.Equal("rechazado", estado.GetProperty("despues").GetString());
        Assert.DoesNotContain("Motivo-libre", registro.Detalle);
        // RNFO04: la rechazada se conserva.
        Assert.True(await api.ConsultarAsync(db => db.Solicitudes.AnyAsync(s => s.IdSolicitud == id)));
    }

    [Fact]
    public async Task LaAuditoria_NoGuardaContrasenasHashesNiCorreos()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var (id, correo, _) = await api.CrearClienteConfirmadoAsync();
        var nueva = $"Nv-{Guid.NewGuid():N}"[..20];
        await admin.PutAsJsonAsync($"/api/v1/clientes/{id}/credenciales", new { password = nueva, passwordActual = ApiFixture.AdminPassword })
            .EsperarAsync(HttpStatusCode.NoContent);
        var hash = await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == id).Select(c => c.PasswordHash).SingleAsync());

        var registros = await api.AuditoriaAsync("cliente", id.ToString());
        Assert.NotEmpty(registros);
        foreach (var registro in await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().ToListAsync()))
        {
            Assert.DoesNotContain(hash!, registro.Detalle ?? "");
            Assert.DoesNotContain(nueva, registro.Detalle ?? "");
            Assert.DoesNotContain(ApiFixture.AdminPassword, registro.Detalle ?? "");
            Assert.DoesNotContain(correo, registro.Detalle ?? "", StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains(registros, r => r.Accion == "modificar" && Campos(r).Contains("passwordHash")
            && !(Detalle(r).TryGetProperty("valores", out var valores) && valores.TryGetProperty("passwordHash", out _)));
    }

    [Fact]
    public async Task CadaAbonoYCadaAnulacion_QuedanConQuienCuandoYCuanto()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var pedido = await api.CrearPedidoAsync(80_000);
        var id = pedido.GetProperty("idPedido").GetInt32();
        var conAbono = await admin.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos", new { monto = 30_000, medioPago = "efectivo" }).EsperarAsync(HttpStatusCode.Created);
        var idPago = conAbono.GetProperty("pagos").EnumerateArray().Last().GetProperty("idPago").GetInt32();
        await admin.PostAsJsonAsync($"/api/v1/pedidos/{id}/pagos/{idPago}/anular", new { motivo = "Mal ingresado" }).EsperarAsync(HttpStatusCode.OK);

        var abono = Assert.Single(await api.AuditoriaAsync("pago", idPago.ToString(), "crear"));
        Assert.Equal(await api.IdAdminAsync(), abono.IdEmpleado);
        Assert.Equal(30_000, Detalle(abono).GetProperty("valores").GetProperty("monto").GetProperty("despues").GetInt32());
        var anulacion = Assert.Single(await api.AuditoriaAsync("pago", idPago.ToString(), "modificar"));
        Assert.True(Detalle(anulacion).GetProperty("valores").GetProperty("anulado").GetProperty("despues").GetBoolean());
        Assert.Equal(30_000, Detalle(anulacion).GetProperty("valores").GetProperty("monto").GetProperty("despues").GetInt32());
    }

    [Fact]
    public async Task LasEntidadesPrincipales_QuedanCubiertas()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        // Un pedido completo (solicitud, detalle, cotizacion, pedido, pago), un cliente, un empleado, un material,
        // un producto, el horario y el cartel.
        var pedido = await api.CrearPedidoAsync(50_000, abonoInicial: new { monto = 10_000, medioPago = "efectivo" });
        var idCliente = await api.CrearClienteAsync();
        var (idEmpleado, _, _) = await api.CrearEmpleadoAsync(Roles.Operario);
        await admin.PostAsJsonAsync("/api/v1/materiales", new { nombre = $"Material {api.Siguiente()}", unidadMedida = "m2", costoUnitarioAprox = 1000 })
            .EsperarAsync(HttpStatusCode.Created);
        await admin.PostAsJsonAsync("/api/v1/productos", new { idCategoria = 1, nombre = $"Producto {api.Siguiente()}", descripcion = "x" })
            .EsperarAsync(HttpStatusCode.Created);
        await admin.GuardarCartelAsync(new { abiertoEnLocal = true, mensajeEstado = "Local abierto y atendiendo" });

        var entidades = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().Select(a => a.Entidad).Distinct().ToListAsync());
        foreach (var entidad in new[] { "solicitud", "solicitud_detalle", "cotizacion", "pedido", "pago", "cliente", "empleado", "material", "producto", "cartel_digital" })
            Assert.Contains(entidad, entidades);
        Assert.NotEmpty(await api.AuditoriaAsync("cliente", idCliente.ToString(), "crear"));
        Assert.NotEmpty(await api.AuditoriaAsync("empleado", idEmpleado.ToString(), "crear"));
        Assert.NotEmpty(await api.AuditoriaAsync("pedido", pedido.GetProperty("idPedido").GetInt32().ToString(), "crear"));
    }

    [Fact]
    public async Task LaTablaDeAuditoria_NoSePuedeModificarNiBorrar()
    {
        await api.CrearClienteAsync();
        var id = await api.ConsultarAsync(db => db.Auditoria.MaxAsync(a => a.IdAuditoria));

        await Assert.ThrowsAnyAsync<DbException>(() => api.ConsultarAsync(db =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.auditoria SET accion = 'otra' WHERE id_auditoria = {id}")));
        await Assert.ThrowsAnyAsync<DbException>(() => api.ConsultarAsync(db =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.auditoria WHERE id_auditoria = {id}")));
        await Assert.ThrowsAnyAsync<DbException>(() => api.ConsultarAsync(db => db.Auditoria.Where(a => a.IdAuditoria == id).ExecuteDeleteAsync()));

        var sigue = await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().SingleAsync(a => a.IdAuditoria == id));
        Assert.NotEqual("otra", sigue.Accion);
    }

    [Fact]
    public async Task ElVisor_FiltraPaginaYSoloLoVeGestion()
    {
        var admin = api.Cliente(await api.TokenAdminAsync());
        var categoria = await admin.PostAsJsonAsync("/api/v1/categorias", new { nombre = $"Visor {api.Siguiente()}" }).EsperarAsync(HttpStatusCode.Created);
        var id = categoria.GetProperty("idCategoria").GetInt32().ToString();

        var pagina = await admin.GetAsync($"/api/v1/auditoria?entidad=categoria&accion=crear&q={id}&tamano=5").EsperarAsync(HttpStatusCode.OK);
        var fila = Assert.Single(pagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal("categoria", fila.GetProperty("entidad").GetString());
        Assert.Equal("Categoría", fila.GetProperty("entidadTexto").GetString());
        Assert.Equal("Creó", fila.GetProperty("accionTexto").GetString());
        Assert.Equal(id, fila.GetProperty("idEntidad").GetString());
        Assert.Equal("empleado", fila.GetProperty("usuario").GetProperty("tipo").GetString());
        Assert.Equal(await api.IdAdminAsync(), fila.GetProperty("usuario").GetProperty("id").GetInt32());
        Assert.Equal("Admin Pruebas", fila.GetProperty("usuario").GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Object, fila.GetProperty("detalle").ValueKind);

        var todas = await admin.GetAsync("/api/v1/auditoria?tamano=3").EsperarAsync(HttpStatusCode.OK);
        var fechas = todas.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("fecha").GetDateTimeOffset()).ToList();
        Assert.Equal(fechas.OrderByDescending(f => f), fechas);
        Assert.True(todas.GetProperty("total").GetInt32() >= 3);

        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).GetAsync("/api/v1/auditoria").EsperarAsync(HttpStatusCode.Forbidden);
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).GetAsync("/api/v1/auditoria/consentimientos").EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LosConsentimientos_SeConsultan_ConTitularVersionYFecha()
    {
        var (idCliente, _, _) = await api.RegistrarClienteAsync();
        var admin = api.Cliente(await api.TokenAdminAsync());

        var pagina = await admin.GetAsync("/api/v1/auditoria/consentimientos?tipo=terminos&tamano=100").EsperarAsync(HttpStatusCode.OK);
        var fila = pagina.GetProperty("elementos").EnumerateArray()
            .First(e => e.GetProperty("titular").GetProperty("tipo").GetString() == "cliente" && e.GetProperty("titular").GetProperty("id").GetInt32() == idCliente);
        Assert.Equal("terminos", fila.GetProperty("tipo").GetString());
        Assert.Equal("1.0", fila.GetProperty("version").GetString());
        Assert.True(fila.GetProperty("acepta").GetBoolean());
        Assert.Equal("Cli Web", fila.GetProperty("titular").GetProperty("nombre").GetString());
        Assert.NotEqual(default, fila.GetProperty("fecha").GetDateTimeOffset());
    }
}
