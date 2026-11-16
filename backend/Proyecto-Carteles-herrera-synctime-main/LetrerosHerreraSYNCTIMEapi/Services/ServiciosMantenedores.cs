using System.Linq.Expressions;
using LetrerosHerreraSYNCTIMEapi.Datos;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.Extensions.Options;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 9: mantenedores (RFS07, RFU17, RFU18, E12-E15, RNFP10, RNFO08) ──
// Categorias, tipos de letrero (productos), materiales y personal. No hay borrado fisico: se desactiva, y
// lo desactivado deja de ofrecerse en los formularios. Editar o desactivar exige la version vista.

// Resultado comun de los servicios de la fase 9; la API lo traduce con RespuestasGestion.
public abstract record ResultadoGestion<T>
{
    public sealed record Correcto(T Valor) : ResultadoGestion<T>;
    public sealed record Invalido(IReadOnlyDictionary<string, string[]> Errores, string? Mensaje = null) : ResultadoGestion<T>;
    public sealed record NoEncontrado : ResultadoGestion<T>;
    // DesdeLaBase: la regla la hizo cumplir la base (nivel «base_de_datos» en la respuesta).
    public sealed record Conflicto(string Codigo, string Mensaje, bool DesdeLaBase = false) : ResultadoGestion<T>;
    public sealed record SinVersion : ResultadoGestion<T>;
    public sealed record VersionObsoleta : ResultadoGestion<T>;
    public sealed record DemasiadosIntentos(string Mensaje) : ResultadoGestion<T>;
    public sealed record NoDisponible(string Mensaje) : ResultadoGestion<T>;
    // La accion delicada pidio la contraseña de quien la hace y no era correcta (o no vino).
    public sealed record ReautenticacionFallida : ResultadoGestion<T>;
}

// Lo que tiene version: la API la envia como ETag.
public interface IConVersion
{
    byte[] RowVersion { get; }
}

public sealed record FichaCategoria(int IdCategoria, string Nombre, string? Descripcion, bool Activo, byte[] RowVersion) : IConVersion;

public sealed record CategoriaDelProducto(int IdCategoria, string Nombre, bool Activo);

public sealed record FichaProducto(int IdProducto, int IdCategoria, CategoriaDelProducto Categoria, string Nombre, string? Descripcion,
    decimal? PrecioBaseRef, bool Activo, byte[] RowVersion) : IConVersion;

public sealed record FichaMaterial(int IdMaterial, string Nombre, string UnidadMedida, decimal CostoUnitarioAprox, bool Activo, byte[] RowVersion) : IConVersion;

// Sin el hash de la contraseña ni nada del acceso con rostro.
public sealed record FichaEmpleado(int IdEmpleado, string Rut, string Nombre, string Apellido, string Telefono, string Email, string Rol,
    bool Activo, byte[] RowVersion) : IConVersion;

// La lista corta para elegir al responsable de un pedido (todo el personal la ve).
public sealed record EmpleadoActivo(int IdEmpleado, string Nombre, string Rol);

internal static class Versiones
{
    public static bool Iguales(byte[]? vista, byte[] actual) => vista is { Length: > 0 } && vista.AsSpan().SequenceEqual(actual);
}

// ── Categorias ──

public sealed class ServicioCategorias(LetrerosHerreradbContext db)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó esta categoría mientras la veías. Vuelve a cargarla para ver los cambios.";
    private const string IndiceNombre = "uq_categorias_nombre";

    private static readonly Dictionary<string, Expression<Func<Categoria, object?>>> Orden = new()
    {
        ["nombre"] = c => c.Nombre,
        ["id"] = c => c.IdCategoria
    };

    private static readonly Expression<Func<Categoria, FichaCategoria>> Ficha = c =>
        new FichaCategoria(c.IdCategoria, c.Nombre, c.Descripcion, c.Activo, c.RowVersion);

    public Task<ResultadoPaginado<FichaCategoria>> ListarAsync(ParametrosListado parametros, bool? activo, CancellationToken ct)
    {
        var consulta = db.Categorias.AsNoTracking();
        if (activo is bool a)
            consulta = consulta.Where(c => c.Activo == a);
        if (parametros.Buscar is { } q)
            consulta = consulta.Where(c => c.Nombre.Contains(q) || (c.Descripcion != null && c.Descripcion.Contains(q)));
        return consulta.Ordenar(parametros.Orden, Orden, "nombre", c => c.IdCategoria).Select(Ficha).PaginarAsync(parametros, ct);
    }

    public Task<FichaCategoria?> ObtenerAsync(int id, bool incluirInactivas, CancellationToken ct) =>
        db.Categorias.AsNoTracking().Where(c => c.IdCategoria == id && (incluirInactivas || c.Activo)).Select(Ficha).FirstOrDefaultAsync(ct);

    public async Task<ResultadoGestion<FichaCategoria>> CrearAsync(EntradaCategoria entrada, bool activo, CancellationToken ct)
    {
        var errores = ValidacionMantenedores.Categoria(entrada);
        if (errores.Count > 0)
            return new ResultadoGestion<FichaCategoria>.Invalido(errores);
        var nombre = entrada.Nombre!.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre, ct))
            return YaExiste();

        var categoria = new Categoria { Nombre = nombre, Descripcion = ValidacionMantenedores.Limpiar(entrada.Descripcion), Activo = activo };
        db.Categorias.Add(categoria);
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaCategoria>.Correcto((await ObtenerAsync(categoria.IdCategoria, true, CancellationToken.None))!);
    }

    public async Task<ResultadoGestion<FichaCategoria>> EditarAsync(int id, EntradaCategoria entrada, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaCategoria>.SinVersion();
        var errores = ValidacionMantenedores.Categoria(entrada);
        if (errores.Count > 0)
            return new ResultadoGestion<FichaCategoria>.Invalido(errores);
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id, ct);
        if (categoria is null)
            return new ResultadoGestion<FichaCategoria>.NoEncontrado();
        if (!Versiones.Iguales(version, categoria.RowVersion))
            return new ResultadoGestion<FichaCategoria>.VersionObsoleta();
        var nombre = entrada.Nombre!.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre && c.IdCategoria != id, ct))
            return YaExiste();

        categoria.Nombre = nombre;
        categoria.Descripcion = ValidacionMantenedores.Limpiar(entrada.Descripcion);
        db.Entry(categoria).Property(c => c.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaCategoria>.Correcto((await ObtenerAsync(id, true, CancellationToken.None))!);
    }

    // Una categoria desactivada oculta sus tipos de letrero en la portada y en las solicitudes.
    public async Task<ResultadoGestion<FichaCategoria>> CambiarActivoAsync(int id, bool activo, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaCategoria>.SinVersion();
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.IdCategoria == id, ct);
        if (categoria is null)
            return new ResultadoGestion<FichaCategoria>.NoEncontrado();
        if (!Versiones.Iguales(version, categoria.RowVersion))
            return new ResultadoGestion<FichaCategoria>.VersionObsoleta();
        categoria.Activo = activo;
        db.Entry(categoria).Property(c => c.Activo).IsModified = true;
        db.Entry(categoria).Property(c => c.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaCategoria>.Correcto((await ObtenerAsync(id, true, CancellationToken.None))!);
    }

    private static ResultadoGestion<FichaCategoria> YaExiste(bool desdeLaBase = false) =>
        new ResultadoGestion<FichaCategoria>.Conflicto(CodigosMantenedores.YaExiste, "Ya existe una categoría con ese nombre.", desdeLaBase);

    private async Task<ResultadoGestion<FichaCategoria>?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<FichaCategoria>.VersionObsoleta();
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceNombre))
        {
            return YaExiste(desdeLaBase: true);
        }
    }
}

// ── Tipos de letrero (productos) ──

public sealed class ServicioProductos(LetrerosHerreradbContext db)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó este tipo de letrero mientras lo veías. Vuelve a cargarlo para ver los cambios.";
    private const string IndiceNombre = "uq_productos_nombre";

    private static readonly Dictionary<string, Expression<Func<Producto, object?>>> Orden = new()
    {
        ["nombre"] = p => p.Nombre,
        ["categoria"] = p => p.Categoria!.Nombre,
        ["precio"] = p => p.PrecioBaseRef,
        ["id"] = p => p.IdProducto
    };

    private static readonly Expression<Func<Producto, FichaProducto>> Ficha = p => new FichaProducto(
        p.IdProducto, p.IdCategoria, new CategoriaDelProducto(p.Categoria!.IdCategoria, p.Categoria.Nombre, p.Categoria.Activo),
        p.Nombre, p.Descripcion, p.PrecioBaseRef, p.Activo, p.RowVersion);

    // Quien no es de gestion solo ve lo que se ofrece: activo y de una categoria activa.
    public Task<ResultadoPaginado<FichaProducto>> ListarAsync(ParametrosListado parametros, bool? activo, bool soloOfrecidos, int? idCategoria,
        CancellationToken ct)
    {
        var consulta = db.Productos.AsNoTracking();
        if (soloOfrecidos)
            consulta = consulta.Where(p => p.Activo && p.Categoria!.Activo);
        else if (activo is bool a)
            consulta = consulta.Where(p => p.Activo == a);
        if (idCategoria is int categoria)
            consulta = consulta.Where(p => p.IdCategoria == categoria);
        if (parametros.Buscar is { } q)
            consulta = consulta.Where(p => p.Nombre.Contains(q) || p.Categoria!.Nombre.Contains(q));
        return consulta.Ordenar(parametros.Orden, Orden, "nombre", p => p.IdProducto).Select(Ficha).PaginarAsync(parametros, ct);
    }

    public Task<FichaProducto?> ObtenerAsync(int id, bool soloOfrecidos, CancellationToken ct) =>
        db.Productos.AsNoTracking()
            .Where(p => p.IdProducto == id && (!soloOfrecidos || (p.Activo && p.Categoria!.Activo)))
            .Select(Ficha).FirstOrDefaultAsync(ct);

    public async Task<ResultadoGestion<FichaProducto>> CrearAsync(EntradaProducto entrada, bool activo, CancellationToken ct)
    {
        var errores = ValidacionMantenedores.Producto(entrada);
        if (errores.Count == 0 && !await db.Categorias.AnyAsync(c => c.IdCategoria == entrada.IdCategoria && c.Activo, ct))
            errores["idCategoria"] = ["La categoría no existe o está desactivada."];
        if (errores.Count > 0)
            return new ResultadoGestion<FichaProducto>.Invalido(errores);
        var nombre = entrada.Nombre!.Trim();
        if (await db.Productos.AnyAsync(p => p.Nombre == nombre, ct))
            return YaExiste();

        var producto = new Producto
        {
            IdCategoria = entrada.IdCategoria!.Value,
            Nombre = nombre,
            Descripcion = ValidacionMantenedores.Limpiar(entrada.Descripcion),
            PrecioBaseRef = entrada.PrecioBaseRef,
            Activo = activo
        };
        db.Productos.Add(producto);
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaProducto>.Correcto((await ObtenerAsync(producto.IdProducto, false, CancellationToken.None))!);
    }

    public async Task<ResultadoGestion<FichaProducto>> EditarAsync(int id, EntradaProducto entrada, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaProducto>.SinVersion();
        var errores = ValidacionMantenedores.Producto(entrada);
        if (errores.Count > 0)
            return new ResultadoGestion<FichaProducto>.Invalido(errores);
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id, ct);
        if (producto is null)
            return new ResultadoGestion<FichaProducto>.NoEncontrado();
        if (!Versiones.Iguales(version, producto.RowVersion))
            return new ResultadoGestion<FichaProducto>.VersionObsoleta();
        // Se puede conservar una categoria que se desactivo despues, pero no pasar a una desactivada.
        if (entrada.IdCategoria != producto.IdCategoria && !await db.Categorias.AnyAsync(c => c.IdCategoria == entrada.IdCategoria && c.Activo, ct))
            return new ResultadoGestion<FichaProducto>.Invalido(new Dictionary<string, string[]> { ["idCategoria"] = ["La categoría no existe o está desactivada."] });
        var nombre = entrada.Nombre!.Trim();
        if (await db.Productos.AnyAsync(p => p.Nombre == nombre && p.IdProducto != id, ct))
            return YaExiste();

        producto.IdCategoria = entrada.IdCategoria!.Value;
        producto.Nombre = nombre;
        producto.Descripcion = ValidacionMantenedores.Limpiar(entrada.Descripcion);
        producto.PrecioBaseRef = entrada.PrecioBaseRef;
        db.Entry(producto).Property(p => p.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaProducto>.Correcto((await ObtenerAsync(id, false, CancellationToken.None))!);
    }

    public async Task<ResultadoGestion<FichaProducto>> CambiarActivoAsync(int id, bool activo, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaProducto>.SinVersion();
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id, ct);
        if (producto is null)
            return new ResultadoGestion<FichaProducto>.NoEncontrado();
        if (!Versiones.Iguales(version, producto.RowVersion))
            return new ResultadoGestion<FichaProducto>.VersionObsoleta();
        producto.Activo = activo;
        db.Entry(producto).Property(p => p.Activo).IsModified = true;
        db.Entry(producto).Property(p => p.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaProducto>.Correcto((await ObtenerAsync(id, false, CancellationToken.None))!);
    }

    // La base no admite dos tipos de letrero con el mismo nombre, aunque sean de categorias distintas.
    private static ResultadoGestion<FichaProducto> YaExiste(bool desdeLaBase = false) =>
        new ResultadoGestion<FichaProducto>.Conflicto(CodigosMantenedores.YaExiste, "Ya existe un tipo de letrero con ese nombre.", desdeLaBase);

    private async Task<ResultadoGestion<FichaProducto>?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<FichaProducto>.VersionObsoleta();
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceNombre))
        {
            return YaExiste(desdeLaBase: true);
        }
    }
}

// ── Materiales ──

public sealed class ServicioMateriales(LetrerosHerreradbContext db)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó este material mientras lo veías. Vuelve a cargarlo para ver los cambios.";
    private const string IndiceNombre = "uq_materiales_nombre";

    private static readonly Dictionary<string, Expression<Func<Material, object?>>> Orden = new()
    {
        ["nombre"] = m => m.Nombre,
        ["costo"] = m => m.CostoUnitarioAprox,
        ["id"] = m => m.IdMaterial
    };

    private static readonly Expression<Func<Material, FichaMaterial>> Ficha = m =>
        new FichaMaterial(m.IdMaterial, m.Nombre, m.UnidadMedida, m.CostoUnitarioAprox, m.Activo, m.RowVersion);

    public Task<ResultadoPaginado<FichaMaterial>> ListarAsync(ParametrosListado parametros, bool? activo, CancellationToken ct)
    {
        var consulta = db.Materiales.AsNoTracking();
        if (activo is bool a)
            consulta = consulta.Where(m => m.Activo == a);
        if (parametros.Buscar is { } q)
            consulta = consulta.Where(m => m.Nombre.Contains(q));
        return consulta.Ordenar(parametros.Orden, Orden, "nombre", m => m.IdMaterial).Select(Ficha).PaginarAsync(parametros, ct);
    }

    public Task<FichaMaterial?> ObtenerAsync(int id, bool incluirInactivos, CancellationToken ct) =>
        db.Materiales.AsNoTracking().Where(m => m.IdMaterial == id && (incluirInactivos || m.Activo)).Select(Ficha).FirstOrDefaultAsync(ct);

    public async Task<ResultadoGestion<FichaMaterial>> CrearAsync(EntradaMaterialMantenedor entrada, CancellationToken ct)
    {
        var errores = ValidacionMantenedores.Material(entrada);
        if (errores.Count > 0)
            return new ResultadoGestion<FichaMaterial>.Invalido(errores);
        var nombre = entrada.Nombre!.Trim();
        if (await db.Materiales.AnyAsync(m => m.Nombre == nombre, ct))
            return YaExiste();

        var material = new Material { Nombre = nombre, UnidadMedida = entrada.UnidadMedida!.Trim(), CostoUnitarioAprox = entrada.CostoUnitarioAprox!.Value };
        db.Materiales.Add(material);
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaMaterial>.Correcto((await ObtenerAsync(material.IdMaterial, true, CancellationToken.None))!);
    }

    // El costo nuevo solo lo sugiere el editor de cotizaciones: las ya emitidas guardan el suyo.
    public async Task<ResultadoGestion<FichaMaterial>> EditarAsync(int id, EntradaMaterialMantenedor entrada, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaMaterial>.SinVersion();
        var errores = ValidacionMantenedores.Material(entrada);
        if (errores.Count > 0)
            return new ResultadoGestion<FichaMaterial>.Invalido(errores);
        var material = await db.Materiales.FirstOrDefaultAsync(m => m.IdMaterial == id, ct);
        if (material is null)
            return new ResultadoGestion<FichaMaterial>.NoEncontrado();
        if (!Versiones.Iguales(version, material.RowVersion))
            return new ResultadoGestion<FichaMaterial>.VersionObsoleta();
        var nombre = entrada.Nombre!.Trim();
        if (await db.Materiales.AnyAsync(m => m.Nombre == nombre && m.IdMaterial != id, ct))
            return YaExiste();

        material.Nombre = nombre;
        material.UnidadMedida = entrada.UnidadMedida!.Trim();
        material.CostoUnitarioAprox = entrada.CostoUnitarioAprox!.Value;
        db.Entry(material).Property(m => m.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaMaterial>.Correcto((await ObtenerAsync(id, true, CancellationToken.None))!);
    }

    public async Task<ResultadoGestion<FichaMaterial>> CambiarActivoAsync(int id, bool activo, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaMaterial>.SinVersion();
        var material = await db.Materiales.FirstOrDefaultAsync(m => m.IdMaterial == id, ct);
        if (material is null)
            return new ResultadoGestion<FichaMaterial>.NoEncontrado();
        if (!Versiones.Iguales(version, material.RowVersion))
            return new ResultadoGestion<FichaMaterial>.VersionObsoleta();
        material.Activo = activo;
        db.Entry(material).Property(m => m.Activo).IsModified = true;
        db.Entry(material).Property(m => m.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaMaterial>.Correcto((await ObtenerAsync(id, true, CancellationToken.None))!);
    }

    private static ResultadoGestion<FichaMaterial> YaExiste(bool desdeLaBase = false) =>
        new ResultadoGestion<FichaMaterial>.Conflicto(CodigosMantenedores.YaExiste, "Ya existe un material con ese nombre.", desdeLaBase);

    private async Task<ResultadoGestion<FichaMaterial>?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<FichaMaterial>.VersionObsoleta();
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceNombre))
        {
            return YaExiste(desdeLaBase: true);
        }
    }
}

// ── Personal (E15, RFU18, RNFO08) ──

public sealed record EntradaAltaEmpleado(EntradaEmpleado Datos, string? Password);

public sealed class ServicioEmpleados(
    LetrerosHerreradbContext db,
    AuthService auth,
    ServicioSesiones sesiones,
    ServicioCodigosCuenta codigos,
    ServicioReautenticacion reautenticacion,
    ColaEnTransaccion cola,
    Datos.Auditoria.IAuditoria auditoria,
    IOptions<OpcionesCorreo> opcionesCorreo,
    IConfiguration configuracion,
    TimeProvider reloj)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó los datos de esta persona mientras los veías. Vuelve a cargarlos para ver los cambios.";
    private const string IndiceRut = "uq_empleados_rut";
    private const string IndiceEmail = "uq_empleados_email";
    // Pone en fila los cambios de rol y las desactivaciones: dos personas no pueden quitar a la vez a los
    // dos ultimos dueños.
    private const string BloqueoDuenos = "empleados_duenos";

    private static readonly Dictionary<string, Expression<Func<Empleado, object?>>> Orden = new()
    {
        ["nombre"] = e => e.Nombre,
        ["apellido"] = e => e.Apellido,
        ["rol"] = e => e.Rol,
        ["id"] = e => e.IdEmpleado
    };

    private static readonly Expression<Func<Empleado, FichaEmpleado>> Ficha = e =>
        new FichaEmpleado(e.IdEmpleado, e.Rut, e.Nombre, e.Apellido, e.Telefono, e.Email, e.Rol, e.Activo, e.RowVersion);

    public Task<ResultadoPaginado<FichaEmpleado>> ListarAsync(ParametrosListado parametros, bool? activo, CancellationToken ct)
    {
        var consulta = db.Empleados.AsNoTracking();
        if (activo is bool a)
            consulta = consulta.Where(e => e.Activo == a);
        if (parametros.Buscar is { } q)
        {
            var rut = q.Replace(".", "").Trim().ToUpperInvariant();
            consulta = consulta.Where(e => (e.Nombre + " " + e.Apellido).Contains(q) || e.Email.Contains(q) || e.Rut.Contains(rut));
        }
        return consulta.Ordenar(parametros.Orden, Orden, "nombre", e => e.IdEmpleado).Select(Ficha).PaginarAsync(parametros, ct);
    }

    public Task<FichaEmpleado?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == id).Select(Ficha).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EmpleadoActivo>> ActivosAsync(CancellationToken ct) =>
        await db.Empleados.AsNoTracking().Where(e => e.Activo)
            .OrderBy(e => e.Nombre).ThenBy(e => e.Apellido).ThenBy(e => e.IdEmpleado)
            .Select(e => new EmpleadoActivo(e.IdEmpleado, e.Nombre + " " + e.Apellido, e.Rol))
            .ToListAsync(ct);

    // La contraseña inicial la escribe quien da el alta (no el sistema). Quien da el alta ya confirmo la suya.
    public async Task<ResultadoGestion<FichaEmpleado>> CrearAsync(EntradaAltaEmpleado entrada, CancellationToken ct)
    {
        var errores = ValidacionMantenedores.Empleado(entrada.Datos, RolesValidos);
        if (!AuthService.EsPasswordValida(entrada.Password))
            errores["password"] = [AuthService.MensajePasswordInvalida];
        if (errores.Count > 0)
            return new ResultadoGestion<FichaEmpleado>.Invalido(errores);

        var datos = Normalizar(entrada.Datos);
        if (await DuplicadoAsync(datos, idPropio: null, ct) is { } duplicado)
            return duplicado;
        var empleado = new Empleado
        {
            Rut = datos.Rut!, Nombre = datos.Nombre!, Apellido = datos.Apellido!, Telefono = datos.Telefono!, Email = datos.Email!,
            Rol = datos.Rol!, PasswordHash = auth.HashPassword(entrada.Password!)
        };
        db.Empleados.Add(empleado);
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<FichaEmpleado>.Correcto((await ObtenerAsync(empleado.IdEmpleado, CancellationToken.None))!);
    }

    // Cambiar el rol cierra sus sesiones: el nuevo rol rige desde su proximo ingreso. Revision de seguridad:
    // cambiar el correo de ingreso o el rol exige la contraseña de quien lo hace (con el correo se recupera la
    // cuenta, asi que cambiarlo equivale a tomarla). Si cambia el correo, ademas se cierran sus sesiones, se
    // anulan sus codigos de recuperacion pendientes y el correo anterior recibe el aviso, en la misma transaccion.
    public async Task<ResultadoGestion<FichaEmpleado>> EditarAsync(int idQuienCambia, int id, EntradaEmpleado entrada, byte[]? version,
        string? passwordActual, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaEmpleado>.SinVersion();
        var errores = ValidacionMantenedores.Empleado(entrada, RolesValidos);
        if (errores.Count > 0)
            return new ResultadoGestion<FichaEmpleado>.Invalido(errores);
        var datos = Normalizar(entrada);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, BloqueoDuenos, BloqueoDeAplicacion.Espera(configuracion), ct);
        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.IdEmpleado == id, ct);
        if (empleado is null)
            return new ResultadoGestion<FichaEmpleado>.NoEncontrado();
        if (!Versiones.Iguales(version, empleado.RowVersion))
            return new ResultadoGestion<FichaEmpleado>.VersionObsoleta();
        if (await ReglaAsync(idQuienCambia, empleado, datos.Rol!, empleado.Activo, ct) is { } regla)
            return regla;
        if (await DuplicadoAsync(datos, id, ct) is { } duplicado)
            return duplicado;

        var cambiaRol = empleado.Rol != datos.Rol;
        var correoAnterior = empleado.Email;
        var cambiaCorreo = !string.Equals(correoAnterior, datos.Email, StringComparison.OrdinalIgnoreCase);
        if ((cambiaRol || cambiaCorreo) && !await reautenticacion.ConfirmarAsync(idQuienCambia, passwordActual, ct))
            return new ResultadoGestion<FichaEmpleado>.ReautenticacionFallida();
        empleado.Rut = datos.Rut!;
        empleado.Nombre = datos.Nombre!;
        empleado.Apellido = datos.Apellido!;
        empleado.Telefono = datos.Telefono!;
        empleado.Email = datos.Email!;
        empleado.Rol = datos.Rol!;
        db.Entry(empleado).Property(e => e.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        if (cambiaRol || cambiaCorreo)
            await sesiones.RevocarTodasAsync(TitularSesion.DeEmpleado(id), ct);
        if (cambiaCorreo)
        {
            await codigos.AnularPendientesAsync(TitularSesion.DeEmpleado(id), ct);
            if (Reglas.EsCorreo(correoAnterior))
                await cola.EncolarAsync(CorreosDeCuenta.AvisoCambioCorreoPersonal(correoAnterior, empleado.Nombre,
                    CorreosDestinosAvisos.Enmascarar(empleado.Email), opcionesCorreo.Value.ContactoTaller), ct);
            await db.SaveChangesAsync(ct);
        }
        await transaccion.CommitAsync(CancellationToken.None);
        if (cambiaCorreo)
            cola.AvisarAlTrabajador();
        return new ResultadoGestion<FichaEmpleado>.Correcto((await ObtenerAsync(id, CancellationToken.None))!);
    }

    // Desactivar es un solo paso (una transaccion): activo = false, se borran su rostro y su descriptor, y se
    // cierran todas sus sesiones. Si algo falla, no cambia nada. Reactivar no le devuelve el rostro.
    public async Task<ResultadoGestion<FichaEmpleado>> CambiarActivoAsync(int idQuienCambia, int id, bool activo, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaEmpleado>.SinVersion();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, BloqueoDuenos, BloqueoDeAplicacion.Espera(configuracion), ct);
        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.IdEmpleado == id, ct);
        if (empleado is null)
            return new ResultadoGestion<FichaEmpleado>.NoEncontrado();
        if (!Versiones.Iguales(version, empleado.RowVersion))
            return new ResultadoGestion<FichaEmpleado>.VersionObsoleta();
        if (await ReglaAsync(idQuienCambia, empleado, empleado.Rol, activo, ct) is { } regla)
            return regla;

        var seDesactiva = empleado.Activo && !activo;
        empleado.Activo = activo;
        db.Entry(empleado).Property(e => e.Activo).IsModified = true;
        db.Entry(empleado).Property(e => e.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        if (seDesactiva)
        {
            var ahora = reloj.GetUtcNow();
            // Fase 10: tambien las tres muestras (DatosRostro.Borrado borra las cuatro columnas cifradas). Fase 11: queda
            // en la bitacora, en la misma transaccion (el interceptor no ve el ExecuteUpdate).
            var idRostro = await db.RostrosEmpleado.AsNoTracking().Where(r => r.IdEmpleado == id && r.FechaRevocacion == null)
                .Select(r => (int?)r.IdRostro).FirstOrDefaultAsync(ct);
            if (idRostro is int rostro && await db.RostrosEmpleado
                    .Where(r => r.IdRostro == rostro && r.FechaRevocacion == null)
                    .ExecuteUpdateAsync(DatosRostro.Borrado(ahora), ct) > 0)
                await auditoria.RegistrarAsync("rostro_empleado", rostro.ToString(System.Globalization.CultureInfo.InvariantCulture), Models.AccionesAuditoria.Modificar,
                    Datos.Auditoria.DetalleAuditoria.Libre(new Dictionary<string, object?>
                    {
                        ["campos"] = new[] { "descriptorCifrado", "descriptorFrente", "descriptorIzquierda", "descriptorDerecha", "tokenEquipoHash", "fechaRevocacion" },
                        ["motivo"] = "empleado desactivado"
                    }), ct);
            await sesiones.RevocarTodasAsync(TitularSesion.DeEmpleado(id), ct);
        }
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoGestion<FichaEmpleado>.Correcto((await ObtenerAsync(id, CancellationToken.None))!);
    }

    private static readonly string[] RolesValidos = Security.Roles.Empleados;

    private async Task<ResultadoGestion<FichaEmpleado>?> ReglaAsync(int idQuienCambia, Empleado empleado, string rolNuevo, bool activoNuevo, CancellationToken ct)
    {
        var duenos = await db.Empleados.CountAsync(e => e.Activo && e.Rol == Security.Roles.Dueno, ct);
        return ReglasPersonal.RevisarCambio(idQuienCambia, empleado.IdEmpleado, empleado.Rol, empleado.Activo, rolNuevo, activoNuevo, duenos, Security.Roles.Dueno) switch
        {
            CodigosMantenedores.Propio => new ResultadoGestion<FichaEmpleado>.Conflicto(CodigosMantenedores.Propio, ReglasPersonal.MensajePropio),
            CodigosMantenedores.UltimoDueno => new ResultadoGestion<FichaEmpleado>.Conflicto(CodigosMantenedores.UltimoDueno, ReglasPersonal.MensajeUltimoDueno),
            _ => null
        };
    }

    private static EntradaEmpleado Normalizar(EntradaEmpleado e)
    {
        Rut.TryNormalizar(e.Rut, out var rut);
        return new EntradaEmpleado(rut, e.Nombre!.Trim(), e.Apellido!.Trim(), ValidacionCuenta.NormalizarTelefono(e.Telefono),
            ValidacionMantenedores.NormalizarCorreo(e.Email), e.Rol);
    }

    private async Task<ResultadoGestion<FichaEmpleado>?> DuplicadoAsync(EntradaEmpleado datos, int? idPropio, CancellationToken ct)
    {
        if (await db.Empleados.AnyAsync(e => e.Email == datos.Email && e.IdEmpleado != idPropio, ct))
            return new ResultadoGestion<FichaEmpleado>.Conflicto(CodigosMantenedores.YaExiste, "Ya existe una persona del personal con ese correo.");
        if (await db.Empleados.AnyAsync(e => e.Rut == datos.Rut && e.IdEmpleado != idPropio, ct))
            return new ResultadoGestion<FichaEmpleado>.Conflicto(CodigosMantenedores.YaExiste, "Ya existe una persona del personal con ese RUT.");
        return null;
    }

    private async Task<ResultadoGestion<FichaEmpleado>?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<FichaEmpleado>.VersionObsoleta();
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceRut, IndiceEmail))
        {
            return new ResultadoGestion<FichaEmpleado>.Conflicto(CodigosMantenedores.YaExiste, "Ya existe una persona del personal con ese RUT o correo.", DesdeLaBase: true);
        }
    }
}

// ── Reautenticacion para las acciones delicadas (dar de alta personal, dar acceso web a un cliente) ──

public sealed class ServicioReautenticacion(LetrerosHerreradbContext db, AuthService auth)
{
    // true si el empleado con sesion esta activo y su contraseña es correcta.
    public async Task<bool> ConfirmarAsync(int idEmpleado, string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password))
            return false;
        var hash = await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idEmpleado && e.Activo).Select(e => e.PasswordHash).FirstOrDefaultAsync(ct);
        return hash is not null && auth.VerifyPassword(password, hash);
    }
}
