using System.Text;
using LetrerosHerreraSYNCTIMEapi.Endpoints;
using LetrerosHerreraSYNCTIMEapi.Middleware;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.OpenApi;
using LetrerosHerreraSYNCTIMEapi.Repositories;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuracion de infraestructura: base de datos, secretos y autenticacion.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontro ConnectionStrings:DefaultConnection. Configure la variable de entorno ConnectionStrings__DefaultConnection.");

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
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

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

// Limites por IP: con una ventana global, un solo abusador bloquearia a todos los usuarios.
static RateLimitPartition<string> VentanaPorIp(HttpContext context, int limite) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = limite, QueueLimit = 0 });

var limiteApi = builder.Configuration.GetValue("RateLimiting:ApiGeneral", 100);
var limiteLogin = builder.Configuration.GetValue("RateLimiting:Login", 10);
var limiteLoginFacial = builder.Configuration.GetValue("RateLimiting:LoginFacial", limiteLogin);
var limiteReautenticacion = builder.Configuration.GetValue("RateLimiting:Reautenticacion", limiteLogin);
var limitePublico = builder.Configuration.GetValue("RateLimiting:Publico", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ApiGeneral", context => VentanaPorIp(context, limiteApi));
    options.AddPolicy("Login", context => VentanaPorIp(context, limiteLogin));
    // Mismo limite con contadores propios: ni fallar con el rostro, ni equivocarse al confirmar la contraseña
    // en una accion delicada, deben bloquear la entrada con contraseña de quienes comparten la IP.
    options.AddPolicy("LoginFacial", context => VentanaPorIp(context, limiteLoginFacial));
    options.AddPolicy("Reautenticacion", context => VentanaPorIp(context, limiteReautenticacion));
    options.AddPolicy("Publico", context => VentanaPorIp(context, limitePublico));
});

// Documentacion automatica y esquema Bearer para probar JWT desde Scalar.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
    await db.Database.MigrateAsync();
}

await AdminBootstrapper.CrearPrimerAdministradorAsync(app.Services, app.Configuration, app.Logger);

if (!app.Services.GetRequiredService<RostroFacialService>().Configurado)
    app.Logger.LogWarning("RostroFacial:ClaveCifrado falta o no es valida: el login facial queda deshabilitado.");

app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();
// Estos controles se aplican antes de ejecutar cualquier endpoint.
app.UseCors("FrontendPermitido");
app.UseRateLimiter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
// Primero se identifica al usuario; despues se revisan sus roles y permisos.
app.UseAuthentication();
app.UseAuthorization();

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
