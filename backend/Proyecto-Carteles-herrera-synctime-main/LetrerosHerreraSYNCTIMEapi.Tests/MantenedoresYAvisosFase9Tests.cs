using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 9: avisos del taller (pedido del usuario del 14-11-2026), mantenedores, estado del local y
// disponibilidad (RFS07, RFS02, RFU16-RFU18, RFU20, RNFO03, RNFO08, RNFP10, E10-E15). Las pruebas de la
// coleccion Api corren de a una; las de avisos dejan la tabla de destinos vacia al terminar, para que las
// demas sigan usando el respaldo de la configuracion (ApiFixture.AvisosTaller).
[Collection(ApiCollection.Name)]
public sealed partial class MantenedoresYAvisosFase9Tests(ApiFixture api)
{
    private static string Version(JsonElement registro) => registro.GetProperty("rowVersion").GetString()!;

    private static string Codigo(JsonElement error) => error.GetProperty("codigo").GetString()!;

    private async Task<HttpClient> AdminAsync() => api.Cliente(await api.TokenAdminAsync());

    private static Task<HttpResponseMessage> ConVersion(HttpClient cliente, HttpMethod metodo, string url, string version, object? cuerpo = null) =>
        cliente.SendAsync(ApiFixture.ConVersion(metodo, url, version, cuerpo));

    private static async Task<HttpResponseMessage> Enviar(HttpClient cliente, HttpMethod metodo, string url, object? cuerpo = null)
    {
        using var peticion = new HttpRequestMessage(metodo, url);
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo);
        return await cliente.SendAsync(peticion);
    }

    // ── Avisos del taller ──

    private Task LimpiarDestinosAsync() => api.ConsultarAsync(async db =>
    {
        await db.DestinatariosAvisosHistorial.ExecuteDeleteAsync();
        await db.DestinatariosAvisos.Where(d => d.IdReemplaza != null).ExecuteDeleteAsync();
        return await db.DestinatariosAvisos.ExecuteDeleteAsync();
    });

    private async Task<IReadOnlyList<string>> DestinatariosAsync()
    {
        await using var ambito = api.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IDestinatariosAvisos>().ObtenerAsync(CancellationToken.None);
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_\-%]+)")]
    private static partial Regex Enlace();

    // El enlace solo viaja en el correo: se lee de la cola, como lo leeria quien lo recibe.
    private Task<string> TokenEnviadoAAsync(string correo) => api.ConsultarAsync(async db =>
    {
        var texto = await db.CorreosSalientes.AsNoTracking()
            .Where(c => c.Tipo == TiposCorreo.ConfirmacionDestinoAvisos && c.Destinatarios == correo)
            .OrderByDescending(c => c.IdCorreo).Select(c => c.CuerpoTexto).FirstAsync();
        return Uri.UnescapeDataString(Enlace().Match(texto).Groups[1].Value);
    });

    private Task<int> CorreosAsync(string tipo, string para) =>
        api.ConsultarAsync(db => db.CorreosSalientes.CountAsync(c => c.Tipo == tipo && c.Destinatarios == para));

    private static string CorreoNuevo(string prefijo) => $"{prefijo}{Guid.NewGuid():N}"[..20] + "@taller.test";

    // DELETE con la contraseña de quien quita (revision de seguridad).
    private static Task<HttpResponseMessage> Quitar(HttpClient cliente, int id, string? password = null) =>
        cliente.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/avisos-taller/destinos/{id}")
        {
            Content = JsonContent.Create(new { passwordActual = password ?? ApiFixture.AdminPassword })
        });

    private async Task<int> AgregarYConfirmarAsync(HttpClient gestion, string correo, int? idReemplaza = null)
    {
        var creado = await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo, idReemplaza, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created);
        await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token = await TokenEnviadoAAsync(correo) }).EsperarAsync(HttpStatusCode.OK);
        return creado.GetProperty("idDestinatario").GetInt32();
    }

    [Fact]
    public async Task AvisosDelTaller_SoloDuenoYAdministrador_LosVenYLosCambian()
    {
        await LimpiarDestinosAsync();
        try
        {
            var dueno = api.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno));
            await dueno.GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.OK);
            await (await AdminAsync()).GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.OK);
            await dueno.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("dueno"), passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created);

            var (_, _, tokenCliente) = await api.RegistrarClienteAsync();
            var otros = new[]
            {
                api.Cliente(await api.TokenEmpleadoAsync(Roles.Disenador)),
                api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)),
                api.Cliente(tokenCliente)
            };
            foreach (var sinPermiso in otros)
            {
                await sinPermiso.GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.Forbidden);
                await sinPermiso.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("intruso") }).EsperarAsync(HttpStatusCode.Forbidden);
                await sinPermiso.DeleteAsync("/api/v1/avisos-taller/destinos/1").EsperarAsync(HttpStatusCode.Forbidden);
                await sinPermiso.PostAsync("/api/v1/avisos-taller/prueba", null).EsperarAsync(HttpStatusCode.Forbidden);
                await sinPermiso.GetAsync("/api/v1/avisos-taller/prueba/estado?ids=1").EsperarAsync(HttpStatusCode.Forbidden);
            }
            await api.Cliente().GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.Unauthorized);
            Assert.Equal(1, await api.ConsultarAsync(db => db.DestinatariosAvisos.CountAsync()));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task UnDestinoNuevo_RecibeAvisosSoloDespuesDeConfirmar_YElEnlaceSirveUnaVez()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var correo = CorreoNuevo("nuevo");

            // Sin destinos en la base, los avisos van al respaldo de la configuracion.
            Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
            var creado = await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = correo.ToUpperInvariant(), passwordActual = ApiFixture.AdminPassword })
                .EsperarAsync(HttpStatusCode.Created);
            Assert.Equal("pendiente", creado.GetProperty("estado").GetString());
            Assert.Equal(correo, creado.GetProperty("correo").GetString());

            // Pendiente: no recibe avisos, y el enlace se guarda solo como hash.
            Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
            var estado = await gestion.GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.OK);
            Assert.True(estado.GetProperty("usaRespaldo").GetBoolean());
            var token = await TokenEnviadoAAsync(correo);
            var fila = await api.ConsultarAsync(db => db.DestinatariosAvisos.AsNoTracking().SingleAsync(d => d.Correo == correo));
            Assert.NotEqual(token, fila.TokenHash);
            Assert.True(fila.Vence > DateTimeOffset.UtcNow.AddHours(23));

            var confirmado = await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token }).EsperarAsync(HttpStatusCode.OK);
            Assert.Equal(CorreosDestinosAvisos.Enmascarar(correo), confirmado.GetProperty("correo").GetString());
            Assert.Contains("•••", confirmado.GetProperty("correo").GetString());

            // El valor guardado reemplaza a Correo:AvisosTaller.
            Assert.Equal([correo], await DestinatariosAsync());
            estado = await gestion.GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.OK);
            Assert.False(estado.GetProperty("usaRespaldo").GetBoolean());
            Assert.Contains(estado.GetProperty("historial").EnumerateArray(), h => h.GetProperty("accion").GetString() == "confirmar"
                && h.GetProperty("correo").GetString() == CorreosDestinosAvisos.Enmascarar(correo) && h.GetProperty("empleado").ValueKind == JsonValueKind.Null);

            // Un solo uso: la segunda vez responde 400 sin decir de que correo era.
            var segunda = await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token }).EsperarAsync(HttpStatusCode.BadRequest);
            Assert.DoesNotContain(correo, segunda.GetRawText());
            await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token = "inventado" }).EsperarAsync(HttpStatusCode.BadRequest);

            // Una solicitud nueva avisa a la direccion confirmada.
            await api.LimpiarColaDeCorreoAsync();
            var (_, _, tokenCliente) = await api.CrearClienteConfirmadoAsync();
            await api.Cliente(tokenCliente).PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud()).EsperarAsync(HttpStatusCode.Created);
            Assert.Equal(1, await CorreosAsync(TiposCorreo.AvisoSolicitudNueva, correo));
            Assert.Equal(0, await CorreosAsync(TiposCorreo.AvisoSolicitudNueva, ApiFixture.AvisosTaller));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }

        // Sin activos en la base, vuelve el respaldo.
        Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
    }

    [Fact]
    public async Task ElEnlaceVencido_NoConfirma()
    {
        await LimpiarDestinosAsync();
        try
        {
            var correo = CorreoNuevo("vencido");
            await (await AdminAsync()).PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created);
            var token = await TokenEnviadoAAsync(correo);

            // 25 horas despues, segun el reloj de la API: el enlace ya vencio.
            await api.ClienteConReloj(TimeSpan.FromHours(25)).PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token })
                .EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
            Assert.Equal(EstadosDestinatario.Pendiente, await api.ConsultarAsync(db => db.DestinatariosAvisos.Where(d => d.Correo == correo).Select(d => d.Estado).SingleAsync()));

            // Vencido, no bloquea: pedirlo de nuevo lo cancela (con su historial) y crea otro pendiente.
            var otraVez = await api.ClienteConReloj(TimeSpan.FromHours(25), await api.TokenAdminAsync())
                .PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created);
            Assert.Equal("pendiente", otraVez.GetProperty("estado").GetString());
            Assert.Equal(1, await api.ConsultarAsync(db => db.DestinatariosAvisos.CountAsync(d => d.Correo == correo && d.Estado == EstadosDestinatario.Cancelado)));
            Assert.True(await api.ConsultarAsync(db => db.DestinatariosAvisosHistorial.AnyAsync(h => h.Accion == "cancelar" && h.Detalle == "Venció sin confirmarse.")));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task Reemplazar_LaAnteriorSigueActivaHastaConfirmar_YRecibeElAvisoDelCambio()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var anterior = CorreoNuevo("anterior");
            var nueva = CorreoNuevo("nueva");
            var idAnterior = await AgregarYConfirmarAsync(gestion, anterior);

            await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = nueva, idReemplaza = idAnterior, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created);
            // Un segundo reemplazo del mismo destino mientras hay uno pendiente: 409.
            var otro = await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("otro"), idReemplaza = idAnterior, passwordActual = ApiFixture.AdminPassword })
                .EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("reemplazo_pendiente", Codigo(otro));
            // El mismo correo dos veces: 409 ya_existe.
            Assert.Equal("ya_existe", Codigo(await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = anterior, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Conflict)));

            // Hasta confirmar, los avisos siguen llegando a la anterior (un error de tipeo no deja al taller sin avisos).
            Assert.Equal([anterior], await DestinatariosAsync());
            var estado = await gestion.GetAsync("/api/v1/avisos-taller").EsperarAsync(HttpStatusCode.OK);
            var pendiente = estado.GetProperty("destinos").EnumerateArray().Single(d => d.GetProperty("estado").GetString() == "pendiente");
            Assert.Equal(idAnterior, pendiente.GetProperty("reemplazaA").GetProperty("idDestinatario").GetInt32());
            Assert.Equal(0, await CorreosAsync(TiposCorreo.DestinoAvisosCambiado, anterior));

            await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token = await TokenEnviadoAAsync(nueva) }).EsperarAsync(HttpStatusCode.OK);

            Assert.Equal([nueva], await DestinatariosAsync());
            Assert.Equal(1, await CorreosAsync(TiposCorreo.DestinoAvisosCambiado, anterior));
            // El aviso del cambio no muestra la direccion nueva completa.
            var texto = await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.Tipo == TiposCorreo.DestinoAvisosCambiado && c.Destinatarios == anterior)
                .Select(c => c.CuerpoTexto + c.CuerpoHtml).SingleAsync());
            Assert.DoesNotContain(nueva, texto);
            Assert.Contains(CorreosDestinosAvisos.Enmascarar(nueva), texto);
            Assert.Equal(EstadosDestinatario.Retirado, await api.ConsultarAsync(db => db.DestinatariosAvisos.Where(d => d.IdDestinatario == idAnterior).Select(d => d.Estado).SingleAsync()));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task Agregar_AvisaALosActivos_YNoSePuedeQuitarElUltimo()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var primero = CorreoNuevo("primero");
            var segundo = CorreoNuevo("segundo");
            var idPrimero = await AgregarYConfirmarAsync(gestion, primero);

            // Uno solo: no se puede quitar (decision 3).
            var ultimo = await Quitar(gestion, idPrimero).EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("ultimo_destino", Codigo(ultimo));
            Assert.Equal(Services.ServicioDestinosAvisos.MensajeUltimoDestino, ultimo.GetProperty("error").GetString());

            var idSegundo = await AgregarYConfirmarAsync(gestion, segundo);
            // Decision 4: el activo anterior se entera de que se agrego otro destino.
            Assert.Equal(1, await CorreosAsync(TiposCorreo.DestinoAvisosAgregado, primero));
            Assert.Equal([primero, segundo], await DestinatariosAsync());

            await Quitar(gestion, idPrimero).EsperarAsync(HttpStatusCode.NoContent);
            Assert.Equal([segundo], await DestinatariosAsync());
            Assert.Equal(1, await CorreosAsync(TiposCorreo.DestinoAvisosQuitado, primero));
            Assert.Equal("ultimo_destino", Codigo(await Quitar(gestion, idSegundo).EsperarAsync(HttpStatusCode.Conflict)));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task Reenviar_InvalidaElEnlaceAnterior_TieneLimite_YCancelarDejaTodoIgual()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var correo = CorreoNuevo("reenvio");
            var id = (await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created))
                .GetProperty("idDestinatario").GetInt32();
            var primerToken = await TokenEnviadoAAsync(correo);

            // Recien enviado: hay que esperar un minuto.
            await gestion.PostAsync($"/api/v1/avisos-taller/destinos/{id}/reenviar", null).EsperarAsync(HttpStatusCode.TooManyRequests);
            await api.ConsultarAsync(db => db.DestinatariosAvisosHistorial.Where(h => h.IdDestinatario == id)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.Fecha, h => h.Fecha.AddMinutes(-2))));
            await gestion.PostAsync($"/api/v1/avisos-taller/destinos/{id}/reenviar", null).EsperarAsync(HttpStatusCode.OK);
            var segundoToken = await TokenEnviadoAAsync(correo);
            Assert.NotEqual(primerToken, segundoToken);
            await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token = primerToken }).EsperarAsync(HttpStatusCode.BadRequest);

            var cancelado = await gestion.PostAsync($"/api/v1/avisos-taller/destinos/{id}/cancelar", null).EsperarAsync(HttpStatusCode.OK);
            Assert.Equal("cancelado", cancelado.GetProperty("estado").GetString());
            await api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token = segundoToken }).EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
            var acciones = await api.ConsultarAsync(db => db.DestinatariosAvisosHistorial.Where(h => h.IdDestinatario == id).Select(h => h.Accion).ToListAsync());
            Assert.Equal(["agregar", "cancelar", "reenviar"], acciones.Order().ToList());
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task LaPrueba_UsaLaCola_NoEsperaElEnvio_YTieneSuPropioLimite()
    {
        await LimpiarDestinosAsync();
        await api.LimpiarColaDeCorreoAsync();
        try
        {
            var gestion = await AdminAsync();
            var respuesta = await gestion.PostAsync("/api/v1/avisos-taller/prueba", null).EsperarAsync(HttpStatusCode.Accepted);
            Assert.True(respuesta.GetProperty("usaRespaldo").GetBoolean());
            Assert.Equal([ApiFixture.AvisosTaller], respuesta.GetProperty("destinatarios").EnumerateArray().Select(d => d.GetString()).ToList());
            var id = respuesta.GetProperty("idsCorreos")[0].GetInt64();

            // La peticion no espero al servidor de correo: el aviso de ejemplo quedo pendiente en la cola.
            var fila = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.IdCorreo == id));
            Assert.Equal(EstadosCorreo.Pendiente, fila.Estado);
            Assert.Equal(TiposCorreo.PruebaAvisos, fila.Tipo);
            Assert.Equal(AvisoSolicitudNueva.AsuntoPrueba, fila.Asunto);
            Assert.Contains("Esto es una prueba", fila.CuerpoTexto);
            var estado = await gestion.GetAsync($"/api/v1/avisos-taller/prueba/estado?ids={id}").EsperarAsync(HttpStatusCode.OK);
            Assert.Equal("pendiente", estado[0].GetProperty("estado").GetString());

            // Cuando el trabajador de la cola lo envia, el estado lo dice.
            await using (var ambito = api.Services.CreateAsyncScope())
                await ambito.ServiceProvider.GetRequiredService<TrabajadorColaCorreo>().ProcesarLoteAsync(CancellationToken.None);
            estado = await gestion.GetAsync($"/api/v1/avisos-taller/prueba/estado?ids={id}").EsperarAsync(HttpStatusCode.OK);
            Assert.Equal("enviado", estado[0].GetProperty("estado").GetString());

            // El estado solo responde por correos de prueba.
            var otro = await api.ConsultarAsync(async db =>
            {
                var c = new CorreoSaliente { Tipo = TiposCorreo.ConsultaContacto, Destinatarios = "otro@taller.test", Asunto = "x", CuerpoHtml = "x", CuerpoTexto = "x",
                    ProximoIntento = DateTimeOffset.UtcNow, FechaCreacion = DateTimeOffset.UtcNow };
                db.CorreosSalientes.Add(c);
                await db.SaveChangesAsync();
                return c.IdCorreo;
            });
            Assert.Equal(0, (await gestion.GetAsync($"/api/v1/avisos-taller/prueba/estado?ids={otro}").EsperarAsync(HttpStatusCode.OK)).GetArrayLength());

            // 3 pruebas cada 10 minutos: la cuarta responde 429.
            await gestion.PostAsync("/api/v1/avisos-taller/prueba", null).EsperarAsync(HttpStatusCode.Accepted);
            await gestion.PostAsync("/api/v1/avisos-taller/prueba", null).EsperarAsync(HttpStatusCode.Accepted);
            await gestion.PostAsync("/api/v1/avisos-taller/prueba", null).EsperarAsync(HttpStatusCode.TooManyRequests);
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task LaPrueba_NoGastaElTopeDeAvisosPorHora()
    {
        await LimpiarDestinosAsync();
        await api.LimpiarColaDeCorreoAsync();
        await using var conTope = api.ConVariable("Correo__TopeAvisosPorHora", "1");
        try
        {
            await conTope.Cliente(await api.TokenAdminAsync()).PostAsync("/api/v1/avisos-taller/prueba", null).EsperarAsync(HttpStatusCode.Accepted);

            // Con tope 1, la solicitud igual recibe su aviso propio: la prueba no conto.
            var (_, _, tokenCliente) = await api.CrearClienteConfirmadoAsync();
            await conTope.Cliente(tokenCliente).PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud()).EsperarAsync(HttpStatusCode.Created);
            Assert.Equal(1, await CorreosAsync(TiposCorreo.AvisoSolicitudNueva, ApiFixture.AvisosTaller));
            Assert.Equal(0, await CorreosAsync(TiposCorreo.AvisoTopeSolicitudes, ApiFixture.AvisosTaller));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    // ── Mantenedores: permisos y version ──

    public static TheoryData<string, object> Altas => new()
    {
        { "/api/v1/categorias", new { nombre = "Categoría de permisos" } },
        { "/api/v1/productos", new { idCategoria = 1, nombre = "Tipo de permisos" } },
        { "/api/v1/materiales", new { nombre = "Material de permisos", unidadMedida = "m2", costoUnitarioAprox = 1000 } },
        { "/api/v1/catalogo", new { idProducto = 1, titulo = "Trabajo de permisos", descripcion = "x", visible = false } }
    };

    [Theory]
    [MemberData(nameof(Altas))]
    public async Task CadaMantenedor_SoloLoAdministraGestion(string ruta, object cuerpo)
    {
        var (_, _, tokenCliente) = await api.RegistrarClienteAsync();
        foreach (var token in new[] { await api.TokenEmpleadoAsync(Roles.Operario), await api.TokenEmpleadoAsync(Roles.Disenador), tokenCliente })
        {
            var sinPermiso = api.Cliente(token);
            await sinPermiso.PostAsJsonAsync(ruta, cuerpo).EsperarAsync(HttpStatusCode.Forbidden);
            await sinPermiso.PutAsJsonAsync($"{ruta}/1", cuerpo).EsperarAsync(HttpStatusCode.Forbidden);
            await sinPermiso.PatchAsJsonAsync(ruta.EndsWith("catalogo") ? $"{ruta}/1/visible" : $"{ruta}/1/activo", new { activo = false, visible = false })
                .EsperarAsync(HttpStatusCode.Forbidden);
        }
        await api.Cliente().PostAsJsonAsync(ruta, cuerpo).EsperarAsync(HttpStatusCode.Unauthorized);

        // El dueño y el administrador si (nombres distintos para no chocar con la regla de nombre unico).
        var n = api.Siguiente();
        var conNombre = JsonSerializer.SerializeToNode(cuerpo)!.AsObject();
        foreach (var campo in new[] { "nombre", "titulo" })
            if (conNombre.ContainsKey(campo))
                conNombre[campo] = $"{conNombre[campo]} {n}";
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Dueno)).PostAsJsonAsync(ruta, conNombre).EsperarAsync(HttpStatusCode.Created);
        foreach (var campo in new[] { "nombre", "titulo" })
            if (conNombre.ContainsKey(campo))
                conNombre[campo] = $"{conNombre[campo]} b";
        await (await AdminAsync()).PostAsJsonAsync(ruta, conNombre).EsperarAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ElPersonal_LoAdministraGestion_YTodosVenLaListaCorta()
    {
        var operario = api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario));
        await operario.GetAsync("/api/v1/empleados").EsperarAsync(HttpStatusCode.Forbidden);
        await operario.GetAsync("/api/v1/empleados/1").EsperarAsync(HttpStatusCode.Forbidden);
        await operario.PostAsJsonAsync("/api/v1/empleados", new { }).EsperarAsync(HttpStatusCode.Forbidden);
        var corta = await operario.GetAsync("/api/v1/empleados/activos").EsperarAsync(HttpStatusCode.OK);
        Assert.All(corta.EnumerateArray(), e => Assert.False(e.TryGetProperty("email", out _)));

        var lista = await (await AdminAsync()).GetAsync("/api/v1/empleados?tamano=100").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain("password", lista.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.True(lista.GetProperty("total").GetInt32() > 0);
        // La ruta antigua se retiro.
        await (await AdminAsync()).PostAsJsonAsync("/api/v1/auth/empleados", new { }).EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditarSinVersion_Responde400_YConUnaVieja_409()
    {
        var admin = await AdminAsync();
        var n = api.Siguiente();
        var categoria = await admin.PostAsJsonAsync("/api/v1/categorias", new { nombre = $"Versionada {n}" }).EsperarAsync(HttpStatusCode.Created);
        var id = categoria.GetProperty("idCategoria").GetInt32();
        var vieja = Version(categoria);

        Assert.Equal("sin_version", Codigo(await admin.PutAsJsonAsync($"/api/v1/categorias/{id}", new { nombre = $"Sin version {n}" }).EsperarAsync(HttpStatusCode.BadRequest)));
        var editada = await ConVersion(admin, HttpMethod.Put, $"/api/v1/categorias/{id}", vieja, new { nombre = $"Editada {n}" }).EsperarAsync(HttpStatusCode.OK);
        Assert.NotEqual(vieja, Version(editada));
        Assert.Equal("version_obsoleta", Codigo(await ConVersion(admin, HttpMethod.Put, $"/api/v1/categorias/{id}", vieja, new { nombre = $"Pisada {n}" })
            .EsperarAsync(HttpStatusCode.Conflict)));
        Assert.Equal("version_obsoleta", Codigo(await ConVersion(admin, HttpMethod.Patch, $"/api/v1/categorias/{id}/activo", vieja, new { activo = false })
            .EsperarAsync(HttpStatusCode.Conflict)));

        // Igual en materiales, productos, empleados, la vitrina y la ficha del cliente (RNFP10).
        var material = await admin.PostAsJsonAsync("/api/v1/materiales", new { nombre = $"Material {n}", unidadMedida = "m2", costoUnitarioAprox = 500 }).EsperarAsync(HttpStatusCode.Created);
        await VersionObligatoriaAsync(admin, $"/api/v1/materiales/{material.GetProperty("idMaterial").GetInt32()}", Version(material),
            new { nombre = $"Material {n} b", unidadMedida = "m2", costoUnitarioAprox = 600 });
        var producto = await admin.PostAsJsonAsync("/api/v1/productos", new { idCategoria = 1, nombre = $"Producto {n}", precioBaseRef = 1000 }).EsperarAsync(HttpStatusCode.Created);
        await VersionObligatoriaAsync(admin, $"/api/v1/productos/{producto.GetProperty("idProducto").GetInt32()}", Version(producto),
            new { idCategoria = 1, nombre = $"Producto {n} b" });
        var (idEmpleado, email, _) = await api.CrearEmpleadoAsync(Roles.Operario);
        var empleado = await admin.GetAsync($"/api/v1/empleados/{idEmpleado}").EsperarAsync(HttpStatusCode.OK);
        await VersionObligatoriaAsync(admin, $"/api/v1/empleados/{idEmpleado}", Version(empleado), new
        {
            rut = empleado.GetProperty("rut").GetString(), nombre = "Otro", apellido = "Nombre", telefono = api.Telefono(), email, rol = Roles.Disenador, passwordActual = ApiFixture.AdminPassword
        });
        var publicacion = await admin.PostAsJsonAsync("/api/v1/catalogo", new { idProducto = 1, titulo = $"Trabajo {n}", descripcion = "x", visible = false })
            .EsperarAsync(HttpStatusCode.Created);
        await VersionObligatoriaAsync(admin, $"/api/v1/catalogo/{publicacion.GetProperty("idCatalogo").GetInt32()}", Version(publicacion),
            new { idProducto = 1, titulo = $"Trabajo {n} b", descripcion = "x", visible = false });
        var idCliente = await api.CrearClienteAsync();
        var cliente = await admin.GetAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.OK);
        await VersionObligatoriaAsync(admin, $"/api/v1/clientes/{idCliente}", Version(cliente), new
        {
            nombre = "Cli", apellido = "Editado", telefono = cliente.GetProperty("telefono").GetString(), activo = true
        });
    }

    // La ficha del cliente (RNFP10) responde igual que los mantenedores: ETag al leer y al guardar, 409 version_obsoleta.
    [Fact]
    public async Task LaFichaDelCliente_UsaETagYVersionComoLosMantenedores()
    {
        var admin = await AdminAsync();
        var idCliente = await api.CrearClienteAsync();
        using var lectura = await admin.GetAsync($"/api/v1/clientes/{idCliente}");
        var etag = lectura.Headers.ETag!.Tag;
        var cliente = await lectura.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"\"{Version(cliente)}\"", etag);
        var cuerpo = new { nombre = "Cli", apellido = "Con ETag", telefono = cliente.GetProperty("telefono").GetString(), activo = true };

        using var guardado = await admin.SendAsync(ApiFixture.ConVersion(HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(cliente), cuerpo));
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);
        var nuevo = await guardado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"\"{Version(nuevo)}\"", guardado.Headers.ETag!.Tag);
        var obsoleta = await ConVersion(admin, HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(cliente), cuerpo).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("version_obsoleta", Codigo(obsoleta));
        Assert.Equal("sin_version", Codigo(await admin.PutAsJsonAsync($"/api/v1/clientes/{idCliente}", cuerpo).EsperarAsync(HttpStatusCode.BadRequest)));
    }

    // Sin version: 400 sin_version. Con la vigente: 200 y version nueva. Con la anterior: 409 version_obsoleta.
    private static async Task VersionObligatoriaAsync(HttpClient admin, string url, string version, object cuerpo)
    {
        Assert.Equal("sin_version", Codigo(await admin.PutAsJsonAsync(url, cuerpo).EsperarAsync(HttpStatusCode.BadRequest)));
        var editado = await ConVersion(admin, HttpMethod.Put, url, version, cuerpo).EsperarAsync(HttpStatusCode.OK);
        Assert.NotEqual(version, Version(editado));
        Assert.Equal("version_obsoleta", Codigo(await ConVersion(admin, HttpMethod.Put, url, version, cuerpo).EsperarAsync(HttpStatusCode.Conflict)));
    }

    [Fact]
    public async Task UnNombreRepetido_Responde409YaExiste_YDesactivarOcultaDeLaPortada()
    {
        var admin = await AdminAsync();
        var nombre = $"Repetida {api.Siguiente()}";
        var creada = await admin.PostAsJsonAsync("/api/v1/categorias", new { nombre }).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("ya_existe", Codigo(await admin.PostAsJsonAsync("/api/v1/categorias", new { nombre = nombre.ToUpperInvariant() }).EsperarAsync(HttpStatusCode.Conflict)));

        var id = creada.GetProperty("idCategoria").GetInt32();
        var desactivada = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/categorias/{id}/activo", Version(creada), new { activo = false }).EsperarAsync(HttpStatusCode.OK);
        Assert.False(desactivada.GetProperty("activo").GetBoolean());
        await api.Cliente().GetAsync($"/api/v1/categorias/{id}").EsperarAsync(HttpStatusCode.NotFound);
        // No se crea un tipo de letrero en una categoria desactivada.
        await admin.PostAsJsonAsync("/api/v1/productos", new { idCategoria = id, nombre = $"Producto {api.Siguiente()}" }).EsperarAsync(HttpStatusCode.BadRequest);
    }

    // ── Vitrina: logo sin autorizacion ──

    [Fact]
    public async Task HacerVisibleUnaPublicacionConLogoSinAutorizacion_Responde409_YLaBaseTambienLoImpide()
    {
        var admin = await AdminAsync();
        var creada = await admin.PostAsJsonAsync("/api/v1/catalogo", new
        {
            idProducto = 1, titulo = $"Con logo {api.Siguiente()}", descripcion = "x", visible = false, incluyeLogoCliente = true
        }).EsperarAsync(HttpStatusCode.Created);
        var id = creada.GetProperty("idCatalogo").GetInt32();

        var rechazo = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/catalogo/{id}/visible", Version(creada), new { visible = true }).EsperarAsync(HttpStatusCode.Conflict);
        Assert.Equal("logo_sin_autorizacion", Codigo(rechazo));
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => api.ConsultarAsync(async db =>
        {
            var fila = await db.Catalogos.SingleAsync(c => c.IdCatalogo == id);
            fila.Visible = true;
            return await db.SaveChangesAsync();
        }));

        // Con la autorizacion registrada (y su version nueva) si se puede mostrar.
        var autorizada = await ConVersion(admin, HttpMethod.Put, $"/api/v1/catalogo/{id}/autorizacion-logo", Version(creada), new { autorizado = true })
            .EsperarAsync(HttpStatusCode.OK);
        var visible = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/catalogo/{id}/visible", Version(autorizada), new { visible = true }).EsperarAsync(HttpStatusCode.OK);
        Assert.True(visible.GetProperty("visible").GetBoolean());
    }

    // ── Personal: desactivar, reglas propio y ultimo dueño ──

    private async Task<int> DarRostroAsync(int idEmpleado) => await api.ConsultarAsync(async db =>
    {
        var rostro = new RostroEmpleado
        {
            IdEmpleado = idEmpleado, DescriptorCifrado = [1, 2, 3], FechaConsentimiento = DateTimeOffset.UtcNow, VersionConsentimiento = "1.0",
            TokenEquipoHash = new byte[32], FechaAutorizacionEquipo = DateTimeOffset.UtcNow,
            // Fase 10: las tres muestras tambien se borran al desactivar.
            DescriptorFrente = [4], DescriptorIzquierda = [5], DescriptorDerecha = [6], FormatoMuestras = RostroEmpleado.FormatoTresMuestras
        };
        db.RostrosEmpleado.Add(rostro);
        await db.SaveChangesAsync();
        return rostro.IdRostro;
    });

    private Task<int> SesionesVigentesAsync(int idEmpleado) =>
        api.ConsultarAsync(db => db.TokensRenovacion.CountAsync(t => t.IdEmpleado == idEmpleado && t.FechaRevocacion == null));

    [Fact]
    public async Task DesactivarAUnEmpleado_BorraSuRostroYCierraSusSesionesEnElMismoPaso()
    {
        var admin = await AdminAsync();
        var (id, email, _) = await api.CrearEmpleadoAsync(Roles.Operario);
        var idRostro = await DarRostroAsync(id);
        Assert.True(await SesionesVigentesAsync(id) > 0);
        var ficha = await admin.GetAsync($"/api/v1/empleados/{id}").EsperarAsync(HttpStatusCode.OK);

        var desactivado = await ConVersion(admin, HttpMethod.Patch, $"/api/v1/empleados/{id}/activo", Version(ficha), new { activo = false }).EsperarAsync(HttpStatusCode.OK);

        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        var rostro = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().SingleAsync(r => r.IdRostro == idRostro));
        Assert.Null(rostro.DescriptorCifrado);
        Assert.Null(rostro.DescriptorFrente);
        Assert.Null(rostro.DescriptorIzquierda);
        Assert.Null(rostro.DescriptorDerecha);
        Assert.Null(rostro.TokenEquipoHash);
        Assert.NotNull(rostro.FechaRevocacion);
        Assert.Equal(0, await SesionesVigentesAsync(id));
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(id, (await admin.GetAsync("/api/v1/empleados/activos").EsperarAsync(HttpStatusCode.OK)).EnumerateArray()
            .Select(e => e.GetProperty("idEmpleado").GetInt32()));
    }

    [Fact]
    public async Task SiCerrarLasSesionesFalla_ElEmpleadoNoQuedaDesactivadoNiPierdeSuRostro()
    {
        var (id, _, _) = await api.CrearEmpleadoAsync(Roles.Operario);
        var idRostro = await DarRostroAsync(id);
        var conFalla = api.ClienteConInterceptor(new FallaAlCerrarSesiones(), await api.TokenAdminAsync());
        var ficha = await (await AdminAsync()).GetAsync($"/api/v1/empleados/{id}").EsperarAsync(HttpStatusCode.OK);

        await ConVersion(conFalla, HttpMethod.Patch, $"/api/v1/empleados/{id}/activo", Version(ficha), new { activo = false })
            .EsperarAsync(HttpStatusCode.InternalServerError);

        Assert.True(await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == id).Select(e => e.Activo).SingleAsync()));
        var rostro = await api.ConsultarAsync(db => db.RostrosEmpleado.AsNoTracking().SingleAsync(r => r.IdRostro == idRostro));
        Assert.NotNull(rostro.DescriptorCifrado);
        Assert.Null(rostro.FechaRevocacion);
        Assert.True(await SesionesVigentesAsync(id) > 0);
    }

    private sealed class FallaAlCerrarSesiones : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE", StringComparison.Ordinal) && command.CommandText.Contains("[tokens_renovacion]", StringComparison.Ordinal))
                throw new InvalidOperationException("Falla provocada por la prueba.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task NadieSeDesactivaASiMismo_YSiempreQuedaUnDuenoActivo()
    {
        var admin = await AdminAsync();
        var idAdmin = await api.IdAdminAsync();
        var yo = await admin.GetAsync($"/api/v1/empleados/{idAdmin}").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("propio", Codigo(await ConVersion(admin, HttpMethod.Patch, $"/api/v1/empleados/{idAdmin}/activo", Version(yo), new { activo = false })
            .EsperarAsync(HttpStatusCode.Conflict)));

        // Se deja un solo dueño activo, solo para esta prueba.
        var (idDueno, _, _) = await api.CrearEmpleadoAsync(Roles.Dueno);
        var otros = await api.ConsultarAsync(db => db.Empleados.Where(e => e.Activo && e.Rol == Roles.Dueno && e.IdEmpleado != idDueno).Select(e => e.IdEmpleado).ToListAsync());
        await api.ConsultarAsync(db => db.Empleados.Where(e => otros.Contains(e.IdEmpleado)).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, false)));
        try
        {
            var dueno = await admin.GetAsync($"/api/v1/empleados/{idDueno}").EsperarAsync(HttpStatusCode.OK);
            Assert.Equal("ultimo_dueno", Codigo(await ConVersion(admin, HttpMethod.Patch, $"/api/v1/empleados/{idDueno}/activo", Version(dueno), new { activo = false })
                .EsperarAsync(HttpStatusCode.Conflict)));
            Assert.Equal("ultimo_dueno", Codigo(await ConVersion(admin, HttpMethod.Put, $"/api/v1/empleados/{idDueno}", Version(dueno), new
            {
                rut = dueno.GetProperty("rut").GetString(), nombre = "Emp", apellido = "Prueba", telefono = dueno.GetProperty("telefono").GetString(),
                email = dueno.GetProperty("email").GetString(), rol = Roles.Administrador, passwordActual = ApiFixture.AdminPassword
            }).EsperarAsync(HttpStatusCode.Conflict)));
            Assert.True(await api.ConsultarAsync(db => db.Empleados.Where(e => e.IdEmpleado == idDueno).Select(e => e.Activo && e.Rol == Roles.Dueno).SingleAsync()));

            // Con otro dueño activo, ya se puede.
            var (idOtro, _, _) = await api.CrearEmpleadoAsync(Roles.Dueno);
            await ConVersion(admin, HttpMethod.Patch, $"/api/v1/empleados/{idDueno}/activo", Version(dueno), new { activo = false }).EsperarAsync(HttpStatusCode.OK);
            otros.Add(idDueno);
            Assert.NotEqual(0, idOtro);
        }
        finally
        {
            await api.ConsultarAsync(db => db.Empleados.Where(e => otros.Contains(e.IdEmpleado)).ExecuteUpdateAsync(s => s.SetProperty(e => e.Activo, true)));
        }
    }

    [Fact]
    public async Task ElAltaDePersonal_ValidaRutRolYDuplicados()
    {
        var admin = await AdminAsync();
        object Alta(string rut, string email, string rol) => new
        {
            rut, nombre = "Ana", apellido = "Pérez", telefono = api.Telefono(), email, rol, password = ApiFixture.AdminPassword, passwordActual = ApiFixture.AdminPassword
        };
        var email = $"alta{api.Siguiente()}@test.local";
        var invalido = await admin.PostAsJsonAsync("/api/v1/empleados", Alta("12.345.678-9", email, Roles.Operario)).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(invalido.GetProperty("errores").TryGetProperty("rut", out _));
        var rut = api.Rut();
        await admin.PostAsJsonAsync("/api/v1/empleados", Alta(rut, email, Roles.Operario)).EsperarAsync(HttpStatusCode.Created);
        Assert.Equal("ya_existe", Codigo(await admin.PostAsJsonAsync("/api/v1/empleados", Alta(rut, $"otro{api.Siguiente()}@test.local", Roles.Operario))
            .EsperarAsync(HttpStatusCode.Conflict)));
        Assert.Equal("ya_existe", Codigo(await admin.PostAsJsonAsync("/api/v1/empleados", Alta(api.Rut(), email.ToUpperInvariant(), Roles.Operario))
            .EsperarAsync(HttpStatusCode.Conflict)));
    }

    // ── Cartel, horario y disponibilidad ──

    [Theory]
    [InlineData(Roles.Operario)]
    [InlineData(Roles.Disenador)]
    public async Task ElCartel_LoDaVueltaTodoElPersonal_YLaPortadaLoRefleja(string rol)
    {
        var personal = api.Cliente(await api.TokenEmpleadoAsync(rol));
        try
        {
            var cartel = await personal.PutAsJsonAsync("/api/v1/cartel-digital", new { abiertoEnLocal = false, mensajeEstado = $"Salimos a instalar ({rol})", tiempoEstimadoRegreso = "Volvemos a las 16:00" })
                .EsperarAsync(HttpStatusCode.OK);
            Assert.False(cartel.GetProperty("abiertoEnLocal").GetBoolean());
            var estado = await api.Cliente().GetAsync("/api/v1/local/estado").EsperarAsync(HttpStatusCode.OK);
            Assert.Equal($"Salimos a instalar ({rol})", estado.GetProperty("cartel").GetProperty("mensajeEstado").GetString());
            Assert.DoesNotContain("mpleado", estado.GetRawText());

            await personal.PutAsJsonAsync("/api/v1/cartel-digital", new { abiertoEnLocal = true, mensajeEstado = new string('x', 201) }).EsperarAsync(HttpStatusCode.BadRequest);
            var (_, _, tokenCliente) = await api.RegistrarClienteAsync();
            await api.Cliente(tokenCliente).PutAsJsonAsync("/api/v1/cartel-digital", new { abiertoEnLocal = true, mensajeEstado = "x" }).EsperarAsync(HttpStatusCode.Forbidden);
        }
        finally
        {
            await personal.PutAsJsonAsync("/api/v1/cartel-digital", new { abiertoEnLocal = true, mensajeEstado = "Local abierto y atendiendo" }).EsperarAsync(HttpStatusCode.OK);
        }
    }

    private static object Semana(string cierreLunes = "18:30") => new
    {
        dias = new object[]
        {
            new { diaSemana = 1, abierto = true, horaInicio = "09:00", horaFin = cierreLunes },
            new { diaSemana = 2, abierto = true, horaInicio = "09:00", horaFin = "18:30" },
            new { diaSemana = 3, abierto = true, horaInicio = "09:00", horaFin = "18:30" },
            new { diaSemana = 4, abierto = true, horaInicio = "09:00", horaFin = "18:30" },
            new { diaSemana = 5, abierto = true, horaInicio = "09:00", horaFin = "18:30" },
            new { diaSemana = 6, abierto = true, horaInicio = "09:00", horaFin = "14:00" },
            new { diaSemana = 7, abierto = false, horaInicio = (string?)null, horaFin = (string?)null }
        }
    };

    [Fact]
    public async Task ElHorario_LoCambiaGestion_ConCierrePosteriorALaApertura()
    {
        await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).PutAsJsonAsync("/api/v1/horarios", Semana()).EsperarAsync(HttpStatusCode.Forbidden);
        var admin = await AdminAsync();
        var invalido = await admin.PutAsJsonAsync("/api/v1/horarios", Semana(cierreLunes: "08:00")).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(invalido.GetProperty("errores").TryGetProperty("1.horaFin", out _));

        var guardado = await admin.PutAsJsonAsync("/api/v1/horarios", Semana()).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(7, guardado.GetArrayLength());
        Assert.Equal("09:00", guardado[0].GetProperty("horaInicio").GetString());
        var fila = await api.ConsultarAsync(db => db.HorariosAtencion.AsNoTracking().SingleAsync(h => h.DiaSemana == 1));
        Assert.Equal(await api.IdAdminAsync(), fila.IdEmpleadoModifica);
        Assert.NotNull(fila.FechaModificacion);
        var publico = await api.Cliente().GetAsync("/api/v1/horarios").EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain("mpleado", publico.GetRawText());
    }

    [Fact]
    public async Task LaDisponibilidad_SeValida_YLaPortadaMuestraLaUnionSinNombres()
    {
        await (await AdminAsync()).PutAsJsonAsync("/api/v1/horarios", Semana()).EsperarAsync(HttpStatusCode.OK);
        var (idUno, _, tokenUno) = await api.CrearEmpleadoAsync(Roles.Operario);
        var (idDos, _, tokenDos) = await api.CrearEmpleadoAsync(Roles.Disenador);
        await api.ConsultarAsync(db => db.DisponibilidadEmpleados.ExecuteDeleteAsync());
        var uno = api.Cliente(tokenUno);

        object Franjas(params (int Dia, string Desde, string Hasta)[] f) => new { franjas = f.Select(x => new { diaSemana = x.Dia, horaInicio = x.Desde, horaFin = x.Hasta }) };
        await uno.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franjas((1, "13:00", "10:00"))).EsperarAsync(HttpStatusCode.BadRequest);
        await uno.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franjas((1, "09:00", "12:00"), (1, "11:00", "13:00"))).EsperarAsync(HttpStatusCode.BadRequest);
        await uno.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franjas((1, "08:00", "12:00"))).EsperarAsync(HttpStatusCode.BadRequest);
        await uno.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franjas((7, "10:00", "12:00"))).EsperarAsync(HttpStatusCode.BadRequest);

        var mias = await uno.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franjas((1, "09:00", "13:00"), (1, "13:00", "14:00"), (2, "10:00", "12:00")))
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(3, mias.GetProperty("franjas").GetArrayLength());
        await api.Cliente(tokenDos).PutAsJsonAsync("/api/v1/mi-disponibilidad", Franjas((1, "12:00", "17:00"))).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(1, (await api.Cliente(tokenDos).GetAsync("/api/v1/mi-disponibilidad").EsperarAsync(HttpStatusCode.OK)).GetProperty("franjas").GetArrayLength());

        var atencion = await api.Cliente().GetAsync("/api/v1/local/atencion").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(7, atencion.GetArrayLength());
        var lunes = atencion[0].GetProperty("franjas");
        Assert.Equal(1, lunes.GetArrayLength());
        Assert.Equal("09:00", lunes[0].GetProperty("desde").GetString());
        Assert.Equal("17:00", lunes[0].GetProperty("hasta").GetString());
        var texto = atencion.GetRawText();
        Assert.DoesNotContain("idEmpleado", texto);
        Assert.DoesNotContain("\"nombre\"", texto);
        Assert.DoesNotContain("Emp", texto);

        // Gestion ve a cada persona; el resto del personal no.
        await uno.GetAsync("/api/v1/disponibilidad").EsperarAsync(HttpStatusCode.Forbidden);
        var equipo = await (await AdminAsync()).GetAsync("/api/v1/disponibilidad").EsperarAsync(HttpStatusCode.OK);
        Assert.Contains(equipo.EnumerateArray(), p => p.GetProperty("idEmpleado").GetInt32() == idUno);
        Assert.Contains(equipo.EnumerateArray(), p => p.GetProperty("idEmpleado").GetInt32() == idDos);
        await api.ConsultarAsync(db => db.DisponibilidadEmpleados.ExecuteDeleteAsync());
    }

    // ── Revision de seguridad de la fase 9 ──

    [Fact]
    public async Task AgregarOQuitarUnDestino_PideLaContrasenaDeQuienLoHace()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var sinPassword = await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("sinpass") }).EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal("datos_invalidos", Codigo(sinPassword));
            Assert.True(sinPassword.GetProperty("errores").TryGetProperty("passwordActual", out _));
            await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("malpass"), passwordActual = "no-es-mi-password" })
                .EsperarAsync(HttpStatusCode.BadRequest);
            Assert.Equal(0, await api.ConsultarAsync(db => db.DestinatariosAvisos.CountAsync()));

            var uno = await AgregarYConfirmarAsync(gestion, CorreoNuevo("uno"));
            await AgregarYConfirmarAsync(gestion, CorreoNuevo("dos"));
            var mal = await Quitar(gestion, uno, "no-es-mi-password").EsperarAsync(HttpStatusCode.BadRequest);
            Assert.True(mal.GetProperty("errores").TryGetProperty("passwordActual", out _));
            Assert.Equal(2, await api.ConsultarAsync(db => db.DestinatariosAvisos.CountAsync(d => d.Estado == EstadosDestinatario.Activo)));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task ComoMuchoTresPendientes_YElLimitePorDireccionNoSeSaltaCancelando()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            async Task<int> PedirAsync(string correo) =>
                (await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo, passwordActual = ApiFixture.AdminPassword })
                    .EsperarAsync(HttpStatusCode.Created)).GetProperty("idDestinatario").GetInt32();

            var repetido = CorreoNuevo("repetido");
            var id = await PedirAsync(repetido);
            await gestion.PostAsync($"/api/v1/avisos-taller/destinos/{id}/cancelar", null).EsperarAsync(HttpStatusCode.OK);
            // Pedir, cancelar y volver a pedir no salta la espera de un minuto entre correos a la misma direccion.
            await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = repetido, passwordActual = ApiFixture.AdminPassword })
                .EsperarAsync(HttpStatusCode.TooManyRequests);

            await PedirAsync(CorreoNuevo("p1"));
            await PedirAsync(CorreoNuevo("p2"));
            await PedirAsync(CorreoNuevo("p3"));
            var cuarto = await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("p4"), passwordActual = ApiFixture.AdminPassword })
                .EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("demasiados_pendientes", Codigo(cuarto));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task ElPrimerDestinoConfirmado_AvisaALasDireccionesDelRespaldo()
    {
        await LimpiarDestinosAsync();
        await api.LimpiarColaDeCorreoAsync();
        try
        {
            var nuevo = CorreoNuevo("primero");
            await AgregarYConfirmarAsync(await AdminAsync(), nuevo);

            Assert.Equal(1, await CorreosAsync(TiposCorreo.DestinoAvisosAgregado, ApiFixture.AvisosTaller));
            Assert.Equal(0, await CorreosAsync(TiposCorreo.DestinoAvisosAgregado, nuevo));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task NoSeQuitaUnDestinoConUnReemplazoPendiente()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var idA = await AgregarYConfirmarAsync(gestion, CorreoNuevo("a"));
            await AgregarYConfirmarAsync(gestion, CorreoNuevo("b"));
            await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = CorreoNuevo("c"), idReemplaza = idA, passwordActual = ApiFixture.AdminPassword })
                .EsperarAsync(HttpStatusCode.Created);

            var rechazo = await Quitar(gestion, idA).EsperarAsync(HttpStatusCode.Conflict);
            Assert.Equal("reemplazo_pendiente", Codigo(rechazo));
            Assert.Equal(EstadosDestinatario.Activo, await api.ConsultarAsync(db => db.DestinatariosAvisos.Where(d => d.IdDestinatario == idA).Select(d => d.Estado).SingleAsync()));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task DosConfirmacionesALaVez_SoloUnaSirve()
    {
        await LimpiarDestinosAsync();
        try
        {
            var correo = CorreoNuevo("doble");
            await (await AdminAsync()).PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo, passwordActual = ApiFixture.AdminPassword }).EsperarAsync(HttpStatusCode.Created);
            var token = await TokenEnviadoAAsync(correo);

            var respuestas = await Task.WhenAll(
                api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token }),
                api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token }));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.BadRequest], respuestas.Select(r => r.StatusCode).Order().ToList());
            Assert.Equal([correo], await DestinatariosAsync());
            Assert.Equal(1, await api.ConsultarAsync(db => db.DestinatariosAvisosHistorial.CountAsync(h => h.Accion == "confirmar")));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task ConfirmarYQuitarALaVez_SiempreQuedaUnEstadoValido()
    {
        await LimpiarDestinosAsync();
        try
        {
            var gestion = await AdminAsync();
            var (a, b, c) = (CorreoNuevo("ra"), CorreoNuevo("rb"), CorreoNuevo("rc"));
            var idA = await AgregarYConfirmarAsync(gestion, a);
            await AgregarYConfirmarAsync(gestion, b);
            await gestion.PostAsJsonAsync("/api/v1/avisos-taller/destinos", new { correo = c, idReemplaza = idA, passwordActual = ApiFixture.AdminPassword })
                .EsperarAsync(HttpStatusCode.Created);
            var token = await TokenEnviadoAAsync(c);

            var respuestas = await Task.WhenAll(
                api.Cliente().PostAsJsonAsync("/api/v1/avisos-taller/confirmar", new { token }),
                Quitar(gestion, idA));

            Assert.Equal(HttpStatusCode.OK, respuestas[0].StatusCode);
            // Si el quitar llego antes, el reemplazo seguia pendiente; si llego despues, A ya estaba retirado.
            Assert.Equal(HttpStatusCode.Conflict, respuestas[1].StatusCode);
            Assert.Equal(new[] { b, c }.Order().ToList(), (await DestinatariosAsync()).Order().ToList());
            Assert.Equal(EstadosDestinatario.Retirado, await api.ConsultarAsync(db => db.DestinatariosAvisos.Where(d => d.IdDestinatario == idA).Select(d => d.Estado).SingleAsync()));
        }
        finally
        {
            await LimpiarDestinosAsync();
        }
    }

    [Fact]
    public async Task SinLaTablaDeDestinos_LosAvisosVanAlRespaldo_YSeRecuerda()
    {
        await using var ambito = api.Services.CreateAsyncScope();
        var tablaAusente = ambito.ServiceProvider.GetRequiredService<Datos.Correo.TablaDestinosAusente>();
        await api.ConsultarAsync(db => db.Database.ExecuteSqlRawAsync("EXEC sp_rename 'dbo.destinatarios_avisos', 'destinatarios_avisos_fuera'"));
        try
        {
            Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
            Assert.True(tablaAusente.Recordada());
            Assert.Equal([ApiFixture.AvisosTaller], await DestinatariosAsync());
        }
        finally
        {
            await api.ConsultarAsync(db => db.Database.ExecuteSqlRawAsync("EXEC sp_rename 'dbo.destinatarios_avisos_fuera', 'destinatarios_avisos'"));
            tablaAusente.Olvidar();
        }
        Assert.False(tablaAusente.Recordada());
    }

    [Fact]
    public async Task CambiarElCorreoOElRolDeOtraPersona_PideLaContrasena_YAvisaAlCorreoAnterior()
    {
        var admin = await AdminAsync();
        var (id, email, _) = await api.CrearEmpleadoAsync(Roles.Operario);
        await api.Cliente().PostAsJsonAsync("/api/v1/auth/recuperar", new { tipo = "personal", correo = email }).EsperarAsync(HttpStatusCode.Accepted);
        Assert.True(await api.ConsultarAsync(db => db.CodigosCuenta.AnyAsync(c => c.IdEmpleado == id && c.FechaUso == null)));
        var ficha = await admin.GetAsync($"/api/v1/empleados/{id}").EsperarAsync(HttpStatusCode.OK);
        var nuevo = $"nuevo{api.Siguiente()}@test.local";
        object Datos(string correo, string rol, string? password) => new
        {
            rut = ficha.GetProperty("rut").GetString(), nombre = "Emp", apellido = "Prueba", telefono = ficha.GetProperty("telefono").GetString(),
            email = correo, rol, passwordActual = password
        };

        var sinPassword = await ConVersion(admin, HttpMethod.Put, $"/api/v1/empleados/{id}", Version(ficha), Datos(nuevo, Roles.Operario, null)).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(sinPassword.GetProperty("errores").TryGetProperty("passwordActual", out _));
        await ConVersion(admin, HttpMethod.Put, $"/api/v1/empleados/{id}", Version(ficha), Datos(email, Roles.Disenador, "no-es-mi-password")).EsperarAsync(HttpStatusCode.BadRequest);
        // Sin cambiar correo ni rol no la pide.
        ficha = await ConVersion(admin, HttpMethod.Put, $"/api/v1/empleados/{id}", Version(ficha), Datos(email, Roles.Operario, null)).EsperarAsync(HttpStatusCode.OK);
        Assert.True(await SesionesVigentesAsync(id) > 0);

        await ConVersion(admin, HttpMethod.Put, $"/api/v1/empleados/{id}", Version(ficha), Datos(nuevo, Roles.Operario, ApiFixture.AdminPassword)).EsperarAsync(HttpStatusCode.OK);

        Assert.Equal(0, await SesionesVigentesAsync(id));
        Assert.False(await api.ConsultarAsync(db => db.CodigosCuenta.AnyAsync(c => c.IdEmpleado == id && c.FechaUso == null)));
        var aviso = await api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleAsync(c => c.Tipo == TiposCorreo.AvisoCambioCorreoPersonal && c.Destinatarios == email));
        Assert.Equal("Tu correo de ingreso a SyncTime cambió", aviso.Asunto);
        Assert.DoesNotContain(nuevo, aviso.CuerpoTexto + aviso.CuerpoHtml);
        Assert.Contains(CorreosDestinosAvisos.Enmascarar(nuevo), aviso.CuerpoTexto);
    }

    [Fact]
    public async Task UnElementoNuloEnElHorarioOEnLasFranjas_Responde400PorIndice()
    {
        var admin = await AdminAsync();
        using var horario = new StringContent("""{"dias":[{"diaSemana":1,"abierto":false},null]}""", System.Text.Encoding.UTF8, "application/json");
        var rechazo = await admin.PutAsync("/api/v1/horarios", horario).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(rechazo.GetProperty("errores").TryGetProperty("dias[1]", out _));

        using var franjas = new StringContent("""{"franjas":[null]}""", System.Text.Encoding.UTF8, "application/json");
        var otro = await api.Cliente(await api.TokenEmpleadoAsync(Roles.Operario)).PutAsync("/api/v1/mi-disponibilidad", franjas).EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(otro.GetProperty("errores").TryGetProperty("franjas[0]", out _));
    }

    [Fact]
    public async Task DosGuardadosALaVezDeLaDisponibilidad_NoMezclanFranjas()
    {
        await (await AdminAsync()).PutAsJsonAsync("/api/v1/horarios", Semana()).EsperarAsync(HttpStatusCode.OK);
        var (id, _, token) = await api.CrearEmpleadoAsync(Roles.Operario);
        var cliente = api.Cliente(token);
        object Franja(string desde, string hasta) => new { franjas = new[] { new { diaSemana = 2, horaInicio = desde, horaFin = hasta } } };

        var respuestas = await Task.WhenAll(
            cliente.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franja("09:00", "10:00")),
            cliente.PutAsJsonAsync("/api/v1/mi-disponibilidad", Franja("11:00", "12:00")));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, await api.ConsultarAsync(db => db.DisponibilidadEmpleados.CountAsync(d => d.IdEmpleado == id)));
        await api.ConsultarAsync(db => db.DisponibilidadEmpleados.Where(d => d.IdEmpleado == id).ExecuteDeleteAsync());
    }

    [Fact]
    public async Task EditarUnCliente_SinActivoConservaElEstado_ValidaPorCampo_YDesactivarEsUnSoloPaso()
    {
        var admin = await AdminAsync();
        var (idCliente, correo, _) = await api.CrearClienteConfirmadoAsync();
        var ficha = await admin.GetAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.OK);
        var telefono = ficha.GetProperty("telefono").GetString();

        var invalido = await ConVersion(admin, HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(ficha), new { nombre = "", apellido = "Web", telefono = "123", email = correo })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(invalido.GetProperty("errores").TryGetProperty("nombre", out _));
        Assert.True(invalido.GetProperty("errores").TryGetProperty("telefono", out _));

        // Si cerrar sus sesiones falla, el cliente no queda desactivado.
        await api.LoginClienteAsync(correo, ApiFixture.AdminPassword);
        var conFalla = api.ClienteConInterceptor(new FallaAlCerrarSesiones(), await api.TokenAdminAsync());
        await ConVersion(conFalla, HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(ficha), new { nombre = "Cli", apellido = "Web", telefono, email = correo, activo = false })
            .EsperarAsync(HttpStatusCode.InternalServerError);
        Assert.True(await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.Activo).SingleAsync()));

        var inactivo = await ConVersion(admin, HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(ficha), new { nombre = "Cli", apellido = "Web", telefono, email = correo, activo = false })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(0, await api.ConsultarAsync(db => db.TokensRenovacion.CountAsync(t => t.IdCliente == idCliente && t.FechaRevocacion == null)));
        // Sin «activo», se conserva: no se reactiva sin pedirlo.
        var editado = await ConVersion(admin, HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(inactivo), new { nombre = "Cli", apellido = "Editado", telefono, email = correo })
            .EsperarAsync(HttpStatusCode.OK);
        Assert.False(editado.GetProperty("activo").GetBoolean());
    }

    // RNFO06: la API valida el RUT del cliente (no solo la pantalla) y lo guarda normalizado.
    [Fact]
    public async Task ElRutDelCliente_SeValidaAlCrearYAlEditar_YSeGuardaNormalizado()
    {
        var admin = await AdminAsync();
        var telefono = $"9{api.Siguiente() % 100_000_000:D8}";

        var malo = await admin.PostAsJsonAsync("/api/v1/clientes", new { rut = "12.345.678-0", nombre = "Rut", apellido = "Malo", telefono })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(malo.GetProperty("errores").TryGetProperty("rut", out _));

        var rut = api.Rut();
        var creado = await admin.PostAsJsonAsync("/api/v1/clientes", new { rut, nombre = "Rut", apellido = "Bueno", telefono })
            .EsperarAsync(HttpStatusCode.Created);
        var idCliente = creado.GetProperty("idCliente").GetInt32();
        Assert.Equal(ValidacionCuenta.NormalizarRut(rut), await api.ConsultarAsync(db => db.Clientes.Where(c => c.IdCliente == idCliente).Select(c => c.Rut).SingleAsync()));

        var ficha = await admin.GetAsync($"/api/v1/clientes/{idCliente}").EsperarAsync(HttpStatusCode.OK);
        var editado = await ConVersion(admin, HttpMethod.Put, $"/api/v1/clientes/{idCliente}", Version(ficha), new { rut = "1-2", nombre = "Rut", apellido = "Bueno", telefono })
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.True(editado.GetProperty("errores").TryGetProperty("rut", out _));
    }

    [Fact]
    public async Task DarleOtraContrasenaAUnaCuentaQueYaTenia_AvisaAlCorreoAnterior()
    {
        var admin = await AdminAsync();
        var (idCliente, correo, _) = await api.CrearClienteConfirmadoAsync();
        var nuevo = $"otro{api.Siguiente()}@correo.test";

        await admin.PutAsJsonAsync($"/api/v1/clientes/{idCliente}/credenciales", new { password = ApiFixture.AdminPassword, passwordActual = ApiFixture.AdminPassword, correo = nuevo })
            .EsperarAsync(HttpStatusCode.NoContent);

        Assert.Equal(1, await CorreosAsync(TiposCorreo.AvisoAccesoReasignado, correo));
    }
}
