using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Api;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Endpoints;
using LetrerosHerreraSYNCTIMEapi.Middleware;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.OpenApi;
using LetrerosHerreraSYNCTIMEapi.Repositories;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuracion de infraestructura: base de datos, secretos y autenticacion.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontro ConnectionStrings:DefaultConnection. Configure la variable de entorno ConnectionStrings__DefaultConnection.");
var nombreBase = BasesDeDatos.Nombre(connectionString);

builder.Services.AddDbContext<LetrerosHerreradbContext>(options =>
    options.UseSqlServer(connectionString));

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
builder.Services.AddScoped<ISolicitudRepository, SolicitudRepository>();
builder.Services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
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
    .AddCheck<SaludBaseDeDatos>("base_de_datos", tags: [Salud.EtiquetaLista]);

// Controles transversales: solo el frontend permitido y limites contra abuso.
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "https://localhost:3000" };
    options.AddPolicy("FrontendPermitido", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Limites por minuto (RNFP07). Con una ventana global, un solo abusador bloquearia a todos los usuarios.
static RateLimitPartition<string> Ventana(string clave, int limite) =>
    RateLimitPartition.GetFixedWindowLimiter(
        clave,
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = limite, QueueLimit = 0 });

static string Ip(HttpContext context) => $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip"}";

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
var limiteReautenticacion = builder.Configuration.GetValue("RateLimiting:Reautenticacion", limiteLogin);
var limitePublico = builder.Configuration.GetValue("RateLimiting:Publico", 10);
var limiteSalud = builder.Configuration.GetValue("RateLimiting:Salud", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ApiGeneral", context => Ventana(UsuarioOIp(context), limiteApi));
    options.AddPolicy("Login", context => Ventana(Ip(context), limiteLogin));
    // Mismo limite con contadores propios: ni fallar con el rostro, ni equivocarse al confirmar la contraseña
    // en una accion delicada, deben bloquear la entrada con contraseña de quienes comparten la IP.
    options.AddPolicy("LoginFacial", context => Ventana(Ip(context), limiteLoginFacial));
    options.AddPolicy("Reautenticacion", context => Ventana(Ip(context), limiteReautenticacion));
    options.AddPolicy("Publico", context => Ventana(Ip(context), limitePublico));
    // Holgado para que un orquestador pueda sondear cada pocos segundos, pero no ilimitado.
    options.AddPolicy(Salud.PoliticaDeLimite, context => Ventana(Ip(context), limiteSalud));
});
// El limite por IP no basta contra quien reparte sus intentos entre muchas IP: este cuenta por cuenta.
builder.Services.AddSingleton(new LimitadorPorCuenta(builder.Configuration.GetValue("RateLimiting:PorCuenta", limiteLogin)));

// Documentacion automatica y esquema Bearer para probar JWT desde Scalar.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// Siempre a la vista: la conexion puede venir de un perfil, de una variable de entorno o de User Secrets,
// y hay que poder ver de un vistazo si se esta trabajando sobre la base de demostracion o sobre la real.
app.Logger.LogInformation("Base de datos en uso: {Base} ({Tipo}).", nombreBase,
    BasesDeDatos.EsDemo(nombreBase) ? "demostracion" : BasesDeDatos.EsDePruebas(nombreBase) ? "pruebas" : "REAL");

// Comando de mantenimiento: carga los datos de demostracion y termina, sin levantar el servidor.
if (args.Contains(SemillaDemo.Comando))
{
    Environment.ExitCode = await SemillaDemo.EjecutarAsync(app.Services, app.Configuration, nombreBase, app.Logger);
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

// La base de demostracion recibe sus usuarios de la semilla, no de Bootstrap:Admin.
if (!BasesDeDatos.EsDemo(nombreBase))
    await AdminBootstrapper.CrearPrimerAdministradorAsync(app.Services, app.Configuration, app.Logger);

if (!app.Services.GetRequiredService<RostroFacialService>().Configurado)
    app.Logger.LogWarning("RostroFacial:ClaveCifrado falta o no es valida: el login facial queda deshabilitado.");

// El orden importa: cada paso envuelve a los que vienen despues.
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapSalud();

// Registro de rutas por area del negocio, al estilo de GamesApi.
app.MapAuthApi();
app.MapRostroApi();
app.MapCatalogoApi();
app.MapSolicitudApi();
app.MapPortalClienteApi();
app.MapCotizacionApi();
app.MapPedidoApi();

app.Run();

public partial class Program;
