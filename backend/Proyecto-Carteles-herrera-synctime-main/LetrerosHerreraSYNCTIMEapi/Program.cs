using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Endpoints;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Middleware;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.OpenApi;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuracion de infraestructura: base de datos, secretos y autenticacion.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontro ConnectionStrings:DefaultConnection. Configure la variable de entorno ConnectionStrings__DefaultConnection.");
var nombreBase = BasesDeDatos.Nombre(connectionString);

// Fase 11: la bitacora se escribe sola con cada SaveChanges (InterceptorAuditoria), con quien hace la operacion.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Datos.Auditoria.IActorActual, ActorHttp>();
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Datos.Auditoria.InterceptorAuditoria>();
// Fase 16A (H7): Azure SQL gratuita se pausa sola; los fallos transitorios se reintentan (ver EstrategiaDeReintentos).
// BaseDeDatos:ReintentosMaximos = 0 los apaga. Un valor fuera de rango impide arrancar.
var reintentosBase = builder.Configuration.GetValue("BaseDeDatos:ReintentosMaximos", 5);
var retardoMaximoBase = builder.Configuration.GetValue("BaseDeDatos:RetardoMaximoSegundos", 30);
if (reintentosBase is < 0 or > 10)
    throw new InvalidOperationException($"BaseDeDatos:ReintentosMaximos debe estar entre 0 y 10 y es {reintentosBase}.");
if (retardoMaximoBase is < 1 or > 120)
    throw new InvalidOperationException($"BaseDeDatos:RetardoMaximoSegundos debe estar entre 1 y 120 y es {retardoMaximoBase}.");
builder.Services.AddDbContext<LetrerosHerreradbContext>((servicios, options) =>
    options.UseSqlServer(connectionString, sql =>
        {
            if (reintentosBase > 0)
                sql.ExecutionStrategy(dependencias => new LetrerosHerreraSYNCTIMEapi.Datos.EstrategiaDeReintentos(
                    dependencias, reintentosBase, TimeSpan.FromSeconds(retardoMaximoBase)));
        })
        .AddInterceptors(servicios.GetRequiredService<LetrerosHerreraSYNCTIMEapi.Datos.Auditoria.InterceptorAuditoria>()));

// Fase 16A: detras de un proxy (Render, nginx de compose) la IP y el protocolo del visitante vienen en X-Forwarded-*.
// Solo se leen si se declaran los proxies de confianza (ForwardedHeaders:*): de otro modo cualquiera podria falsearlas.
var opcionesDeReenvio = LetrerosHerreraSYNCTIMEapi.Api.ConfiguracionDespliegue.OpcionesDeReenvio(builder.Configuration);
// Fuera de desarrollo, el sitio publico y los origenes CORS son obligatorios y se revisan al arrancar.
LetrerosHerreraSYNCTIMEapi.Api.ConfiguracionDespliegue.ValidarUrlPublica(builder.Configuration, builder.Environment);
var origenesCors = LetrerosHerreraSYNCTIMEapi.Api.ConfiguracionDespliegue.OrigenesCors(builder.Configuration, builder.Environment);
builder.Services.AddScoped<LetrerosHerreraSYNCTIMEapi.Datos.Auditoria.IAuditoria, LetrerosHerreraSYNCTIMEapi.Datos.Auditoria.Auditoria>();

// Fase 16C-1 (H12): los archivos de los clientes van a disco (desarrollo y compose) o a Azure Blob Storage (la nube gratuita, sin disco).
// Fuera de desarrollo, Almacen:Modo es obligatorio y con Blob se revisa la cadena de conexion al arrancar (ConfiguracionDespliegue).
var opcionesAlmacen = LetrerosHerreraSYNCTIMEapi.Api.ConfiguracionDespliegue.OpcionesDeAlmacen(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IOptions<OpcionesAlmacen>>(Options.Create(opcionesAlmacen));
if (opcionesAlmacen.EsBlob)
    builder.Services.AddSingleton<IAlmacenArchivos, AlmacenEnBlob>();
else
    builder.Services.AddSingleton<IAlmacenArchivos>(sp => AlmacenEnDisco.Desde(
        sp.GetRequiredService<IOptions<OpcionesAdjuntos>>(), sp.GetRequiredService<IOptions<OpcionesImagenes>>(),
        sp.GetRequiredService<IOptions<LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes.OpcionesReportes>>(),
        sp.GetRequiredService<IHostEnvironment>()));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("No se configuro Jwt:Key.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("No se configuro Jwt:Issuer.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("No se configuro Jwt:Audience.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Empleado, p => p.RequireRole(Roles.Empleados))
    .AddPolicy(Politicas.Gestion, p => p.RequireRole(Roles.Dueno, Roles.Administrador))
    .AddPolicy(Politicas.Cliente, p => p.RequireRole(Roles.Cliente));
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<RostroFacialService>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
builder.Services.AddSingleton(TimeProvider.System);

// Servicios comunes (fase 4): correo con cola de salida, consentimientos e imagenes.
// El correo se envia en segundo plano: una peticion solo deja el mensaje en correos_salientes.
// Un valor mal escrito impide arrancar: es preferible a una API que acepta solicitudes y nunca avisa.
builder.Services.AddOptions<OpcionesCorreo>()
    .Bind(builder.Configuration.GetSection(OpcionesCorreo.Seccion))
    .Validate(o => o.ModoValido, "Correo:Modo debe ser Smtp, Api o Bandeja.")
    .Validate(o => !o.EsApi || o.Api.ProveedorValido, "Correo:Api:Proveedor debe ser Brevo.")
    .Validate(o => !o.EsApi || !o.ContactoIgualAlRemitente,
        "Correo:ContactoTaller no puede ser la casilla automatica que envia los avisos (Correo:Remitente:Direccion).")
    .Validate(o => LetrerosHerreraSYNCTIMEapi.Negocio.Reglas.EsCorreo(o.ContactoTaller), "Correo:ContactoTaller debe ser el correo publico de contacto del taller.")
    .Validate(o => !string.Equals(o.ContactoTaller.Trim(), o.Smtp.Usuario?.Trim(), StringComparison.OrdinalIgnoreCase),
        "Correo:ContactoTaller no puede ser la casilla automatica que envia los avisos (Correo:Smtp:Usuario).")
    .Validate(o => o.Smtp.Opciones is not null, "Correo:Smtp:Seguridad debe ser StartTls, SslAlIniciar o Ninguna.")
    .ValidateOnStart();
builder.Services.AddSingleton<RegistroFallosAvisos>();
builder.Services.AddSingleton<SenalColaCorreo>();
builder.Services.AddSingleton<IServicioCorreo, ServicioCorreoEnCola>();
builder.Services.AddSingleton<TablaDestinosAusente>();
builder.Services.AddScoped<IDestinatariosAvisos, DestinatariosAvisosDesdeBaseYConfiguracion>();
builder.Services.AddSingleton<EnviadorSmtp>();
builder.Services.AddSingleton<EnviadorBandejaLocal>();
// Fase 16C-1 (H11): Render gratuito bloquea el SMTP; en la nube el correo sale por HTTPS (Correo:Modo = Api). Sin seguir redirecciones: la
// clave del proveedor viaja en una cabecera propia que .NET no retira al redirigir a otro servidor.
builder.Services.AddSingleton<EnviadorCorreoApi>();
builder.Services.AddHttpClient(EnviadorCorreoApi.NombreDelCliente)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
builder.Services.AddSingleton<IEnviadorCorreo>(sp =>
{
    var modo = sp.GetRequiredService<IOptions<OpcionesCorreo>>().Value;
    return modo.EsBandeja ? sp.GetRequiredService<EnviadorBandejaLocal>()
        : modo.EsApi ? sp.GetRequiredService<EnviadorCorreoApi>()
        : sp.GetRequiredService<EnviadorSmtp>();
});
builder.Services.AddSingleton<TrabajadorColaCorreo>();
builder.Services.AddHostedService<ProcesadorColaCorreo>();
builder.Services.AddScoped<IAvisosDelTaller, AvisosDelTaller>();
builder.Services.AddScoped<IServicioConsentimientos, ServicioConsentimientos>();
builder.Services.AddOptions<OpcionesImagenes>().Bind(builder.Configuration.GetSection(OpcionesImagenes.Seccion));
builder.Services.AddSingleton<AlmacenImagenes>();
builder.Services.AddScoped<ServicioImagenes>();
// Portada publica (fase 5).
builder.Services.AddScoped<ServicioVitrina>();
builder.Services.AddScoped<ServicioEstadoLocal>();
builder.Services.AddScoped<ServicioContacto>();
builder.Services.AddScoped<PurgaDatosPersonales>();
// Fase 6: sesiones con renovacion, codigos de cuenta y datos del cliente.
builder.Services.AddScoped<ServicioSesiones>();
builder.Services.AddScoped<ServicioCodigosCuenta>();
builder.Services.AddScoped<ServicioMiCuenta>();
// Fase 7: solicitudes, adjuntos privados y clientes. La cola de correo dentro de la transaccion (outbox)
// usa el contexto de la peticion: los avisos se guardan con el mismo COMMIT que la solicitud.
builder.Services.AddOptions<OpcionesAdjuntos>().Bind(builder.Configuration.GetSection(OpcionesAdjuntos.Seccion));
builder.Services.AddSingleton<AlmacenAdjuntos>();
builder.Services.AddScoped<ColaEnTransaccion>();
builder.Services.AddScoped<ServicioSolicitudes>();
builder.Services.AddScoped<ServicioAdjuntos>();
builder.Services.AddScoped<ServicioClientes>();
// Fase 8: cotizaciones, pedidos y abonos. El aviso «cotizacion lista» usa la misma cola en la transaccion.
builder.Services.AddScoped<ServicioCotizaciones>();
builder.Services.AddScoped<ServicioPedidos>();
// Fase 9: mantenedores, estado del local y disponibilidad, y los destinos de los avisos del taller.
builder.Services.AddScoped<ServicioCategorias>();
builder.Services.AddScoped<ServicioProductos>();
builder.Services.AddScoped<ServicioMateriales>();
builder.Services.AddScoped<ServicioEmpleados>();
builder.Services.AddScoped<ServicioReautenticacion>();
builder.Services.AddScoped<ServicioFichaClientes>();
builder.Services.AddScoped<ServicioLocal>();
builder.Services.AddScoped<ServicioDestinosAvisos>();
// Fase 10: rostro con prueba de vida, «Rostros registrados» y calibracion.
builder.Services.AddScoped<ServicioRostros>();
builder.Services.AddScoped<ServicioCalibracionRostro>();
// Fase 11: notificaciones, auditoria y privacidad.
builder.Services.AddSingleton<ClaveEnlaceBaja>();
builder.Services.AddScoped<AvisosAlCliente>();
builder.Services.AddScoped<ServicioBajaCorreos>();
builder.Services.AddScoped<ServicioCorreosNoEnviados>();
builder.Services.AddScoped<ServicioVisorAuditoria>();
builder.Services.AddScoped<LetrerosHerreraSYNCTIMEapi.Datos.Indicadores.ConsultasIndicadores>();
builder.Services.AddScoped<ServicioIndicadores>();
builder.Services.AddScoped<ServicioPrivacidad>();
builder.Services.AddSingleton<ServicioIdempotencia>();
builder.Services.AddHostedService<ProcesoPurgaDatosPersonales>();
// Fase 13A: reportes y exportaciones a Excel, PDF y CSV en segundo plano (mismo patron que la cola de correo).
builder.Services.AddOptions<LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes.OpcionesReportes>().Bind(builder.Configuration.GetSection(LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes.OpcionesReportes.Seccion));
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes.AlmacenExportaciones>();
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes.SenalExportaciones>();
builder.Services.AddScoped<LetrerosHerreraSYNCTIMEapi.Datos.Reportes.ConsultasReportes>();
builder.Services.AddScoped<ServicioReportes>();
builder.Services.AddScoped<ServicioExportaciones>();
builder.Services.AddSingleton<TrabajadorExportaciones>();
builder.Services.AddHostedService<ProcesadorExportaciones>();
// Fase 14A: ingreso con Google (token de identidad; sin clave secreta de cliente). Google:ClientId llega por User Secrets o por la
// variable de entorno Google__ClientId, nunca desde un archivo del repositorio. Sin el, el ingreso responde 503 y el frontend no muestra el boton.
builder.Services.AddOptions<LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.OpcionesGoogle>().Bind(builder.Configuration.GetSection(LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.OpcionesGoogle.Seccion));
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle.IVerificadorGoogle, LetrerosHerreraSYNCTIMEapi.Infraestructura.Google.VerificadorGoogleReal>();
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle.NoncesGoogle>(sp => new(sp.GetRequiredService<TimeProvider>()));
// El pase del registro pendiente se firma con una clave derivada de Jwt:Key (otro proposito, otra clave: no sirve para falsificar tokens de sesion).
builder.Services.AddSingleton<LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle.PaseGoogle>(sp => new(
    System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtKey), Encoding.UTF8.GetBytes("synctime:pase-google:v1")), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddScoped<ServicioGoogle>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// Todo error sale con el mismo formato, tambien los que escribe el framework (401, 403, 404, 429, 500).
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => ErrorApi.Completar(context.ProblemDetails));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// Compresion de respuestas (RNFP25). Las respuestas no mezclan secretos con texto elegido por un
// atacante, que es la condicion del ataque BREACH, asi que se permite tambien sobre HTTPS.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/problem+json");
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

builder.Services.AddHealthChecks()
    .AddCheck<SaludBaseDeDatos>("base_de_datos", tags: [Salud.EtiquetaLista])
    .AddCheck<SaludCorreo>("correo", tags: [Salud.EtiquetaCorreo]);

// Controles transversales: solo el frontend permitido y limites contra abuso.
builder.Services.AddCors(options =>
{
    // Fase 16A: fuera de desarrollo la lista es obligatoria y se valida (ConfiguracionDespliegue.OrigenesCors).
    var allowedOrigins = origenesCors;
    // Con credenciales (la cookie de renovacion): solo orígenes explicitos, nunca "*" (RNFP29).
    options.AddPolicy("FrontendPermitido", policy =>
        policy.WithOrigins(allowedOrigins)
            .WithHeaders("Authorization", "Content-Type", "Accept", "If-Match", MiddlewareIdempotencia.Cabecera,
                LetrerosHerreraSYNCTIMEapi.Endpoints.CookiesSesion.CabeceraCsrf)
            .WithExposedHeaders("ETag", MiddlewareIdempotencia.CabeceraRepetida, "Content-Disposition")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .AllowCredentials());
});

// Limites por minuto (RNFP07). Con una ventana global, un solo abusador bloquearia a todos los usuarios.
static RateLimitPartition<string> Ventana(string clave, int limite) =>
    RateLimitPartition.GetFixedWindowLimiter(
        clave,
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = limite, QueueLimit = 0 });

// Para los envios anonimos que guardan algo (contacto, cookies): pocas veces por hora es todo lo que una
// persona necesita, y asi un robot no llena la base ni el cupo de avisos del taller.
static RateLimitPartition<string> VentanaHora(string clave, int limite) =>
    RateLimitPartition.GetFixedWindowLimiter(
        clave,
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromHours(1), PermitLimit = limite, QueueLimit = 0 });

static string Ip(HttpContext context) => $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip"}";

// Fase 14A: una IPv6 de casa u oficina trae un bloque /64 entero: se agrupa por ese bloque, o quien cambia de direccion en cada peticion
// se saltaria el limite.
static string IpAgrupada(HttpContext context)
{
    var ip = context.Connection.RemoteIpAddress;
    if (ip is null)
        return "ip:sin-ip";
    if (ip.IsIPv4MappedToIPv6)
        return $"ip:{ip.MapToIPv4()}";
    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
    {
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"ip6:{new System.Net.IPAddress(bytes)}";
    }
    return $"ip:{ip}";
}

// Con sesion se limita por usuario, para que quienes comparten la IP del taller no se bloqueen entre si.
static string UsuarioOIp(HttpContext context)
{
    var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (id is null)
        return Ip(context);
    return $"{(context.User.IsInRole(Roles.Cliente) ? "cliente" : "empleado")}:{id}";
}

var limiteApi = builder.Configuration.GetValue("RateLimiting:ApiGeneral", 100);
var limiteLogin = builder.Configuration.GetValue("RateLimiting:Login", 10);
var limiteLoginFacial = builder.Configuration.GetValue("RateLimiting:LoginFacial", limiteLogin);
// Seguimiento de la fase 10: pedir desafios tiene su propio cupo, para no gastar los intentos de ingreso con rostro.
var limiteDesafioFacial = builder.Configuration.GetValue("RateLimiting:DesafioFacial", 10);
var limiteReautenticacion = builder.Configuration.GetValue("RateLimiting:Reautenticacion", limiteLogin);
var limitePublico = builder.Configuration.GetValue("RateLimiting:Publico", 10);
// Fase 14A: cada intento con Google son dos peticiones (el nonce y el ingreso): 20 por minuto, con contadores aparte del ingreso con contraseña.
var limiteIngresoGoogle = builder.Configuration.GetValue("RateLimiting:IngresoGoogle", limiteLogin * 2);
var limiteSalud = builder.Configuration.GetValue("RateLimiting:Salud", 60);
// Por hora, no por minuto.
var limiteContacto = builder.Configuration.GetValue("RateLimiting:ContactoPorHora", 5);
var limiteCookies = builder.Configuration.GetValue("RateLimiting:CookiesPorHora", 5);
// Una pagina de la vitrina pide varias imagenes a la vez; el navegador las guarda despues por un año.
var limiteImagenes = builder.Configuration.GetValue("RateLimiting:Imagenes", 600);
// Fase 7: subidas de adjuntos (procesar una foto cuesta), por usuario y por minuto.
var limiteAdjuntos = builder.Configuration.GetValue("RateLimiting:Adjuntos", 10);
// Fase 13A: pedir una exportacion cuesta (genera un archivo): 10 por minuto por persona.
var limiteExportaciones = builder.Configuration.GetValue("RateLimiting:Exportaciones", 10);
// Leer un reporte tambien cuesta (varias consultas y hasta 50.000 filas): 30 por minuto por persona.
var limiteReportes = builder.Configuration.GetValue("RateLimiting:Reportes", 30);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ApiGeneral", context => Ventana(UsuarioOIp(context), limiteApi));
    options.AddPolicy("Login", context => Ventana(Ip(context), limiteLogin));
    // Mismo limite con contadores propios: ni fallar con el rostro, ni equivocarse al confirmar la contraseña
    // en una accion delicada, deben bloquear la entrada con contraseña de quienes comparten la IP.
    options.AddPolicy("LoginFacial", context => Ventana(Ip(context), limiteLoginFacial));
    options.AddPolicy("DesafioFacial", context => Ventana(Ip(context), limiteDesafioFacial));
    options.AddPolicy("Reautenticacion", context => Ventana(Ip(context), limiteReautenticacion));
    options.AddPolicy("Publico", context => Ventana(Ip(context), limitePublico));
    options.AddPolicy(LetrerosHerreraSYNCTIMEapi.Endpoints.GoogleApi.PoliticaDeLimite, context => Ventana(IpAgrupada(context), limiteIngresoGoogle));
    options.AddPolicy("Contacto", context => VentanaHora(Ip(context), limiteContacto));
    options.AddPolicy("Cookies", context => VentanaHora(Ip(context), limiteCookies));
    // Holgado para que un orquestador pueda sondear cada pocos segundos, pero no ilimitado.
    options.AddPolicy(Salud.PoliticaDeLimite, context => Ventana(Ip(context), limiteSalud));
    options.AddPolicy("Imagenes", context => Ventana(Ip(context), limiteImagenes));
    options.AddPolicy(LetrerosHerreraSYNCTIMEapi.Endpoints.ArchivoSubido.PoliticaDeLimite, context => Ventana(UsuarioOIp(context), limiteAdjuntos));
    options.AddPolicy(LetrerosHerreraSYNCTIMEapi.Endpoints.ReportesApi.PoliticaDeLectura, context => Ventana(UsuarioOIp(context), limiteReportes));
    options.AddPolicy(LetrerosHerreraSYNCTIMEapi.Endpoints.ReportesApi.PoliticaDeLimite, context => Ventana(UsuarioOIp(context), limiteExportaciones));
});
// El limite por IP no basta contra quien reparte sus intentos entre muchas IP: este cuenta por cuenta.
builder.Services.AddSingleton(new LimitadorPorCuenta(builder.Configuration.GetValue("RateLimiting:PorCuenta", limiteLogin)));

// Documentacion automatica y esquema Bearer para probar JWT desde Scalar.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// Fase 16C-1 (H14): fuera de desarrollo, lo que toca la base al arrancar corre en segundo plano y reintenta mientras la base despierta (Azure SQL
// gratuita se pausa sola; Render gratuito se duerme). El servidor abre el puerto de inmediato y /salud responde; /salud/lista la despierta.
// Lo que no necesita la base (revisar la configuracion del primer administrador) sigue impidiendo arrancar si esta mal escrito.
if (!builder.Environment.IsDevelopment())
{
    AdminBootstrapper.ValidarConfiguracion(builder.Configuration);
    var opcionesDeArranque = OpcionesArranqueBase.Desde(builder.Configuration);
    builder.Services.AddHostedService(sp => new TareasDeArranqueDeLaBase(
        [
            // Una base atrasada debe notarse (fuera de desarrollo tampoco se migra sola): sin la tabla de la cola, por ejemplo, ninguna
            // solicitud avisaria al taller.
            new PasoDeArranque("migraciones pendientes", async ct =>
            {
                await using var ambito = sp.CreateAsyncScope();
                var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
                if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger("Arranque").LogCritical(
                        "La base {Base} tiene migraciones pendientes: partes de la API (como los avisos por correo) fallaran hasta aplicarlas con respaldo previo.", nombreBase);
            }),
            // A quien avisar de las solicitudes nuevas: si no hay nadie, se dice al arrancar (y lo muestra /salud/correo).
            new PasoDeArranque("destinatarios de los avisos", async ct =>
            {
                await using var ambito = sp.CreateAsyncScope();
                await ambito.ServiceProvider.GetRequiredService<IDestinatariosAvisos>().ObtenerAsync(ct);
            }),
            // La base de demostracion recibe sus usuarios de la semilla, no de Bootstrap:Admin.
            new PasoDeArranque("primer administrador", _ => BasesDeDatos.EsDemo(nombreBase)
                ? Task.CompletedTask
                : AdminBootstrapper.CrearPrimerAdministradorAsync(sp, sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILoggerFactory>().CreateLogger("Arranque")))
        ],
        opcionesDeArranque, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IHostApplicationLifetime>(), sp.GetRequiredService<ILogger<TareasDeArranqueDeLaBase>>()));
}

var app = builder.Build();

// Siempre a la vista: la conexion puede venir de un perfil, de una variable de entorno o de User Secrets,
// y hay que poder ver de un vistazo si se esta trabajando sobre la base de demostracion o sobre la real.
app.Logger.LogInformation("Base de datos en uso: {Base} ({Tipo}).", nombreBase,
    BasesDeDatos.EsDemo(nombreBase) ? "demostracion" : BasesDeDatos.EsDePruebas(nombreBase) ? "pruebas" : "REAL");

// El estado "abierto ahora" y las fechas que ve el visitante usan la hora de Chile: sin esa zona, mejor
// no arrancar que responder 500 en la portada.
_ = LetrerosHerreraSYNCTIMEapi.Negocio.Local.HoraChile.ZonaHoraria;

// Comando de mantenimiento: carga los datos de demostracion y termina, sin levantar el servidor.
if (args.Contains(SemillaDemo.Comando))
{
    Environment.ExitCode = await SemillaDemo.EjecutarAsync(app.Services, app.Configuration, nombreBase, app.Logger);
    return;
}

// Comando de mantenimiento: envia un correo de prueba por SMTP a los destinatarios de los avisos y termina.
if (args.Contains(PruebaDeCorreo.Comando))
{
    Environment.ExitCode = await PruebaDeCorreo.EjecutarAsync(app.Services);
    return;
}

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();

    // Las migraciones se aplican solas unicamente en la base de demostracion y en las de pruebas.
    // En cualquier otra, incluida la real, se aplican a mano y con un respaldo previo.
    if (BasesDeDatos.AdmiteMigracionAutomatica(nombreBase))
        await db.Database.MigrateAsync();
    else if ((await db.Database.GetPendingMigrationsAsync()).Any())
        app.Logger.LogWarning("La base {Base} tiene migraciones pendientes. No se aplican solas: respalde la base y ejecute 'dotnet ef database update'.", nombreBase);
}
// Fuera de desarrollo (fase 16C-1, H14) estos mismos pasos corren en segundo plano, con reintentos, para que una base en pausa (Azure SQL
// gratuita) no impida que la API levante: ver TareasDeArranqueDeLaBase, registrada antes de construir la aplicacion.
if (app.Environment.IsDevelopment())
{
    // A quien avisar de las solicitudes nuevas: si no hay nadie, se dice al arrancar (y lo muestra /salud/correo).
    await using (var scope = app.Services.CreateAsyncScope())
        await scope.ServiceProvider.GetRequiredService<IDestinatariosAvisos>().ObtenerAsync(CancellationToken.None);

    // La base de demostracion recibe sus usuarios de la semilla, no de Bootstrap:Admin.
    if (!BasesDeDatos.EsDemo(nombreBase))
        await AdminBootstrapper.CrearPrimerAdministradorAsync(app.Services, app.Configuration, app.Logger);
}

if (!app.Services.GetRequiredService<RostroFacialService>().Configurado)
    app.Logger.LogWarning("RostroFacial:ClaveCifrado falta o no es valida: el login facial queda deshabilitado.");

// El orden importa: cada paso envuelve a los que vienen despues.
// Primero se aprende la IP y el protocolo reales del visitante: los limites de tasa, la redireccion a HTTPS, la bitacora y
// la regla contra solicitudes falsas leen la conexion, y detras de un proxy esta trae la del proxy.
if (opcionesDeReenvio is not null)
    app.UseForwardedHeaders(opcionesDeReenvio);
app.UseResponseCompression();
app.UseCabecerasDeSeguridad(politicaDeContenidoEstricta: !app.Environment.IsDevelopment());
// Los errores tambien deben llevar las cabeceras de CORS, o el navegador no dejaria que el frontend
// leyera su motivo. Lo comprueba la prueba LosErrores_ConservanLasCabecerasDeSeguridadYDeCors.
app.UseCors("FrontendPermitido");
app.UseExceptionHandler();
// Da cuerpo, con el formato estandar, a las respuestas de error que salen vacias (401, 403, 404, 429).
app.UseStatusCodePages();
app.UseMiddleware<RequestLoggingMiddleware>();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();

// Primero se identifica al usuario; despues se cuentan sus peticiones y se revisan sus permisos.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
// Fase 11: Idempotency-Key en las rutas que crean (despues de saber quien es el usuario).
app.UseMiddleware<MiddlewareIdempotencia>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // Los correos del modo Bandeja, para verlos sin un programa de correo. Solo en desarrollo y en ese modo.
    if (app.Services.GetRequiredService<IOptions<OpcionesCorreo>>().Value.EsBandeja)
        app.MapBandejaLocal();
}

app.MapSalud();

// Registro de rutas por area del negocio, al estilo de GamesApi.
app.MapAuthApi();
app.MapGoogleApi();
app.MapRostroApi();
app.MapCatalogoApi();
app.MapClienteApi();
app.MapPersonalApi();
app.MapLocalApi();
app.MapDestinosAvisosApi();
app.MapSolicitudApi();
app.MapPortalClienteApi();
app.MapMisSolicitudesApi();
app.MapAdjuntoApi();
app.MapCotizacionApi();
app.MapPedidoApi();
app.MapImagenApi();
app.MapPortadaApi();
app.MapCuentaApi();
app.MapCorreosApi();
app.MapAuditoriaApi();
app.MapIndicadoresApi();
app.MapReportesApi();

app.Run();

public partial class Program;
