using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// RFS12: quien acepto, que documento, que version, y cuando; y cuando lo retiro.
// Los formularios que lo usan llegan en las fases 5 y 6: aqui se prueba el servicio y la tabla.
[Collection(ApiCollection.Name)]
public sealed class ConsentimientoServicioTests(ApiFixture api)
{
    private async Task<T> ConServicioAsync<T>(Func<IServicioConsentimientos, Task<T>> accion)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await accion(scope.ServiceProvider.GetRequiredService<IServicioConsentimientos>());
    }

    private static string Vigente(string tipo) => DocumentosLegales.VersionVigente(tipo)!;

    [Fact]
    public async Task UnConsentimiento_QuedaConTipoVersionYHora()
    {
        var idCliente = await api.CrearClienteAsync();
        var antes = DateTimeOffset.UtcNow.AddSeconds(-2);

        var resultado = await ConServicioAsync(s => s.RegistrarAsync(TitularConsentimiento.Cliente(idCliente), DocumentosLegales.Privacidad, Vigente(DocumentosLegales.Privacidad), CancellationToken.None));

        Assert.Null(resultado.Error);
        var registro = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().SingleAsync(c => c.IdCliente == idCliente));
        Assert.Equal("privacidad", registro.Tipo);
        Assert.Equal(Vigente(DocumentosLegales.Privacidad), registro.VersionDocumento);
        Assert.InRange(registro.FechaAceptacion, antes, DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Null(registro.FechaRevocacion);
        Assert.Null(registro.IdEmpleado);
        Assert.Null(registro.ReferenciaEnvio);
    }

    [Fact]
    public async Task UnaVersionQueNoEsLaVigente_SeRechaza()
    {
        var idCliente = await api.CrearClienteAsync();

        var resultado = await ConServicioAsync(s => s.RegistrarAsync(TitularConsentimiento.Cliente(idCliente), DocumentosLegales.Terminos, "0.9", CancellationToken.None));

        Assert.Contains("versión", resultado.Error);
        Assert.False(await api.ConsultarAsync(db => db.Consentimientos.AnyAsync(c => c.IdCliente == idCliente)));
    }

    [Fact]
    public async Task UnTipoDesconocido_SeRechaza()
    {
        var idCliente = await api.CrearClienteAsync();

        var resultado = await ConServicioAsync(s => s.RegistrarAsync(TitularConsentimiento.Cliente(idCliente), "biometrico", "1.0", CancellationToken.None));

        Assert.NotNull(resultado.Error);
        Assert.False(await api.ConsultarAsync(db => db.Consentimientos.AnyAsync(c => c.IdCliente == idCliente)));
    }

    [Fact]
    public async Task AceptarDosVecesLaMismaVersion_NoDuplica()
    {
        var idCliente = await api.CrearClienteAsync();
        var titular = TitularConsentimiento.Cliente(idCliente);

        await ConServicioAsync(s => s.RegistrarAsync(titular, DocumentosLegales.Marketing, Vigente(DocumentosLegales.Marketing), CancellationToken.None));
        await ConServicioAsync(s => s.RegistrarAsync(titular, DocumentosLegales.Marketing, Vigente(DocumentosLegales.Marketing), CancellationToken.None));

        Assert.Equal(1, await api.ConsultarAsync(db => db.Consentimientos.CountAsync(c => c.IdCliente == idCliente)));
    }

    // Revision ECC: dos peticiones al mismo tiempo (un doble clic) tampoco duplican.
    [Fact]
    public async Task DosAceptacionesSimultaneas_DejanUnSoloRegistro()
    {
        var idCliente = await api.CrearClienteAsync();
        var titular = TitularConsentimiento.Cliente(idCliente);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            ConServicioAsync(s => s.RegistrarAsync(titular, DocumentosLegales.Terminos, Vigente(DocumentosLegales.Terminos), CancellationToken.None))));

        Assert.All(resultados, r => Assert.Null(r.Error));
        Assert.Single(resultados.Select(r => r.IdConsentimiento).Distinct());
        Assert.Equal(1, await api.ConsultarAsync(db => db.Consentimientos.CountAsync(c => c.IdCliente == idCliente)));
    }

    [Fact]
    public async Task AlRetirarlo_QuedaLaFechaYDejaDeEstarVigente()
    {
        var idCliente = await api.CrearClienteAsync();
        var titular = TitularConsentimiento.Cliente(idCliente);
        await ConServicioAsync(s => s.RegistrarAsync(titular, DocumentosLegales.Marketing, Vigente(DocumentosLegales.Marketing), CancellationToken.None));
        Assert.NotNull(await ConServicioAsync(s => s.VigenteAsync(titular, DocumentosLegales.Marketing, CancellationToken.None)));

        var retirado = await ConServicioAsync(s => s.RevocarAsync(titular, DocumentosLegales.Marketing, CancellationToken.None));

        Assert.True(retirado);
        Assert.Null(await ConServicioAsync(s => s.VigenteAsync(titular, DocumentosLegales.Marketing, CancellationToken.None)));
        var registro = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().SingleAsync(c => c.IdCliente == idCliente));
        Assert.NotNull(registro.FechaRevocacion);
        Assert.False(await ConServicioAsync(s => s.RevocarAsync(titular, DocumentosLegales.Marketing, CancellationToken.None)));
    }

    [Fact]
    public async Task UnVisitanteSinCuenta_QuedaRegistradoPorSuEnvio()
    {
        var referencia = $"contacto:{api.Siguiente()}";

        var resultado = await ConServicioAsync(s => s.RegistrarAsync(TitularConsentimiento.Envio(referencia), DocumentosLegales.Privacidad, Vigente(DocumentosLegales.Privacidad), CancellationToken.None));

        Assert.Null(resultado.Error);
        var registro = await api.ConsultarAsync(db => db.Consentimientos.AsNoTracking().SingleAsync(c => c.ReferenciaEnvio == referencia));
        Assert.Null(registro.IdCliente);
    }

    [Fact]
    public async Task LaBase_ExigeExactamenteUnTitular()
    {
        var idCliente = await api.CrearClienteAsync();
        var idEmpleado = await api.IdAdminAsync();

        foreach (var invalido in new[]
        {
            new Consentimiento { Tipo = "privacidad", VersionDocumento = "1.0" },
            new Consentimiento { Tipo = "privacidad", VersionDocumento = "1.0", IdCliente = idCliente, IdEmpleado = idEmpleado }
        })
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => api.ConsultarAsync(async db =>
            {
                db.Consentimientos.Add(invalido);
                return await db.SaveChangesAsync();
            }));
        }
    }
}
