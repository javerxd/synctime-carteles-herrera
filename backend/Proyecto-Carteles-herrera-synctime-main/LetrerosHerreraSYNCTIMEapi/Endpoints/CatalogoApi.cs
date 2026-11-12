using LetrerosHerreraSYNCTIMEapi.Api;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

public static class CatalogoApi
{
    public static void MapCatalogoApi(this WebApplication app)
    {
        // ── Clientes CRUD ──
        var clientes = app.MapGroup("/api/v1/clientes").WithTags("Clientes")
            .RequireAuthorization(Politicas.Empleado).RequireRateLimiting("ApiGeneral");

        clientes.MapGet("/", async (LetrerosHerreradbContext db) =>
            Results.Ok(await db.Clientes.AsNoTracking().ToListAsync()));

        clientes.MapGet("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var c = await db.Clientes.FindAsync(id);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });

        clientes.MapPost("/", async (CrearClienteRequest req, LetrerosHerreradbContext db) =>
        {
            if (await db.Clientes.AnyAsync(c => c.Telefono == req.Telefono))
                return ErrorApi.Conflicto("Ya existe un cliente con ese teléfono.");

            var cliente = new Cliente
            {
                Rut = req.Rut,
                Nombre = req.Nombre,
                Apellido = req.Apellido,
                Telefono = req.Telefono,
                Email = req.Email,
                Direccion = req.Direccion,
                Activo = req.Activo
            };

            db.Clientes.Add(cliente);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/clientes/{cliente.IdCliente}", cliente);
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "operario"));

        clientes.MapPut("/{id}", async (int id, ActualizarClienteRequest req, LetrerosHerreradbContext db) =>
        {
            var existing = await db.Clientes.FindAsync(id);
            if (existing is null) return Results.NotFound();

            existing.Rut = req.Rut;
            existing.Nombre = req.Nombre;
            existing.Apellido = req.Apellido;
            existing.Telefono = req.Telefono;
            existing.Email = req.Email;
            existing.Direccion = req.Direccion;
            existing.Activo = req.Activo;

            await db.SaveChangesAsync();
            return Results.Ok(existing);
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "operario"));

        // Activa el acceso web de un cliente que ya existe, una vez que el personal comprobo su identidad.
        clientes.MapPut("/{id}/credenciales", async (int id, AsignarCredencialesRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db, AuthService auth) =>
        {
            if (await Reautenticacion.ConfirmarAsync(user, req.PasswordActual, db, auth) is null)
                return Reautenticacion.Rechazo();

            if (!AuthService.EsPasswordValida(req.Password))
                return ErrorApi.Invalido(AuthService.MensajePasswordInvalida);

            var cliente = await db.Clientes.FindAsync(id);
            if (cliente is null) return Results.NotFound();

            cliente.PasswordHash = auth.HashPassword(req.Password);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador", "operario"))
        .RequireRateLimiting("Reautenticacion");

        clientes.MapDelete("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var c = await db.Clientes.FindAsync(id);
            if (c is null) return Results.NotFound();
            db.Clientes.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        // ── Productos CRUD ──
        var productos = app.MapGroup("/api/v1/productos").WithTags("Productos")
            .RequireRateLimiting("ApiGeneral");

        // Por defecto se lista lo que se vende, tambien para el personal con sesion que mira la pagina publica.
        // Para administrar lo inactivo, el personal lo pide de forma explicita.
        productos.MapGet("/", async (ClaimsPrincipal user, LetrerosHerreradbContext db, bool incluirInactivos = false) =>
            Results.Ok(await db.Productos.AsNoTracking().Include(p => p.Categoria)
                .Disponibles(incluirInactivos && EsPersonal(user))
                .ToListAsync()))
            .AllowAnonymous();

        productos.MapGet("/{id}", async (int id, ClaimsPrincipal user, LetrerosHerreradbContext db) =>
        {
            var p = await db.Productos.AsNoTracking().Include(p => p.Categoria)
                .Disponibles(EsPersonal(user))
                .FirstOrDefaultAsync(p => p.IdProducto == id);
            return p is null ? Results.NotFound() : Results.Ok(p);
        }).AllowAnonymous();

        productos.MapPost("/", async (CrearProductoRequest req, LetrerosHerreradbContext db) =>
        {
            var producto = new Producto
            {
                IdCategoria = req.IdCategoria,
                Nombre = req.Nombre,
                Descripcion = req.Descripcion,
                PrecioBaseRef = req.PrecioBaseRef,
                Activo = req.Activo
            };
            db.Productos.Add(producto);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/productos/{producto.IdProducto}", producto);
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        productos.MapPut("/{id}", async (int id, ActualizarProductoRequest req, LetrerosHerreradbContext db) =>
        {
            var existing = await db.Productos.FindAsync(id);
            if (existing is null) return Results.NotFound();
            existing.IdCategoria = req.IdCategoria;
            existing.Nombre = req.Nombre;
            existing.Descripcion = req.Descripcion;
            existing.PrecioBaseRef = req.PrecioBaseRef;
            existing.Activo = req.Activo;
            await db.SaveChangesAsync();
            return Results.Ok(existing);
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        productos.MapDelete("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var p = await db.Productos.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.Productos.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        // ── Categorías CRUD ──
        var categorias = app.MapGroup("/api/v1/categorias").WithTags("Categorias")
            .RequireRateLimiting("ApiGeneral");

        categorias.MapGet("/", async (ClaimsPrincipal user, LetrerosHerreradbContext db, bool incluirInactivos = false) =>
        {
            var todas = incluirInactivos && EsPersonal(user);
            return Results.Ok(await db.Categorias.AsNoTracking().Where(c => todas || c.Activo).ToListAsync());
        })
        .AllowAnonymous();

        categorias.MapGet("/{id}", async (int id, ClaimsPrincipal user, LetrerosHerreradbContext db) =>
        {
            var c = await db.Categorias.FindAsync(id);
            return c is null || (!c.Activo && !EsPersonal(user)) ? Results.NotFound() : Results.Ok(c);
        }).AllowAnonymous();

        categorias.MapPost("/", async (CrearCategoriaRequest req, LetrerosHerreradbContext db) =>
        {
            var cat = new Categoria
            {
                Nombre = req.Nombre,
                Descripcion = req.Descripcion,
                Activo = req.Activo
            };
            db.Categorias.Add(cat);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/categorias/{cat.IdCategoria}", cat);
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        categorias.MapPut("/{id}", async (int id, ActualizarCategoriaRequest req, LetrerosHerreradbContext db) =>
        {
            var existing = await db.Categorias.FindAsync(id);
            if (existing is null) return Results.NotFound();
            existing.Nombre = req.Nombre;
            existing.Descripcion = req.Descripcion;
            existing.Activo = req.Activo;
            await db.SaveChangesAsync();
            return Results.Ok(existing);
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        categorias.MapDelete("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var c = await db.Categorias.FindAsync(id);
            if (c is null) return Results.NotFound();
            db.Categorias.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        // ── Catálogo (vitrina pública) ──
        var catalogo = app.MapGroup("/api/v1/catalogo").WithTags("Catalogo")
            .RequireRateLimiting("ApiGeneral");

        catalogo.MapGet("/", async (ClaimsPrincipal user, LetrerosHerreradbContext db, bool incluirOcultos = false) =>
            Results.Ok(await db.Catalogos.AsNoTracking()
                .Include(c => c.Producto!).ThenInclude(p => p!.Categoria)
                .Publicados(incluirOcultos && EsPersonal(user))
                .ToListAsync()))
            .AllowAnonymous();

        catalogo.MapGet("/{id}", async (int id, ClaimsPrincipal user, LetrerosHerreradbContext db) =>
        {
            var item = await db.Catalogos.AsNoTracking()
                .Include(c => c.Producto!).ThenInclude(p => p!.Categoria)
                .Publicados(EsPersonal(user))
                .FirstOrDefaultAsync(c => c.IdCatalogo == id);
            return item is null ? Results.NotFound() : Results.Ok(item);
        }).AllowAnonymous();

        catalogo.MapPost("/", async (CrearCatalogoRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db) =>
        {
            var item = new Catalogo
            {
                IdProducto = req.IdProducto,
                IdEmpleadoCreador = user.IdUsuario(),
                Titulo = req.Titulo,
                Descripcion = req.Descripcion,
                ImagenUrl = req.ImagenUrl,
                AnchoMm = req.AnchoMm,
                AltoMm = req.AltoMm,
                PrecioReferencial = req.PrecioReferencial,
                Visible = req.Visible
            };
            db.Catalogos.Add(item);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/catalogo/{item.IdCatalogo}", item);
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        catalogo.MapPut("/{id}", async (int id, ActualizarCatalogoRequest req, LetrerosHerreradbContext db) =>
        {
            var existing = await db.Catalogos.FindAsync(id);
            if (existing is null) return Results.NotFound();
            existing.IdProducto = req.IdProducto;
            existing.Titulo = req.Titulo;
            existing.Descripcion = req.Descripcion;
            existing.ImagenUrl = req.ImagenUrl;
            existing.AnchoMm = req.AnchoMm;
            existing.AltoMm = req.AltoMm;
            existing.PrecioReferencial = req.PrecioReferencial;
            existing.Visible = req.Visible;
            await db.SaveChangesAsync();
            return Results.Ok(existing);
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        catalogo.MapDelete("/{id}", async (int id, LetrerosHerreradbContext db) =>
        {
            var item = await db.Catalogos.FindAsync(id);
            if (item is null) return Results.NotFound();
            db.Catalogos.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("dueño", "administrador"));

        // ── Consultas operativas ──
        app.MapGet("/api/v1/empleados", async (LetrerosHerreradbContext db) =>
            Results.Ok(await db.Empleados.AsNoTracking().ToListAsync()))
            .WithTags("Empleados")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral");

        app.MapGet("/api/v1/materiales", async (LetrerosHerreradbContext db) =>
            Results.Ok(await db.Materiales.AsNoTracking().ToListAsync()))
            .WithTags("Materiales")
            .RequireAuthorization(Politicas.Empleado)
            .RequireRateLimiting("ApiGeneral");

        // ── Horarios de atención ──
        app.MapGet("/api/v1/horarios", async (LetrerosHerreradbContext db) =>
            Results.Ok(await db.HorariosAtencion.AsNoTracking().OrderBy(h => h.DiaSemana).ToListAsync()))
            .WithTags("Horarios")
            .AllowAnonymous()
            .RequireRateLimiting("ApiGeneral");

        // ── Cartel Digital ──
        app.MapGet("/api/v1/cartel-digital", async (LetrerosHerreradbContext db) =>
        {
            // Ruta publica: no expone que empleado hizo el ultimo cambio.
            var cartel = await db.CartelDigital.AsNoTracking().FirstOrDefaultAsync();
            return cartel is null
                ? Results.NotFound()
                : Results.Ok(new { cartel.AbiertoEnLocal, cartel.MensajeEstado, cartel.TiempoEstimadoRegreso, cartel.FechaUltimaModificacion });
        })
        .WithTags("CartelDigital")
        .AllowAnonymous()
        .RequireRateLimiting("ApiGeneral");

        app.MapPut("/api/v1/cartel-digital", async (ActualizarCartelDigitalRequest req, ClaimsPrincipal user, LetrerosHerreradbContext db) =>
        {
            var cartel = await db.CartelDigital.FirstOrDefaultAsync();
            if (cartel is null) return Results.NotFound();
            cartel.AbiertoEnLocal = req.AbiertoEnLocal;
            cartel.MensajeEstado = req.MensajeEstado;
            cartel.TiempoEstimadoRegreso = req.TiempoEstimadoRegreso;
            cartel.IdEmpleadoModifica = user.IdUsuario();
            cartel.FechaUltimaModificacion = DateTime.Now;
            await db.SaveChangesAsync();
            return Results.Ok(cartel);
        })
        .WithTags("CartelDigital")
        .RequireAuthorization(p => p.RequireRole("dueño", "administrador"))
        .RequireRateLimiting("ApiGeneral");
    }

    private static bool EsPersonal(ClaimsPrincipal user) => Roles.Empleados.Any(user.IsInRole);

    private static IQueryable<Producto> Disponibles(this IQueryable<Producto> productos, bool incluirTodos) =>
        incluirTodos ? productos : productos.Where(p => p.Activo && p.Categoria!.Activo);

    // Un trabajo oculto o de un producto que ya no se vende no se muestra en la vitrina.
    private static IQueryable<Catalogo> Publicados(this IQueryable<Catalogo> catalogo, bool incluirTodos) =>
        incluirTodos ? catalogo : catalogo.Where(c => c.Visible && c.Producto!.Activo && c.Producto.Categoria!.Activo);
}

public record CrearClienteRequest(string? Rut, string Nombre, string Apellido, string Telefono, string? Email, string? Direccion, bool Activo = true);
public record ActualizarClienteRequest(string? Rut, string Nombre, string Apellido, string Telefono, string? Email, string? Direccion, bool Activo = true);
public record AsignarCredencialesRequest(string Password, string? PasswordActual);

public record CrearProductoRequest(int IdCategoria, string Nombre, string? Descripcion, decimal? PrecioBaseRef, bool Activo = true);
public record ActualizarProductoRequest(int IdCategoria, string Nombre, string? Descripcion, decimal? PrecioBaseRef, bool Activo = true);

public record CrearCategoriaRequest(string Nombre, string? Descripcion, bool Activo = true);
public record ActualizarCategoriaRequest(string Nombre, string? Descripcion, bool Activo = true);

public record CrearCatalogoRequest(int IdProducto, string Titulo, string Descripcion, string ImagenUrl, int? AnchoMm, int? AltoMm, decimal? PrecioReferencial, bool Visible = true);
public record ActualizarCatalogoRequest(int IdProducto, string Titulo, string Descripcion, string ImagenUrl, int? AnchoMm, int? AltoMm, decimal? PrecioReferencial, bool Visible = true);

public record ActualizarCartelDigitalRequest(bool AbiertoEnLocal, string MensajeEstado, string? TiempoEstimadoRegreso);
