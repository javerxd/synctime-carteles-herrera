using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 12B (RNFE01): la IP de la bitacora se borra a los 90 dias; el registro del evento queda intacto. El trigger de la
// bitacora admite exactamente ese cambio y rechaza cualquier otro UPDATE o DELETE.
[Collection(ApiCollection.Name)]
public sealed class PurgaIpAuditoriaTests(ApiFixture api)
{
    private async Task<long> InsertarAsync(int dias, string? ip, string detalle) => await api.ConsultarAsync(async db =>
    {
        var fila = new RegistroAuditoria
        {
            Fecha = DateTimeOffset.UtcNow.AddDays(-dias), Rol = "sistema", Entidad = "PruebaIp", IdEntidad = detalle, Accion = "crear", Detalle = detalle, Ip = ip
        };
        db.Auditoria.Add(fila);
        await db.SaveChangesAsync();
        return fila.IdAuditoria;
    });

    private async Task<RegistroAuditoria> LeerAsync(long id) => await api.ConsultarAsync(db => db.Auditoria.AsNoTracking().SingleAsync(a => a.IdAuditoria == id));

    private async Task<ResultadoPurga> PurgarAsync()
    {
        await using var alcance = api.Services.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<PurgaDatosPersonales>().PurgarAsync(CancellationToken.None);
    }

    private async Task<string> SqlRechazadoAsync(string sql) =>
        (await Assert.ThrowsAnyAsync<Exception>(() => api.ConsultarAsync(db => db.Database.ExecuteSqlRawAsync(sql)))).ToString();

    [Fact]
    public async Task LaIpDeMasDe90Dias_SeBorra_YLaDeMenosDe90_NoSeToca_SinPerderElRegistro()
    {
        var antes = await api.ConsultarAsync(db => db.Auditoria.CountAsync());
        var a91 = await InsertarAsync(91, "10.1.1.1", "ip-91-dias");
        var a89 = await InsertarAsync(89, "10.1.1.2", "ip-89-dias");
        var a400 = await InsertarAsync(400, "10.1.1.3", "ip-400-dias");
        var sinIp = await InsertarAsync(200, null, "sin-ip-200-dias");

        var resultado = await PurgarAsync();

        var (viejo, reciente, muyViejo, nula) = (await LeerAsync(a91), await LeerAsync(a89), await LeerAsync(a400), await LeerAsync(sinIp));
        Assert.Null(viejo.Ip);
        Assert.Null(muyViejo.Ip);
        Assert.Equal("10.1.1.2", reciente.Ip);   // 89 dias: todavia dentro del plazo
        Assert.Null(nula.Ip);
        Assert.True(resultado.IpsDeAuditoria >= 2);
        // El registro del evento queda intacto: ni se borro ninguna fila ni cambio otra columna.
        Assert.Equal(antes + 4, await api.ConsultarAsync(db => db.Auditoria.CountAsync()));
        Assert.Equal(("PruebaIp", "ip-91-dias", "crear", "sistema"), (viejo.Entidad, viejo.Detalle, viejo.Accion, viejo.Rol));
        Assert.Equal(DateTimeOffset.UtcNow.AddDays(-91).Date, viejo.Fecha.UtcDateTime.Date);
    }

    [Fact]
    public async Task LaPurga_EsRepetible_YNoVuelveATocarLoYaBorrado()
    {
        var id = await InsertarAsync(120, "10.2.2.2", "repetible");

        await PurgarAsync();
        var segunda = await PurgarAsync();

        Assert.Null((await LeerAsync(id)).Ip);
        Assert.Equal(0, segunda.IpsDeAuditoria + await api.ConsultarAsync(db => db.Auditoria.CountAsync(a => a.Ip != null && a.Fecha < DateTimeOffset.UtcNow.AddDays(-91))));
    }

    [Fact]
    public async Task ElTrigger_RechazaCualquierOtroCambioDeLaBitacora()
    {
        var reciente = await InsertarAsync(10, "10.3.3.3", "reciente");
        var viejo = await InsertarAsync(100, "10.3.3.4", "viejo");

        // Borrar la IP de una fila de menos de 90 dias, cambiarla por otra, tocar otra columna y borrar la fila: todo rechazado.
        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"UPDATE auditoria SET ip = NULL WHERE id_auditoria = {reciente}"));
        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"UPDATE auditoria SET ip = '9.9.9.9' WHERE id_auditoria = {viejo}"));
        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"UPDATE auditoria SET ip = NULL, detalle = N'alterado' WHERE id_auditoria = {viejo}"));
        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"UPDATE auditoria SET accion = 'eliminar' WHERE id_auditoria = {viejo}"));
        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"DELETE FROM auditoria WHERE id_auditoria = {viejo}"));

        Assert.Equal(("10.3.3.3", "10.3.3.4", "viejo", "crear"), ((await LeerAsync(reciente)).Ip!, (await LeerAsync(viejo)).Ip!, (await LeerAsync(viejo)).Detalle!, (await LeerAsync(viejo)).Accion));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(60)]
    [InlineData(89)]
    public async Task ElTrigger_RechazaBorrarLaIpDeUnaFilaDeMenosDe90Dias_AunqueTengaMasDeUnMes(int dias)
    {
        var id = await InsertarAsync(dias, "10.5.5.5", $"dentro-del-plazo-{dias}");

        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"UPDATE auditoria SET ip = NULL WHERE id_auditoria = {id}"));

        Assert.Equal("10.5.5.5", (await LeerAsync(id)).Ip);
    }

    // El EXCEPT del trigger compara las columnas de la bitacora UNA POR UNA. Si alguien agrega una columna y no actualiza el trigger,
    // un UPDATE podria cambiarla mientras borra la IP. Esta prueba obliga a actualizar el trigger (migracion nueva).
    [Fact]
    public async Task LaBitacora_TieneSoloLasColumnasQueElTriggerCompara()
    {
        var columnas = await api.ConsultarAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sys.columns WHERE object_id = OBJECT_ID('dbo.auditoria') ORDER BY column_id").ToListAsync());

        Assert.Equal(["id_auditoria", "fecha", "id_empleado", "id_cliente", "rol", "entidad", "id_entidad", "accion", "detalle", "ip"], columnas);
    }

    [Fact]
    public async Task LosIdentity_DelCatalogoQuedanEnElUltimoIdReal_NoEnLosTemporales()
    {
        var (producto, categoria) = await api.ConsultarAsync(async db => (
            await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int, IDENT_CURRENT('dbo.productos')) AS Value").SingleAsync(),
            await db.Database.SqlQueryRaw<int>("SELECT CONVERT(int, IDENT_CURRENT('dbo.categorias')) AS Value").SingleAsync()));

        Assert.True(producto < 1000 && categoria < 1000, $"productos={producto}, categorias={categoria}");
    }

    [Fact]
    public async Task ElTrigger_AdmiteBorrarLaIpDeUnaFilaDeMasDe90Dias_YNadaMas()
    {
        var viejo = await InsertarAsync(100, "10.4.4.4", "borrable");

        await api.ConsultarAsync(db => db.Database.ExecuteSqlRawAsync($"UPDATE auditoria SET ip = NULL WHERE id_auditoria = {viejo}"));

        var fila = await LeerAsync(viejo);
        Assert.Null(fila.Ip);
        Assert.Equal("borrable", fila.Detalle);
        // Ya sin IP, no hay nada mas que borrar: el cambio se rechaza como cualquier otro.
        Assert.Contains("no se modifica ni se borra", await SqlRechazadoAsync($"UPDATE auditoria SET ip = NULL WHERE id_auditoria = {viejo}"));
    }
}
