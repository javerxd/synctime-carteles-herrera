using LetrerosHerreraSYNCTIMEapi.Infraestructura.Adjuntos;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Portada;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
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
    bool LogoAutorizado,
    int? IdImagen,
    byte[] RowVersion) : IConVersion
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

public sealed record ResultadoAutorizacionLogo(TrabajoVitrina? Trabajo, string? Error, bool VersionObsoleta = false);

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
            c.FechaAutorizacionLogo != null,
            c.IdImagen,
            c.RowVersion));

    // Fase 9: gestion ademas busca (q: titulo o tipo de letrero) y filtra por visible u oculta.
    public async Task<ResultadoPaginado<TrabajoVitrina>> ListarAsync(int? idCategoria, ParametrosListado parametros, bool incluirOcultos, CancellationToken ct,
        bool? visible = null)
    {
        var consulta = Base(incluirOcultos);
        if (idCategoria is int categoria)
            consulta = consulta.Where(c => c.Producto!.IdCategoria == categoria);
        if (incluirOcultos && visible is bool v)
            consulta = consulta.Where(c => c.Visible == v);
        if (incluirOcultos && parametros.Buscar is { } q)
            consulta = consulta.Where(c => c.Titulo.Contains(q) || c.Producto!.Nombre.Contains(q));

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
    // restricciones de la tabla, para responder 400 con el motivo en vez de un error de la base. La regla del
    // logo va aparte (409 logo_sin_autorizacion, fase 9).
    public async Task<string?> RevisarAsync(DatosPublicacion datos, CancellationToken ct)
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

        if (datos.IdImagen is int imagen && !await db.Imagenes.AnyAsync(i => i.IdImagen == imagen, ct))
            return "La imagen indicada no existe. Súbela primero en /api/v1/imagenes.";
        return null;
    }

    // ── Fase 9: administrar la vitrina (E12). Editar y cambiar la visibilidad exigen la version vista. ──

    public async Task<ResultadoGestion<TrabajoVitrina>> CrearAsync(DatosPublicacion datos, int idEmpleado, CancellationToken ct)
    {
        if (await RevisarAsync(datos, ct) is { } error)
            return Invalida(error);
        // Recien creada no tiene autorizacion: con logo, solo puede nacer oculta.
        if (datos.Visible && datos.IncluyeLogoCliente)
            return LogoSinAutorizacion();
        var item = new Catalogo
        {
            IdProducto = datos.IdProducto,
            IdEmpleadoCreador = idEmpleado,
            Titulo = datos.Titulo!.Trim(),
            Descripcion = datos.Descripcion ?? "",
            ImagenUrl = datos.ImagenUrl,
            IdImagen = datos.IdImagen,
            AnchoMm = datos.AnchoMm,
            AltoMm = datos.AltoMm,
            PrecioReferencial = datos.PrecioReferencial,
            Visible = datos.Visible,
            IncluyeLogoCliente = datos.IncluyeLogoCliente
        };
        db.Catalogos.Add(item);
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<TrabajoVitrina>.Correcto((await ObtenerAsync(item.IdCatalogo, incluirOcultos: true, CancellationToken.None))!);
    }

    public async Task<ResultadoGestion<TrabajoVitrina>> EditarAsync(int id, DatosPublicacion datos, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<TrabajoVitrina>.SinVersion();
        if (await RevisarAsync(datos, ct) is { } error)
            return Invalida(error);
        var existente = await db.Catalogos.FirstOrDefaultAsync(c => c.IdCatalogo == id, ct);
        if (existente is null)
            return new ResultadoGestion<TrabajoVitrina>.NoEncontrado();
        if (!Versiones.Iguales(version, existente.RowVersion))
            return new ResultadoGestion<TrabajoVitrina>.VersionObsoleta();
        // La autorizacion del logo vale para la pieza que el cliente vio: si cambia la imagen, o la
        // publicacion pasa a llevar un logo, hay que volver a registrarla (RNFO17).
        var mismaPieza = datos.IdImagen == existente.IdImagen
            && datos.ImagenUrl == existente.ImagenUrl
            && datos.IncluyeLogoCliente == existente.IncluyeLogoCliente;
        var logoAutorizado = mismaPieza && existente.FechaAutorizacionLogo is not null;
        if (datos.Visible && datos.IncluyeLogoCliente && !logoAutorizado)
            return LogoSinAutorizacion();

        if (!logoAutorizado)
        {
            existente.IdEmpleadoAutorizaLogo = null;
            existente.FechaAutorizacionLogo = null;
        }
        existente.IdProducto = datos.IdProducto;
        existente.Titulo = datos.Titulo!.Trim();
        existente.Descripcion = datos.Descripcion ?? "";
        existente.ImagenUrl = datos.ImagenUrl;
        existente.IdImagen = datos.IdImagen;
        existente.AnchoMm = datos.AnchoMm;
        existente.AltoMm = datos.AltoMm;
        existente.PrecioReferencial = datos.PrecioReferencial;
        existente.Visible = datos.Visible;
        existente.IncluyeLogoCliente = datos.IncluyeLogoCliente;
        db.Entry(existente).Property(c => c.Visible).IsModified = true;
        db.Entry(existente).Property(c => c.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<TrabajoVitrina>.Correcto((await ObtenerAsync(id, incluirOcultos: true, CancellationToken.None))!);
    }

    public async Task<ResultadoGestion<TrabajoVitrina>> CambiarVisibleAsync(int id, bool visible, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<TrabajoVitrina>.SinVersion();
        var existente = await db.Catalogos.FirstOrDefaultAsync(c => c.IdCatalogo == id, ct);
        if (existente is null)
            return new ResultadoGestion<TrabajoVitrina>.NoEncontrado();
        if (!Versiones.Iguales(version, existente.RowVersion))
            return new ResultadoGestion<TrabajoVitrina>.VersionObsoleta();
        if (visible && existente.IncluyeLogoCliente && existente.FechaAutorizacionLogo is null)
            return LogoSinAutorizacion();
        existente.Visible = visible;
        db.Entry(existente).Property(c => c.Visible).IsModified = true;
        db.Entry(existente).Property(c => c.RowVersion).OriginalValue = version;
        if (await GuardarAsync(ct) is { } conflicto)
            return conflicto;
        return new ResultadoGestion<TrabajoVitrina>.Correcto((await ObtenerAsync(id, incluirOcultos: true, CancellationToken.None))!);
    }

    public const string MensajeVersionObsoleta = "Otra persona modificó esta publicación mientras la veías. Vuelve a cargarla para ver los cambios.";
    private const string ReglaLogo = "ck_catalogo_logo";

    private static ResultadoGestion<TrabajoVitrina> Invalida(string mensaje) =>
        new ResultadoGestion<TrabajoVitrina>.Invalido(new Dictionary<string, string[]>(), mensaje);

    private static ResultadoGestion<TrabajoVitrina> LogoSinAutorizacion(bool desdeLaBase = false) =>
        new ResultadoGestion<TrabajoVitrina>.Conflicto(Negocio.Mantenedores.CodigosMantenedores.LogoSinAutorizacion, MensajeLogo, desdeLaBase);

    private async Task<ResultadoGestion<TrabajoVitrina>?> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<TrabajoVitrina>.VersionObsoleta();
        }
        catch (DbUpdateException e) when (ErroresSql.EsRegla(e, ReglaLogo))
        {
            // La base lo impide aunque la revision de arriba se saltara (ck_catalogo_logo).
            return LogoSinAutorizacion(desdeLaBase: true);
        }
    }

    // Registra (o retira) la autorizacion del cliente para mostrar su logo. Al retirarla, una publicacion
    // con logo deja de verse en el mismo paso. Registrarla de nuevo no cambia quien la dio ni cuando.
    // Fase 9: si viene la version vista (If-Match), se comprueba; sin ella se acepta, como en la fase 5.
    public async Task<ResultadoAutorizacionLogo> AutorizarLogoAsync(int id, int idEmpleado, bool autorizado, CancellationToken ct, byte[]? version = null)
    {
        var item = await db.Catalogos.FirstOrDefaultAsync(c => c.IdCatalogo == id, ct);
        if (item is null)
            return new(null, null);
        if (version is { Length: > 0 } && !Versiones.Iguales(version, item.RowVersion))
            return new(null, null, VersionObsoleta: true);

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

// ── Plazos de conservacion de la politica de privacidad ──

public sealed record ResultadoPurga(int Consultas, int Consentimientos, int Adjuntos = 0, int CuerposDeCorreo = 0, int IpsDeAuditoria = 0);

// Borra lo que la politica de privacidad dice que no se guarda mas:
// - consultas del formulario de contacto, 12 meses despues de recibirlas;
// - consentimientos de visitantes sin cuenta (cookies, contacto), 24 meses despues de darlos o retirarlos;
// - consentimientos de clientes y del personal, 24 meses despues de retirarlos (los vigentes se conservan,
//   porque acreditan que la persona acepto);
// - adjuntos borrador (fase 7) que nadie envio en 24 horas, con sus archivos, y los archivos de adjuntos
//   que quedaron sin registro en la base (por ejemplo, de una solicitud borrada);
// - fase 11: el asunto, el cuerpo, el enlace de baja y la direccion completa de los correos enviados hace mas de
//   Correo:Cola:DiasCuerpoEnviados (30) y de los fallidos hace mas de Correo:Cola:DiasCuerpoFallidos (90); la fila queda
//   con el tipo, la referencia, el estado, las fechas y el destino enmascarado. Y las claves de idempotencia vencidas.
// - fase 12B (RNFE01): la direccion IP de la bitacora, a los 90 dias; el registro del evento queda intacto, solo sin la IP.
//   El trigger de la bitacora admite exactamente ese cambio (ip = NULL en filas de mas de 90 dias) y ningun otro.
// La bitacora (auditoria) no se borra: se conserva al menos 24 meses.
public sealed class PurgaDatosPersonales(LetrerosHerreradbContext db, AlmacenAdjuntos almacenAdjuntos,
    Microsoft.Extensions.Options.IOptions<Infraestructura.Correo.OpcionesCorreo> opcionesCorreo, TimeProvider reloj)
{
    public const int MesesConsultas = 12;
    public const int MesesConsentimientos = 24;
    public const int DiasIpAuditoria = 90;

    public async Task<ResultadoPurga> PurgarAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var limiteConsultas = ahora.AddMonths(-MesesConsultas);
        var limiteConsentimientos = ahora.AddMonths(-MesesConsentimientos);

        var consultas = await db.ConsultasContacto
            .Where(c => c.FechaRecepcion < limiteConsultas)
            .ExecuteDeleteAsync(ct);
        var consentimientos = await db.Consentimientos
            .Where(c => (c.ReferenciaEnvio != null && (c.FechaRevocacion ?? c.FechaAceptacion) < limiteConsentimientos)
                || (c.FechaRevocacion != null && c.FechaRevocacion < limiteConsentimientos))
            .ExecuteDeleteAsync(ct);

        // Sesiones y codigos de cuenta vencidos hace mas de un dia: ya no sirven para nada.
        var limiteTecnico = ahora.AddDays(-1);
        await db.TokensRenovacion.Where(t => t.FechaExpiracion < limiteTecnico).ExecuteDeleteAsync(ct);
        await db.CodigosCuenta.Where(c => c.FechaExpiracion < limiteTecnico).ExecuteDeleteAsync(ct);
        // Fase 10: los desafios del rostro duran segundos; los de mas de un dia ya no sirven.
        while (await db.DesafiosRostro.Where(d => d.Emitido < limiteTecnico).OrderBy(d => d.IdDesafio).Take(5000).ExecuteDeleteAsync(ct) > 0)
        {
        }
        // Fase 11: las claves de idempotencia duran 24 horas.
        while (await db.ClavesIdempotencia.Where(c => c.Vence < ahora).OrderBy(c => c.IdClave).Take(5000).ExecuteDeleteAsync(ct) > 0)
        {
        }
        var cuerpos = await PurgarCuerposDeCorreoAsync(ahora, ct);
        return new ResultadoPurga(consultas, consentimientos, await PurgarAdjuntosAsync(ahora, ct), cuerpos, await PurgarIpsDeAuditoriaAsync(ahora, ct));
    }

    // Fase 12B (RNFE01): pasados 90 dias se borra SOLO la IP de la bitacora. De a 5000 filas, por el indice filtrado
    // ix_auditoria_ip_vigente (solo guarda las filas que aun tienen IP).
    private async Task<int> PurgarIpsDeAuditoriaAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        // 5 minutos de margen: el trigger revalida con el reloj de SQL Server, que puede ir un poco atras del de la aplicacion.
        var limite = ahora.AddDays(-DiasIpAuditoria).AddMinutes(-5);
        var total = 0;
        int lote;
        do
        {
            lote = await db.Auditoria.Where(a => a.Ip != null && a.Fecha < limite).OrderBy(a => a.Fecha).Take(5000)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Ip, (string?)null), ct);
            total += lote;
        }
        while (lote > 0);
        return total;
    }

    // Se cuenta desde que se envio o desde que fallo (proximo_intento guarda cuando fallo), no desde que se creo. Lo ya
    // purgado (cuerpo vacio) no se vuelve a tocar. De a 500 filas, para no bloquear la cola mucho rato.
    private async Task<int> PurgarCuerposDeCorreoAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var o = opcionesCorreo.Value.Cola;
        var limiteEnviados = ahora.AddDays(-Math.Max(1, o.DiasCuerpoEnviados));
        var limiteFallidos = ahora.AddDays(-Math.Max(1, o.DiasCuerpoFallidos));
        var total = 0;
        var ultimo = 0L;
        while (true)
        {
            // Paginado por id: cada vuelta sigue desde el ultimo, sin volver a recorrer lo ya revisado.
            var lote = await db.CorreosSalientes.AsNoTracking()
                .Where(c => c.IdCorreo > ultimo && c.CuerpoHtml != "" && ((c.Estado == EstadosCorreo.Enviado && c.FechaEnvio < limiteEnviados)
                    || (c.Estado == EstadosCorreo.Fallido && c.ProximoIntento < limiteFallidos)))
                .OrderBy(c => c.IdCorreo)
                .Select(c => new { c.IdCorreo, c.Destinatarios })
                .Take(500)
                .ToListAsync(ct);
            if (lote.Count == 0)
                return total;
            ultimo = lote[^1].IdCorreo;
            foreach (var correo in lote)
            {
                var enmascarado = Negocio.Correo.CategoriasCorreo.Enmascarar(correo.Destinatarios);
                total += await db.CorreosSalientes.Where(c => c.IdCorreo == correo.IdCorreo && c.CuerpoHtml != ""
                        && (c.Estado == EstadosCorreo.Enviado || c.Estado == EstadosCorreo.Fallido))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Asunto, "")
                        .SetProperty(c => c.CuerpoHtml, "")
                        .SetProperty(c => c.CuerpoTexto, "")
                        .SetProperty(c => c.EnlaceBaja, (string?)null)
                        .SetProperty(c => c.ResponderA, (string?)null)
                        .SetProperty(c => c.Destinatarios, enmascarado.Length == 0 ? "•••" : enmascarado), ct);
            }
        }
    }

    private async Task<int> PurgarAdjuntosAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var limite = ahora - ReglasAdjunto.VidaBorrador;
        var vencidos = await db.AdjuntosSolicitud.AsNoTracking()
            .Where(a => a.IdSolicitud == null && a.FechaSubida < limite)
            .Select(a => new { a.IdAdjunto, a.Clave })
            .ToListAsync(ct);
        var borrados = 0;
        if (vencidos.Count > 0)
        {
            var ids = vencidos.Select(v => v.IdAdjunto).ToList();
            // Solo los que siguen siendo borradores: si justo se vincularon a una solicitud, se conservan.
            borrados = await db.AdjuntosSolicitud.Where(a => ids.Contains(a.IdAdjunto) && a.IdSolicitud == null).ExecuteDeleteAsync(ct);
            var siguen = await db.AdjuntosSolicitud.AsNoTracking().Where(a => ids.Contains(a.IdAdjunto)).Select(a => a.IdAdjunto).ToListAsync(ct);
            foreach (var vencido in vencidos.Where(v => !siguen.Contains(v.IdAdjunto)))
                almacenAdjuntos.Borrar(vencido.Clave);
        }

        // Archivos sin registro (de borradores recien borrados o de solicitudes que borro el procedimiento
        // de depuracion). Solo los de mas de 24 horas: uno mas nuevo puede ser una subida en curso.
        var enDisco = almacenAdjuntos.ClavesAnterioresA(limite);
        if (enDisco.Count > 0)
        {
            var claves = enDisco.ToList();
            var conRegistro = await db.AdjuntosSolicitud.AsNoTracking().Where(a => claves.Contains(a.Clave)).Select(a => a.Clave).ToListAsync(ct);
            foreach (var clave in claves.Except(conRegistro))
                almacenAdjuntos.Borrar(clave);
        }
        return borrados;
    }
}

// Aplica la purga una vez al dia, la primera a los pocos minutos de arrancar. Un fallo se registra y se
// reintenta al dia siguiente: nunca detiene la API. Privacidad:PurgaActiva=false la desactiva.
public sealed class ProcesoPurgaDatosPersonales(
    IServiceScopeFactory alcances,
    IConfiguration configuracion,
    TimeProvider reloj,
    ILogger<ProcesoPurgaDatosPersonales> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuracion.GetValue("Privacidad:PurgaActiva", true))
            return;
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(5), reloj, ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await using var alcance = alcances.CreateAsyncScope();
                    var resultado = await alcance.ServiceProvider.GetRequiredService<PurgaDatosPersonales>().PurgarAsync(ct);
                    if (resultado.Consultas + resultado.Consentimientos + resultado.Adjuntos + resultado.CuerposDeCorreo > 0)
                        logger.LogInformation("Plazos de conservacion: se borraron {Consultas} consulta(s), {Consentimientos} consentimiento(s), {Adjuntos} adjunto(s) y el cuerpo de {Correos} correo(s) vencidos.",
                            resultado.Consultas, resultado.Consentimientos, resultado.Adjuntos, resultado.CuerposDeCorreo);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    logger.LogError(error, "No se pudo aplicar la purga de datos personales vencidos; se reintenta en 24 horas.");
                }
                await Task.Delay(TimeSpan.FromHours(24), reloj, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }
}
