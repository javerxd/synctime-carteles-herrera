using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 11: lo que comparten las pruebas de notificaciones, auditoria y privacidad.
public static partial class Fase11Apoyo
{
    public static string Codigo(JsonElement error) => error.GetProperty("codigo").GetString()!;

    // Los destinos de los avisos del taller se dejan en un estado conocido, directo en la base (activos, sin enlace).
    public static Task LimpiarDestinosAsync(this ApiFixture api) => api.ConsultarAsync(async db =>
    {
        await db.DestinatariosAvisosHistorial.ExecuteDeleteAsync();
        await db.DestinatariosAvisos.Where(d => d.IdReemplaza != null).ExecuteDeleteAsync();
        return await db.DestinatariosAvisos.ExecuteDeleteAsync();
    });

    public static async Task<int> DestinoActivoAsync(this ApiFixture api, string correo, bool enLinea, bool presenciales)
    {
        var idAdmin = await api.IdAdminAsync();
        return await api.ConsultarAsync(async db =>
        {
            var destino = new DestinatarioAviso
            {
                Correo = correo,
                Estado = EstadosDestinatario.Activo,
                IdEmpleadoSolicita = idAdmin,
                FechaSolicitud = DateTimeOffset.UtcNow,
                FechaConfirmacion = DateTimeOffset.UtcNow,
                RecibeEnLinea = enLinea,
                RecibePresenciales = presenciales
            };
            db.DestinatariosAvisos.Add(destino);
            await db.SaveChangesAsync();
            return destino.IdDestinatario;
        });
    }

    public static string CorreoUnico(this ApiFixture api, string prefijo) => $"{prefijo}{api.Siguiente()}@taller.test";

    // Solicitud en linea de un cliente confirmado nuevo: devuelve el id de la solicitud y el del cliente.
    public static async Task<(int IdSolicitud, int IdCliente, string Correo, string Token)> SolicitudEnLineaAsync(this ApiFixture api, string? descripcion = null,
        HttpClient? cliente = null)
    {
        var (idCliente, correo, token) = await api.CrearClienteConfirmadoAsync();
        var http = cliente ?? api.Cliente();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var creada = await http.PostAsJsonAsync("/api/v1/mis/solicitudes", ApiFixture.CuerpoSolicitud(descripcion: descripcion ?? "Letrero para la vitrina del local"))
            .EsperarAsync(HttpStatusCode.Created);
        return (creada.GetProperty("idSolicitud").GetInt32(), idCliente, correo, token);
    }

    public static Task<List<CorreoSaliente>> CorreosAsync(this ApiFixture api, string referencia) =>
        api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().Where(c => c.Referencia == referencia).OrderBy(c => c.IdCorreo).ToListAsync());

    public static Task<List<CorreoSaliente>> CorreosDeTipoAsync(this ApiFixture api, string tipo, string para) =>
        api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().Where(c => c.Tipo == tipo && c.Destinatarios == para).OrderBy(c => c.IdCorreo).ToListAsync());

    public static Task<List<RegistroAuditoria>> AuditoriaAsync(this ApiFixture api, string entidad, string? idEntidad = null, string? accion = null) =>
        api.ConsultarAsync(db => db.Auditoria.AsNoTracking()
            .Where(a => a.Entidad == entidad && (idEntidad == null || a.IdEntidad == idEntidad) && (accion == null || a.Accion == accion))
            .OrderBy(a => a.IdAuditoria).ToListAsync());

    public static Task<ResultadoLote> ProcesarColaAsync(this IServiceProvider servicios) =>
        servicios.GetRequiredService<TrabajadorColaCorreo>().ProcesarLoteAsync(CancellationToken.None);

    [GeneratedRegex(@"/baja\?t=([A-Za-z0-9_\-]+)")]
    private static partial Regex EnlaceBaja();

    // El token de baja solo viaja en el correo: se lee del cuerpo, como lo leeria quien lo recibe.
    public static string TokenDeBaja(CorreoSaliente correo) => EnlaceBaja().Match(correo.CuerpoTexto).Groups[1].Value;

    // Horario y cartel exigen la version vista desde la fase 11: se lee el ETag y se guarda con If-Match.
    public static async Task<JsonElement> GuardarHorarioAsync(this HttpClient cliente, object semana, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var etag = await EtagAsync(cliente, "/api/v1/horarios");
        using var peticion = new HttpRequestMessage(HttpMethod.Put, "/api/v1/horarios") { Content = JsonContent.Create(semana) };
        peticion.Headers.TryAddWithoutValidation("If-Match", etag);
        return await cliente.SendAsync(peticion).EsperarAsync(esperado);
    }

    public static async Task<JsonElement> GuardarCartelAsync(this HttpClient cliente, object cartel, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var etag = await EtagAsync(cliente, "/api/v1/cartel-digital");
        using var peticion = new HttpRequestMessage(HttpMethod.Put, "/api/v1/cartel-digital") { Content = JsonContent.Create(cartel) };
        peticion.Headers.TryAddWithoutValidation("If-Match", etag);
        return await cliente.SendAsync(peticion).EsperarAsync(esperado);
    }

    public static async Task<string> EtagAsync(HttpClient cliente, string url)
    {
        using var respuesta = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(respuesta.Headers.ETag);
        return respuesta.Headers.ETag!.ToString();
    }

    // Peticion con Idempotency-Key.
    public static HttpRequestMessage ConClave(HttpMethod metodo, string url, string? clave, object cuerpo)
    {
        var peticion = new HttpRequestMessage(metodo, url) { Content = JsonContent.Create(cuerpo) };
        if (clave is not null)
            peticion.Headers.TryAddWithoutValidation("Idempotency-Key", clave);
        return peticion;
    }
}
