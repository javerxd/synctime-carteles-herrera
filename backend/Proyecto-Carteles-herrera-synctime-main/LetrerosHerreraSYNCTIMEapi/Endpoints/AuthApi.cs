using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

public static class AuthApi
{
    public static void MapAuthApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Autenticacion");

        // ── Empleado login ──
        group.MapPost("/login", async (LoginRequest req, LetrerosHerreradbContext db, AuthService auth) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Email y password son requeridos." });

            var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.Email == req.Email && e.Activo);
            if (empleado is null || !auth.VerifyPassword(req.Password, empleado.PasswordHash))
                return Results.Unauthorized();

            return RespuestaEmpleado(empleado, auth);
        })
        .AllowAnonymous()
        .RequireRateLimiting("Login");

        // ── Cliente login (optional, for tracking orders) ──
        group.MapPost("/login-cliente", async (ClienteLoginRequest req, LetrerosHerreradbContext db, AuthService auth) =>
        {
            if (string.IsNullOrWhiteSpace(req.Telefono) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { error = "Telefono y password son requeridos." });

            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefono == req.Telefono && c.Activo);
            if (cliente is null || cliente.PasswordHash is null || !auth.VerifyPassword(req.Password, cliente.PasswordHash))
                return Results.Unauthorized();

            var token = auth.CreateTokenCliente(cliente);
            return Results.Ok(new
            {
                token,
                cliente = new { cliente.IdCliente, cliente.Nombre, cliente.Apellido, cliente.Telefono, cliente.Email }
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Login");

        // ── Registro de cliente en la web ──
        // Un telefono ya existente no se puede reclamar desde aqui: daria acceso a pedidos ajenos
        // conociendo solo el numero. Ese caso lo activa el personal con PUT /clientes/{id}/credenciales.
        group.MapPost("/registro-cliente", async (RegistroClienteRequest req, LetrerosHerreradbContext db, AuthService auth) =>
        {
            if (string.IsNullOrWhiteSpace(req.Rut) || string.IsNullOrWhiteSpace(req.Nombre) || string.IsNullOrWhiteSpace(req.Apellido) || string.IsNullOrWhiteSpace(req.Telefono))
                return Results.BadRequest(new { error = "RUT, nombre, apellido y telefono son requeridos." });
            if (!AuthService.EsPasswordValida(req.Password))
                return Results.BadRequest(new { error = AuthService.MensajePasswordInvalida });

            var telefono = req.Telefono.Trim();
            if (await db.Clientes.AnyAsync(c => c.Telefono == telefono))
                return Results.Conflict(new { error = "Ese telefono ya esta registrado. Solicite en el local la activacion de su acceso." });

            var cliente = new Cliente
            {
                Rut = req.Rut,
                Nombre = req.Nombre,
                Apellido = req.Apellido,
                Telefono = telefono,
                Email = req.Email,
                Direccion = req.Direccion,
                PasswordHash = auth.HashPassword(req.Password)
            };

            db.Clientes.Add(cliente);
            await db.SaveChangesAsync();
            return Results.Created("/api/v1/mis/solicitudes",
                new { cliente.IdCliente, cliente.Nombre, cliente.Apellido, cliente.Telefono, cliente.Email });
        })
        .AllowAnonymous()
        .RequireRateLimiting("Publico");

        // ── Create empleado (admin only) ──
        group.MapPost("/empleados", async (CrearEmpleadoRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db, AuthService auth) =>
        {
            if (await Reautenticacion.ConfirmarAsync(user, req.PasswordActual, db, auth) is null)
                return Reautenticacion.Rechazo();

            if (!Roles.Empleados.Contains(req.Rol))
                return Results.BadRequest(new { error = $"Rol inválido. Valores válidos: {string.Join(", ", Roles.Empleados)}" });

            if (string.IsNullOrWhiteSpace(req.Rut) || string.IsNullOrWhiteSpace(req.Nombre) || string.IsNullOrWhiteSpace(req.Apellido)
                || string.IsNullOrWhiteSpace(req.Telefono) || string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest(new { error = "RUT, nombre, apellido, telefono y email son requeridos." });

            if (!AuthService.EsPasswordValida(req.Password))
                return Results.BadRequest(new { error = AuthService.MensajePasswordInvalida });

            if (await db.Empleados.AnyAsync(e => e.Email == req.Email))
                return Results.Conflict(new { error = "Ya existe un empleado con ese email." });

            if (await db.Empleados.AnyAsync(e => e.Rut == req.Rut))
                return Results.Conflict(new { error = "Ya existe un empleado con ese RUT." });

            var empleado = new Empleado
            {
                Rut = req.Rut,
                Nombre = req.Nombre,
                Apellido = req.Apellido,
                Telefono = req.Telefono,
                Email = req.Email,
                PasswordHash = auth.HashPassword(req.Password),
                Rol = req.Rol
            };

            db.Empleados.Add(empleado);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/auth/empleados/{empleado.IdEmpleado}",
                new { empleado.IdEmpleado, empleado.Nombre, empleado.Apellido, empleado.Email, empleado.Rol });
        })
        .RequireAuthorization(Politicas.Gestion)
        .RequireRateLimiting("Reautenticacion");

        // ── Who am I? ──
        group.MapGet("/me", (HttpContext ctx) =>
        {
            var claims = ctx.User.Claims.Select(c => new { c.Type, c.Value });
            return Results.Ok(claims);
        })
        .RequireAuthorization();
    }

    // Misma respuesta para el login con contraseña y con rostro.
    internal static IResult RespuestaEmpleado(Empleado empleado, AuthService auth) => Results.Ok(new
    {
        token = auth.CreateTokenEmpleado(empleado),
        usuario = new { empleado.IdEmpleado, empleado.Nombre, empleado.Apellido, empleado.Email, empleado.Rol }
    });
}

public record LoginRequest(string Email, string Password);
public record ClienteLoginRequest(string Telefono, string Password);
public record RegistroClienteRequest(string Rut, string Nombre, string Apellido, string Telefono, string? Email, string? Direccion, string Password);
public record CrearEmpleadoRequest(string Rut, string Nombre, string Apellido, string Telefono, string Email, string Password, string Rol, string? PasswordActual);
