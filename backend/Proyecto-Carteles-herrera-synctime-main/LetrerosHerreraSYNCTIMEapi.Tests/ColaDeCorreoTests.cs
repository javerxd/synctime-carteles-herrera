using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Objetivo principal del sistema (13-11-2026): el dueño recibe un aviso por cada solicitud nueva.
// El aviso pasa por una cola: la peticion solo deja el correo en la base, y un proceso aparte lo envia,
// reintenta si el servidor falla y nunca hace esperar ni falla la peticion de quien envio la solicitud.
[Collection(ApiCollection.Name)]
public sealed class ColaDeCorreoTests(ApiFixture api)
{
    private const string UsuarioCentinela = "usuario-centinela-7f3a@test.local";
    private const string ClaveCentinela = "clave-centinela-9b1c";

    // El correo del cliente es unico en la base: cada solicitud usa uno nuevo, o el aviso no tendria "Responder a".
    private object SolicitudOnline(string? email = null) => new
    {
        cliente = new { nombre = "Juana", apellido = "Pruebas", telefono = api.Telefono(), email = email ?? CorreoCliente() },
        descripcionGeneral = "Letrero para la vitrina",
        detalles = new[] { new { idProducto = 1, descripcion = "Letrero luminoso", anchoMm = 1000, altoMm = 2000, cantidad = 2 } }
    };

    private string CorreoCliente() => $"cliente.web{api.Siguiente()}@test.local";

    private static async Task<int> CrearSolicitudOnlineAsync(HttpClient cliente, object cuerpo)
    {
        var creada = await cliente.PostAsJsonAsync("/api/v1/solicitudes/online", cuerpo).EsperarAsync(HttpStatusCode.Created);
        return creada.GetProperty("idSolicitud").GetInt32();
    }

    private Task<CorreoSaliente?> AvisoDeAsync(int idSolicitud) =>
        api.ConsultarAsync(db => db.CorreosSalientes.AsNoTracking().SingleOrDefaultAsync(c => c.Referencia == $"solicitud:{idSolicitud}"));

    private Task AdelantarReintentosAsync() =>
        api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.Estado == "pendiente")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ProximoIntento, DateTimeOffset.UtcNow.AddMinutes(-1))));

    // Variables de una API que envia por SMTP al servidor de prueba, con la cola detenida salvo que se pida.
    private static Dictionary<string, string> PorSmtp(ServidorSmtpDePrueba smtp, bool colaActiva = false, string usuario = "cuenta.pruebas@test.local", string clave = "clave-de-prueba") => new()
    {
        ["Correo__Modo"] = "Smtp",
        ["Correo__Smtp__Servidor"] = "127.0.0.1",
        ["Correo__Smtp__Puerto"] = smtp.Puerto.ToString(),
        ["Correo__Smtp__Seguridad"] = "Ninguna",
        ["Correo__Smtp__Usuario"] = usuario,
        ["Correo__Smtp__Password"] = clave,
        ["Correo__Smtp__TiempoEsperaSegundos"] = "2",
        ["Correo__Cola__Activa"] = colaActiva ? "true" : "false",
        ["Correo__Cola__IntervaloSegundos"] = "1"
    };

    private static Task<ResultadoLote> ProcesarAsync(IServiceProvider servicios) =>
        servicios.GetRequiredService<TrabajadorColaCorreo>().ProcesarLoteAsync(CancellationToken.None);

    // ── El aviso se encola con la solicitud ──

    [Fact]
    public async Task UnaSolicitudEnLinea_DejaUnAvisoParaElTaller()
    {
        await api.LimpiarColaDeCorreoAsync();

        var correo = CorreoCliente();
        var id = await CrearSolicitudOnlineAsync(api.Cliente(), SolicitudOnline(correo));

        var aviso = await AvisoDeAsync(id);
        Assert.NotNull(aviso);
        Assert.Equal("aviso_solicitud_nueva", aviso.Tipo);
        Assert.Equal(ApiFixture.AvisosTaller, aviso.Destinatarios);
        Assert.Equal("pendiente", aviso.Estado);
        Assert.Equal(0, aviso.Intentos);
        Assert.Contains($"N.º {id}", aviso.Asunto);
        Assert.Equal(correo, aviso.ResponderA);
    }

    [Fact]
    public async Task LasSolicitudesDelClienteYLasPresenciales_TambienAvisan()
    {
        var cliente = await api.RegistrarClienteAsync();
        var propia = await api.Cliente(cliente.Token)
            .PostAsJsonAsync("/api/v1/mis/solicitudes", new { descripcionGeneral = "Toldo para el almacén" })
            .EsperarAsync(HttpStatusCode.Created);
        var presencial = await api.CrearSolicitudAsync();

        var avisoPropia = await AvisoDeAsync(propia.GetProperty("idSolicitud").GetInt32());
        var avisoPresencial = await AvisoDeAsync(presencial);

        Assert.NotNull(avisoPropia);
        Assert.NotNull(avisoPresencial);
        Assert.Contains("en línea", avisoPropia.CuerpoTexto);
        Assert.Contains("presencial", avisoPresencial.CuerpoTexto);
        Assert.Contains("Admin Pruebas", avisoPresencial.CuerpoTexto);
    }

    [Fact]
    public async Task SinDestinatariosConfigurados_LaSolicitudSeGuardaIgual()
    {
        await using var sinDestinatarios = api.ConVariable("Correo__AvisosTaller", " ");

        var id = await CrearSolicitudOnlineAsync(sinDestinatarios.Cliente(), SolicitudOnline());

        Assert.Null(await AvisoDeAsync(id));
        Assert.True(await api.ConsultarAsync(db => db.Solicitudes.AnyAsync(s => s.IdSolicitud == id)));
    }

    // ── Bandeja local (desarrollo) ──

    [Fact]
    public async Task ElAviso_LlegaALaBandejaLocalConHtmlTextoYLogo()
    {
        await api.LimpiarColaDeCorreoAsync();
        var correo = CorreoCliente();
        var id = await CrearSolicitudOnlineAsync(api.Cliente(), SolicitudOnline(correo));

        var resultado = await ProcesarAsync(api.Services);

        Assert.Equal(1, resultado.Enviados);
        var aviso = await AvisoDeAsync(id);
        Assert.Equal("enviado", aviso!.Estado);
        Assert.NotNull(aviso.FechaEnvio);
        Assert.Null(aviso.UltimoError);

        var archivo = Assert.Single(Directory.GetFiles(api.CarpetaBandeja, $"*-solicitud-{id}.eml"));
        var mensaje = await MimeMessage.LoadAsync(archivo);
        Assert.Equal(ApiFixture.AvisosTaller, Assert.Single(mensaje.To.Mailboxes).Address);
        Assert.Equal("synctime@test.local", Assert.Single(mensaje.From.Mailboxes).Address);
        Assert.Equal("Letrería Los Herrera", Assert.Single(mensaje.From.Mailboxes).Name);
        Assert.Equal(correo, Assert.Single(mensaje.ReplyTo.Mailboxes).Address);
        Assert.Equal($"Nueva solicitud N.º {id} · Juana Pruebas", mensaje.Subject);
        Assert.Equal("auto-generated", mensaje.Headers["Auto-Submitted"]);
        Assert.Contains("cid:logo-lh", mensaje.HtmlBody);
        Assert.Contains("https://synctime.test/panel", mensaje.HtmlBody);
        Assert.Contains("2 × ", mensaje.TextBody);
        Assert.Contains("Letrero luminoso, 100 × 200 cm", mensaje.TextBody);
        var logo = Assert.Single(mensaje.BodyParts.OfType<MimePart>(), p => p.ContentId == "logo-lh");
        Assert.Equal("image/png", logo.ContentType.MimeType);
    }

    [Fact]
    public async Task LaBandejaLocal_SeVeEnUnaRutaDeDesarrollo()
    {
        await api.LimpiarColaDeCorreoAsync();
        var id = await CrearSolicitudOnlineAsync(api.Cliente(), SolicitudOnline());
        await ProcesarAsync(api.Services);
        var archivo = Path.GetFileName(Assert.Single(Directory.GetFiles(api.CarpetaBandeja, $"*-solicitud-{id}.eml")));

        using var indice = await api.Cliente().GetAsync("/dev/bandeja");
        var html = await indice.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, indice.StatusCode);
        Assert.Contains($"N.º {id}", html);
        Assert.Contains(archivo, html);

        using var vista = await api.Cliente().GetAsync($"/dev/bandeja/{archivo}");
        Assert.Equal(HttpStatusCode.OK, vista.StatusCode);
        Assert.Equal("text/html", vista.Content.Headers.ContentType?.MediaType);
        // El HTML del correo se muestra aislado: sin scripts ni formularios, y el logotipo incrustado.
        Assert.Contains("sandbox", string.Join(";", vista.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("data:image/png;base64,", await vista.Content.ReadAsStringAsync());

        using var texto = await api.Cliente().GetAsync($"/dev/bandeja/{archivo}/texto");
        Assert.Contains("Letrero luminoso", await texto.Content.ReadAsStringAsync());

        foreach (var invalido in new[] { "..%2Fappsettings.json", "no-existe.eml", "%2E%2E%5Csecreto.eml" })
        {
            using var respuesta = await api.Cliente().GetAsync($"/dev/bandeja/{invalido}");
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }
    }

    // ── SMTP real (MailKit) contra el servidor de prueba ──

    [Fact]
    public async Task UnSmtpCaido_NoBloqueaLaPeticion_YElAvisoQuedaParaReintentar()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.Cuelga);
        await api.LimpiarColaDeCorreoAsync();
        await using var conCola = api.ConVariables(PorSmtp(smtp, colaActiva: true));

        var reloj = Stopwatch.StartNew();
        var id = await CrearSolicitudOnlineAsync(conCola.Cliente(), SolicitudOnline());
        reloj.Stop();

        // La peticion termina antes de que el SMTP alcance a agotar su tiempo de espera (2 s).
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(2), $"La peticion tardo {reloj.Elapsed.TotalSeconds:N1} s.");

        CorreoSaliente? aviso = null;
        for (var i = 0; i < 60 && aviso?.Intentos is not >= 1; i++)
        {
            await Task.Delay(250);
            aviso = await AvisoDeAsync(id);
            if (aviso?.Estado == "enviando")
                aviso = null;
        }

        Assert.NotNull(aviso);
        Assert.Equal("pendiente", aviso.Estado);
        Assert.Equal(1, aviso.Intentos);
        Assert.StartsWith("tiempo_agotado", aviso.UltimoError);
        Assert.True(aviso.ProximoIntento > DateTimeOffset.UtcNow);

        // La API sigue atendiendo mientras el servidor de correo no responde.
        using var salud = await conCola.Cliente().GetAsync("/salud");
        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        await CrearSolicitudOnlineAsync(conCola.Cliente(), SolicitudOnline());
    }

    [Fact]
    public async Task LaCola_ReintentaHastaQueElServidorVuelve()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.NoDisponible);
        await api.LimpiarColaDeCorreoAsync();
        await using var porSmtp = api.ConVariables(PorSmtp(smtp));
        var id = await CrearSolicitudOnlineAsync(porSmtp.Cliente(), SolicitudOnline());

        var primero = await ProcesarAsync(porSmtp.Services);

        Assert.Equal(0, primero.Enviados);
        var aviso = await AvisoDeAsync(id);
        Assert.Equal("pendiente", aviso!.Estado);
        Assert.Equal(1, aviso.Intentos);
        Assert.StartsWith("servidor", aviso.UltimoError);
        Assert.InRange(aviso.ProximoIntento - DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1.1));

        // Antes de su hora no se vuelve a intentar.
        smtp.ModoActual = ServidorSmtpDePrueba.Modo.Acepta;
        Assert.Equal(0, (await ProcesarAsync(porSmtp.Services)).Enviados);

        await AdelantarReintentosAsync();
        var segundo = await ProcesarAsync(porSmtp.Services);

        Assert.Equal(1, segundo.Enviados);
        aviso = await AvisoDeAsync(id);
        Assert.Equal("enviado", aviso!.Estado);
        Assert.Equal(2, aviso.Intentos);
        Assert.Null(aviso.UltimoError);
        var recibido = Assert.Single(smtp.Recibidos);
        Assert.Contains("To: avisos.taller@test.local", recibido);
        Assert.Contains("multipart/alternative", recibido);
    }

    // Revision ECC: si el servidor o la cuenta fallan (por ejemplo, Gmail caido o la contraseña cambiada
    // un viernes), el aviso no debe darse por perdido: se sigue intentando hasta que se arregle.
    [Fact]
    public async Task UnServidorCaido_NoAgotaLosIntentosDelAviso()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.NoDisponible);
        await api.LimpiarColaDeCorreoAsync();
        await using var porSmtp = api.ConVariables(new Dictionary<string, string>(PorSmtp(smtp)) { ["Correo__Cola__MaxIntentos"] = "2" });
        var id = await CrearSolicitudOnlineAsync(porSmtp.Cliente(), SolicitudOnline());

        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            await AdelantarReintentosAsync();
            await ProcesarAsync(porSmtp.Services);
        }

        var aviso = await AvisoDeAsync(id);
        Assert.Equal("pendiente", aviso!.Estado);
        Assert.Equal(3, aviso.Intentos);

        smtp.ModoActual = ServidorSmtpDePrueba.Modo.Acepta;
        await AdelantarReintentosAsync();
        Assert.Equal(1, (await ProcesarAsync(porSmtp.Services)).Enviados);
        Assert.Equal("enviado", (await AvisoDeAsync(id))!.Estado);
    }

    // Un correo cuyo envio se corto (el proceso se detuvo) se retoma, pero no sin fin.
    [Fact]
    public async Task UnEnvioInterrumpidoDemasiadasVeces_QuedaFallido()
    {
        await api.LimpiarColaDeCorreoAsync();
        var id = await CrearSolicitudOnlineAsync(api.Cliente(), SolicitudOnline());
        await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.Referencia == $"solicitud:{id}")
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, "enviando")
                .SetProperty(c => c.Intentos, 6)
                .SetProperty(c => c.BloqueadoHasta, DateTimeOffset.UtcNow.AddMinutes(-1))));

        var resultado = await ProcesarAsync(api.Services);

        Assert.Equal(0, resultado.Enviados);
        var aviso = await AvisoDeAsync(id);
        Assert.Equal("fallido", aviso!.Estado);
        Assert.StartsWith("interrumpido", aviso.UltimoError);
    }

    [Fact]
    public async Task UnEnvioInterrumpido_SeRetomaSiLeQuedanIntentos()
    {
        await api.LimpiarColaDeCorreoAsync();
        var id = await CrearSolicitudOnlineAsync(api.Cliente(), SolicitudOnline());
        await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.Referencia == $"solicitud:{id}")
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, "enviando")
                .SetProperty(c => c.Intentos, 1)
                .SetProperty(c => c.BloqueadoHasta, DateTimeOffset.UtcNow.AddMinutes(-1))));

        Assert.Equal(1, (await ProcesarAsync(api.Services)).Enviados);
        Assert.Equal("enviado", (await AvisoDeAsync(id))!.Estado);
    }

    // ── Tope contra envios masivos (revision de seguridad ECC) ──

    [Fact]
    public async Task PasadoElTopePorHora_LlegaUnSoloAvisoDeMuchasSolicitudes()
    {
        await api.LimpiarColaDeCorreoAsync();
        await using var conTope = api.ConVariable("Correo__TopeAvisosPorHora", "2");

        var ids = new List<int>();
        for (var i = 0; i < 4; i++)
            ids.Add(await CrearSolicitudOnlineAsync(conTope.Cliente(), SolicitudOnline()));

        // Las cuatro solicitudes quedan guardadas.
        Assert.Equal(4, await api.ConsultarAsync(db => db.Solicitudes.CountAsync(s => ids.Contains(s.IdSolicitud))));
        // Dos avisos propios y uno solo de "muchas solicitudes".
        Assert.NotNull(await AvisoDeAsync(ids[0]));
        Assert.NotNull(await AvisoDeAsync(ids[1]));
        Assert.Null(await AvisoDeAsync(ids[2]));
        Assert.Null(await AvisoDeAsync(ids[3]));
        var tope = Assert.Single(await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.Tipo == "aviso_tope_solicitudes").ToListAsync()));
        Assert.Equal(ApiFixture.AvisosTaller, tope.Destinatarios);
    }

    // ── Estado del correo para el operador ──

    [Fact]
    public async Task SaludDelCorreo_DiceDegradadoSiHayAvisosFallidos()
    {
        await api.LimpiarColaDeCorreoAsync();
        var ok = await api.Cliente().GetAsync("/salud/correo").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("ok", ok.GetProperty("estado").GetString());

        var id = await CrearSolicitudOnlineAsync(api.Cliente(), SolicitudOnline());
        await api.ConsultarAsync(db => db.CorreosSalientes.Where(c => c.Referencia == $"solicitud:{id}")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, "fallido").SetProperty(c => c.ProximoIntento, DateTimeOffset.UtcNow)));

        var degradado = await api.Cliente().GetAsync("/salud/correo").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("degradado", degradado.GetProperty("estado").GetString());
        // La disponibilidad de la API no depende del correo.
        var lista = await api.Cliente().GetAsync("/salud/lista").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("ok", lista.GetProperty("estado").GetString());
        await api.LimpiarColaDeCorreoAsync();
    }

    [Fact]
    public async Task SaludDelCorreo_DiceDegradadoSinDestinatariosOSinClave()
    {
        await api.LimpiarColaDeCorreoAsync();
        await using var sinDestinatarios = api.ConVariable("Correo__AvisosTaller", " ");
        var respuesta = await sinDestinatarios.Cliente().GetAsync("/salud/correo").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("degradado", respuesta.GetProperty("estado").GetString());

        await using var smtp = new ServidorSmtpDePrueba();
        await using var sinClave = api.ConVariables(PorSmtp(smtp, clave: " "));
        respuesta = await sinClave.Cliente().GetAsync("/salud/correo").EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("degradado", respuesta.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task UnaConfiguracionMalEscrita_ImpideArrancar()
    {
        Assert.ThrowsAny<Exception>(() => api.ConVariable("Correo__Modo", "Gmail"));
        Assert.ThrowsAny<Exception>(() => api.ConVariable("Correo__Smtp__Seguridad", "TLS"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UnaUrlDelSitioMalEscrita_NoImpideElAviso()
    {
        await api.LimpiarColaDeCorreoAsync();
        await using var malaUrl = api.ConVariable("Sitio__UrlPublica", "localhost:5173");

        var id = await CrearSolicitudOnlineAsync(malaUrl.Cliente(), SolicitudOnline());

        var aviso = await AvisoDeAsync(id);
        Assert.NotNull(aviso);
        Assert.DoesNotContain("/panel", aviso.CuerpoHtml);
    }

    [Fact]
    public async Task UnDestinatarioRechazado_NoSeReintenta()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaDestinatario);
        await api.LimpiarColaDeCorreoAsync();
        await using var porSmtp = api.ConVariables(PorSmtp(smtp));
        var id = await CrearSolicitudOnlineAsync(porSmtp.Cliente(), SolicitudOnline());

        await ProcesarAsync(porSmtp.Services);

        var aviso = await AvisoDeAsync(id);
        Assert.Equal("fallido", aviso!.Estado);
        Assert.Equal(1, aviso.Intentos);
        Assert.StartsWith("destinatario", aviso.UltimoError);
    }

    [Fact]
    public async Task UnUsuarioRechazado_DetieneElLote_SinInsistirConLosDemas()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaUsuario);
        await api.LimpiarColaDeCorreoAsync();
        await using var porSmtp = api.ConVariables(PorSmtp(smtp));
        var primera = await CrearSolicitudOnlineAsync(porSmtp.Cliente(), SolicitudOnline());
        var segunda = await CrearSolicitudOnlineAsync(porSmtp.Cliente(), SolicitudOnline());

        var resultado = await ProcesarAsync(porSmtp.Services);

        Assert.True(resultado.Detenido);
        Assert.Equal(1, smtp.Conexiones);
        var intentos = new[] { (await AvisoDeAsync(primera))!.Intentos, (await AvisoDeAsync(segunda))!.Intentos };
        Assert.Equal([0, 1], intentos.Order());
    }

    [Fact]
    public async Task SinLaClaveDeLaCuenta_LaColaEsperaSinGastarIntentos()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        await api.LimpiarColaDeCorreoAsync();
        await using var sinClave = api.ConVariables(PorSmtp(smtp, clave: " "));
        var id = await CrearSolicitudOnlineAsync(sinClave.Cliente(), SolicitudOnline());

        var resultado = await ProcesarAsync(sinClave.Services);

        Assert.True(resultado.Detenido);
        Assert.Equal(0, smtp.Conexiones);
        var aviso = await AvisoDeAsync(id);
        Assert.Equal("pendiente", aviso!.Estado);
        Assert.Equal(0, aviso.Intentos);
    }

    // ── Credenciales ──

    [Fact]
    public async Task LasCredenciales_NoAparecenEnLogsRespuestasNiEnLaBase()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaUsuario);
        await api.LimpiarColaDeCorreoAsync();
        var logs = new LogsEnMemoria();
        await using var porSmtp = api.ConVariables(PorSmtp(smtp, usuario: UsuarioCentinela, clave: ClaveCentinela), logs);

        using var respuesta = await porSmtp.Cliente().PostAsJsonAsync("/api/v1/solicitudes/online", SolicitudOnline());
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var id = System.Text.Json.JsonDocument.Parse(cuerpo).RootElement.GetProperty("idSolicitud").GetInt32();
        await ProcesarAsync(porSmtp.Services);
        using var salud = await porSmtp.Cliente().GetAsync("/salud/lista");
        var cuerpoSalud = await salud.Content.ReadAsStringAsync();

        // La prueba no es vacia: el servidor si recibio el usuario y lo repitio en su respuesta de rechazo.
        Assert.Contains(UsuarioCentinela, smtp.UsuariosRecibidos);
        var aviso = await AvisoDeAsync(id);
        Assert.StartsWith("autenticacion", aviso!.UltimoError);

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{UsuarioCentinela}\0{ClaveCentinela}"));
        var lugares = new Dictionary<string, string>
        {
            ["la respuesta de la solicitud"] = cuerpo,
            ["la respuesta de salud"] = cuerpoSalud,
            ["la base (ultimo_error)"] = aviso.UltimoError ?? "",
            ["los logs"] = string.Join("\n", logs.Mensajes)
        };
        Assert.NotEmpty(logs.Mensajes);
        foreach (var (lugar, texto) in lugares)
        {
            foreach (var secreto in new[] { UsuarioCentinela, ClaveCentinela, base64 })
                Assert.False(texto.Contains(secreto, StringComparison.OrdinalIgnoreCase), $"Una credencial aparecio en {lugar}.");
        }
    }

    private sealed class LogsEnMemoria : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> mensajes = new();

        public IReadOnlyCollection<string> Mensajes => mensajes;

        public ILogger CreateLogger(string categoryName) => new Registro(mensajes);

        public void Dispose() { }

        private sealed class Registro(ConcurrentQueue<string> mensajes) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                mensajes.Enqueue($"{formatter(state, exception)} {exception}");
        }
    }
}
