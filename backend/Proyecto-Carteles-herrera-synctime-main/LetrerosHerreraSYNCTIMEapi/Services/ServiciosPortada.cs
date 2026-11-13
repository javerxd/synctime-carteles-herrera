using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Portada;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Vitrina (RFS01, RNFP32) ──

public sealed record CategoriaVitrina(int IdCategoria, string Nombre);
public sealed record ImagenVitrina(string UrlGrande, string UrlMiniatura, int Ancho, int Alto, bool EsEjemplo);

// Lo que el personal ve de cada trabajo. No incluye quien lo publico ni quien autorizo el logo.
public sealed record TrabajoVitrina(
    int IdCatalogo,
    string Titulo,
    string Descripcion,
    int IdProducto,
    string TipoLetrero,
    CategoriaVitrina Categoria,
    int? AnchoMm,
    int? AltoMm,
    decimal? PrecioReferencial,
    ImagenVitrina? Imagen,
    DateTime FechaPublicacion,
    bool Visible,
    bool IncluyeLogoCliente,
    bool LogoAutorizado)
{
    // Para visitantes: sin los datos de gestion (si esta oculto o si lleva el logo de un cliente).
    public TrabajoPublico ParaPublico() =>
        new(IdCatalogo, Titulo, Descripcion, IdProducto, TipoLetrero, Categoria, AnchoMm, AltoMm, PrecioReferencial, Imagen, FechaPublicacion);
}

public sealed record TrabajoPublico(
    int IdCatalogo,
    string Titulo,
    string Descripcion,
    int IdProducto,
    string TipoLetrero,
    CategoriaVitrina Categoria,
    int? AnchoMm,
    int? AltoMm,
    decimal? PrecioReferencial,
    ImagenVitrina? Imagen,
    DateTime FechaPublicacion);

public sealed record ResultadoAutorizacionLogo(TrabajoVitrina? Trabajo, string? Error);

public sealed class ServicioVitrina(LetrerosHerreradbContext db, TimeProvider reloj)
{
    public const string MensajeLogo =
        "Una publicación con el logotipo de un cliente necesita su autorización registrada antes de mostrarse. Guárdala oculta y registra la autorización.";

    // Un trabajo oculto, o de un tipo de letrero que ya no se ofrece, no se muestra a los visitantes.
    private IQueryable<Catalogo> Base(bool incluirOcultos) =>
        incluirOcultos
            ? db.Catalogos.AsNoTracking()
            : db.Catalogos.AsNoTracking().Where(c => c.Visible && c.Producto!.Activo && c.Producto.Categoria!.Activo);

    private static IQueryable<TrabajoVitrina> Proyectar(IQueryable<Catalogo> consulta) =>
        consulta.Select(c => new TrabajoVitrina(
            c.IdCatalogo,
            c.Titulo,
            c.Descripcion,
            c.IdProducto,
            c.Producto!.Nombre,
            new CategoriaVitrina(c.Producto.Categoria!.IdCategoria, c.Producto.Categoria.Nombre),
            c.AnchoMm,
            c.AltoMm,
            c.PrecioReferencial,
            c.Imagen == null
                ? null
                : new ImagenVitrina(
                    ServicioImagenes.RutaBase + "/" + c.Imagen.Clave + "-grande.webp",
                    ServicioImagenes.RutaBase + "/" + c.Imagen.Clave + "-miniatura.webp",
                    c.Imagen.Ancho,
                    c.Imagen.Alto,
                    c.Imagen.EsEjemplo),
            c.FechaPublicacion,
            c.Visible,
            c.IncluyeLogoCliente,
            c.FechaAutorizacionLogo != null));

    public async Task<ResultadoPaginado<TrabajoVitrina>> ListarAsync(int? idCategoria, ParametrosListado parametros, bool incluirOcultos, CancellationToken ct)
    {
        var consulta = Base(incluirOcultos);
        if (idCategoria is int categoria)
            consulta = consulta.Where(c => c.Producto!.IdCategoria == categoria);

        var total = await consulta.CountAsync(ct);
        var elementos = await Proyectar(consulta
                .OrderByDescending(c => c.FechaPublicacion).ThenByDescending(c => c.IdCatalogo)
                .Skip(parametros.Saltar).Take(parametros.Tamano))
            .ToListAsync(ct);
        return new ResultadoPaginado<TrabajoVitrina>(elementos, total, parametros.Pagina, parametros.Tamano);
    }

    public Task<TrabajoVitrina?> ObtenerAsync(int id, bool incluirOcultos, CancellationToken ct) =>
        Proyectar(Base(incluirOcultos).Where(c => c.IdCatalogo == id)).FirstOrDefaultAsync(ct);

    public const int LargoTitulo = 120;
    public const int LargoDescripcion = 500;
    public const int LargoImagenUrl = 500;

    // null si la publicacion se puede guardar asi; si no, el motivo en español. Revisa lo mismo que las
    // restricciones de la tabla, para responder 400 con el motivo en vez de un error de la base.
    public async Task<string?> RevisarAsync(DatosPublicacion datos, bool logoAutorizado, CancellationToken ct)
    {
        var titulo = datos.Titulo?.Trim() ?? "";
        if (titulo.Length == 0 || titulo.Length > LargoTitulo)
            return $"El título es obligatorio y puede tener hasta {LargoTitulo} caracteres.";
        if ((datos.Descripcion?.Length ?? 0) > LargoDescripcion)
            return $"La descripción puede tener hasta {LargoDescripcion} caracteres.";
        if ((datos.ImagenUrl?.Length ?? 0) > LargoImagenUrl)
            return $"La dirección de la imagen puede tener hasta {LargoImagenUrl} caracteres.";
        if ((datos.AnchoMm is null) != (datos.AltoMm is null) || datos.AnchoMm <= 0 || datos.AltoMm <= 0)
            return "Indica el ancho y el alto en milímetros (mayores que cero), o deja los dos vacíos.";
        if (datos.PrecioReferencial is decimal precio && !Reglas.EsMontoEnPesos(precio))
            return "El precio de referencia debe ser un monto en pesos, entero y no negativo.";
        if (!await db.Productos.AnyAsync(p => p.IdProducto == datos.IdProducto, ct))
            return "El tipo de letrero indicado no existe.";

        var (idImagen, visible, incluyeLogoCliente) = (datos.IdImagen, datos.Visible, datos.IncluyeLogoCliente);
        if (idImagen is int imagen && !await db.Imagenes.AnyAsync(i => i.IdImagen == imagen, ct))
            return "La imagen indicada no existe. Súbela primero en /api/v1/imagenes.";
        if (visible && incluyeLogoCliente && !logoAutorizado)
            return MensajeLogo;
        return null;
    }

    // Registra (o retira) la autorizacion del cliente para mostrar su logo. Al retirarla, una publicacion
    // con logo deja de verse en el mismo paso. Registrarla de nuevo no cambia quien la dio ni cuando.
    public async Task<ResultadoAutorizacionLogo> AutorizarLogoAsync(int id, int idEmpleado, bool autorizado, CancellationToken ct)
    {
        var item = await db.Catalogos.FirstOrDefaultAsync(c => c.IdCatalogo == id, ct);
        if (item is null)
            return new(null, null);

        if (autorizado && !item.IncluyeLogoCliente)
            return new(null, "Esta publicación no está marcada con el logotipo de un cliente: no necesita autorización.");

        if (autorizado && item.FechaAutorizacionLogo is not null)
        {
            // Ya estaba registrada: se conserva el registro original.
        }
        else if (autorizado)
        {
            item.IdEmpleadoAutorizaLogo = idEmpleado;
            item.FechaAutorizacionLogo = reloj.GetUtcNow();
        }
        else
        {
            item.IdEmpleadoAutorizaLogo = null;
            item.FechaAutorizacionLogo = null;
            if (item.IncluyeLogoCliente)
                item.Visible = false;
        }
        await db.SaveChangesAsync(ct);
        return new(await ObtenerAsync(id, incluirOcultos: true, ct), null);
    }
}

// Lo que el personal envia al crear o editar una publicacion.
public sealed record DatosPublicacion(
    int IdProducto, string? Titulo, string? Descripcion, string? ImagenUrl, int? AnchoMm, int? AltoMm,
    decimal? PrecioReferencial, bool Visible, int? IdImagen, bool IncluyeLogoCliente);

// ── Estado del local (RFS02) ──

public sealed class ServicioEstadoLocal(LetrerosHerreradbContext db, TimeProvider reloj)
{
    public async Task<EstadoLocalAhora> ObtenerAsync(CancellationToken ct)
    {
        var semana = await db.HorariosAtencion.AsNoTracking()
            .OrderBy(h => h.DiaSemana)
            .Select(h => new FranjaHorario(h.DiaSemana, h.NombreDia, h.Abierto, h.HoraInicio, h.HoraFin))
            .ToListAsync(ct);
        var cartel = await db.CartelDigital.AsNoTracking()
            .OrderBy(c => c.IdCartel)
            .Select(c => new EstadoCartelLocal(c.AbiertoEnLocal, c.MensajeEstado, c.TiempoEstimadoRegreso, c.FechaUltimaModificacion))
            .FirstOrDefaultAsync(ct);
        return ReglaLocalAbierto.Calcular(semana, cartel, HoraChile.Convertir(reloj.GetUtcNow()));
    }
}

// ── Formulario de contacto (RFS08, RFS12, E22) ──

public abstract record ResultadoContacto
{
    public sealed record Recibida(int IdConsulta) : ResultadoContacto;
    public sealed record Invalida(IReadOnlyDictionary<string, string[]> Errores) : ResultadoContacto;
    public sealed record VersionVencida(string Mensaje) : ResultadoContacto;
}

// Guarda la consulta y su consentimiento de privacidad en una sola transaccion (si uno falla, no queda
// ninguno) y despues deja el aviso en la cola de correo, que nunca hace esperar ni falla la peticion.
public sealed class ServicioContacto(
    LetrerosHerreradbContext db,
    IServicioConsentimientos consentimientos,
    IAvisosDelTaller avisos,
    TimeProvider reloj)
{
    public async Task<ResultadoContacto> RecibirAsync(ConsultaContactoEntrada entrada, string? versionPrivacidad, CancellationToken ct)
    {
        var errores = ValidacionContacto.Validar(entrada);
        if (errores.Count > 0)
            return new ResultadoContacto.Invalida(errores);

        if (versionPrivacidad != DocumentosLegales.VersionVigente(DocumentosLegales.Privacidad))
            return new ResultadoContacto.VersionVencida("La política de privacidad cambió y la versión que aceptaste ya no es la vigente. Vuelve a leerla y envía de nuevo.");

        var ahora = reloj.GetUtcNow();
        var horaChile = HoraChile.Convertir(ahora);
        var consulta = new ConsultaContacto
        {
            Nombre = entrada.Nombre!.Trim(),
            Correo = entrada.Correo!.Trim(),
            // Siempre como +569XXXXXXXX: cabe en la columna aunque se haya escrito con muchos espacios.
            Telefono = TelefonoChile.Interpretar(entrada.Telefono)?.Internacional,
            Asunto = entrada.Asunto!.Trim(),
            Mensaje = entrada.Mensaje!.Trim(),
            FechaRecepcion = ahora
        };

        // La misma consulta enviada otra vez (doble clic, reintento, robot) no se guarda ni se avisa de nuevo.
        var haceUnaHora = ahora.AddHours(-1);
        var repetida = await db.ConsultasContacto.AsNoTracking()
            .Where(c => c.FechaRecepcion >= haceUnaHora && c.Correo == consulta.Correo && c.Mensaje == consulta.Mensaje)
            .Select(c => (int?)c.IdConsulta)
            .FirstOrDefaultAsync(ct);
        if (repetida is int idRepetida)
            return new ResultadoContacto.Recibida(idRepetida);

        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            db.ConsultasContacto.Add(consulta);
            await db.SaveChangesAsync(ct);
            var registro = await consentimientos.RegistrarAsync(
                TitularConsentimiento.Envio($"contacto:{consulta.IdConsulta}"), DocumentosLegales.Privacidad, versionPrivacidad!, ct);
            if (registro.Error is not null)
                throw new InvalidOperationException("No se pudo registrar el consentimiento de la consulta.");
            await transaccion.CommitAsync(ct);
        }

        await avisos.ConsultaContactoAsync(new ConsultaParaAviso(
            consulta.IdConsulta, horaChile, consulta.Nombre, consulta.Correo, consulta.Telefono, consulta.Asunto, consulta.Mensaje));
        return new ResultadoContacto.Recibida(consulta.IdConsulta);
    }
}
