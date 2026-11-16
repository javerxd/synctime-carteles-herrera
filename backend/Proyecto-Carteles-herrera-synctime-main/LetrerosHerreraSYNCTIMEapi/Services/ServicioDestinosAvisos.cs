using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 9: a que correos llegan los avisos del taller (pedido del usuario del 14-11-2026) ──
// Lo administran el dueño y el administrador desde su perfil. Una direccion nueva queda «pendiente» hasta
// que alguien abre el enlace que le llega (un solo uso, 24 horas, guardado solo como hash): mientras tanto
// no recibe avisos y la que reemplaza sigue recibiendolos. Cada cambio queda en el historial con la
// direccion enmascarada, y sus correos se escriben en la cola en la misma transaccion (outbox).

public sealed record DestinoReemplazado(int IdDestinatario, string Correo);

// Fase 11: con la preferencia (que solicitudes avisa) y su version para editarla con If-Match.
public sealed record VistaDestino(int IdDestinatario, string Correo, string Estado, DestinoReemplazado? ReemplazaA, DateTime FechaSolicitudChile,
    DateTime? FechaConfirmacionChile, DateTime? VenceChile, bool RecibeEnLinea, bool RecibePresenciales, byte[] RowVersion) : IConVersion;

public sealed record VistaHistorialAvisos(DateTime FechaChile, string Accion, string Correo, string? Empleado, string? Detalle);

public sealed record VistaAvisosTaller(IReadOnlyList<VistaDestino> Destinos, bool UsaRespaldo, IReadOnlyList<VistaHistorialAvisos> Historial);

public sealed record DestinoConfirmado(string Correo);

public sealed record PruebaEncolada(IReadOnlyList<long> IdsCorreos, IReadOnlyList<string> Destinatarios, bool UsaRespaldo);

public sealed record EstadoCorreoPrueba(long IdCorreo, string Destinatario, string Estado, int Intentos, string? Error);

public sealed class ServicioDestinosAvisos(
    LetrerosHerreradbContext db,
    ColaEnTransaccion cola,
    IDestinatariosAvisos destinatarios,
    IOptions<OpcionesCorreo> opcionesCorreo,
    IConfiguration configuracion,
    TimeProvider reloj)
{
    public const string MensajeEnlaceInvalido = "El enlace no es válido o ya venció. Pide uno nuevo desde el perfil.";
    public const string MensajeUltimoDestino = "Debe quedar al menos un correo activo. Para cambiarlo, reemplázalo.";
    public const int MaximoHistorial = 20;
    public const int PruebasPorVentana = 3;
    public static readonly TimeSpan VentanaPruebas = TimeSpan.FromMinutes(10);
    public const int ReenviosPorHora = 5;
    public static readonly TimeSpan EsperaEntreEnvios = TimeSpan.FromMinutes(1);
    public const int MaximoIdsEstado = 20;
    // Revision de seguridad: como mucho 3 cambios pendientes a la vez (cada uno envia un correo a una
    // direccion que escribio gestion).
    public const int MaximoPendientes = 3;
    public const string MensajeReemplazoPendienteAlQuitar =
        "Cancela primero el cambio pendiente de este destino: hay otra dirección esperando confirmación para reemplazarlo.";

    // Todas las escrituras van en fila: asi la regla «queda al menos un activo» y el limite de pruebas no se
    // pueden saltar con dos peticiones a la vez.
    private const string Bloqueo = "destinos_avisos";
    private const string IndiceVigente = "uq_destinatarios_avisos_vigente";

    private static readonly string[] Envios = [AccionesDestinatario.Agregar, AccionesDestinatario.Reemplazar, AccionesDestinatario.Reenviar];

    // GET /avisos-taller: los activos y pendientes con sus correos completos (gestion los administra), si se
    // usa el respaldo de la configuracion y los ultimos cambios con las direcciones enmascaradas.
    public async Task<VistaAvisosTaller> EstadoAsync(CancellationToken ct)
    {
        var vigentes = await db.DestinatariosAvisos.AsNoTracking()
            .Where(d => d.Estado == EstadosDestinatario.Activo || d.Estado == EstadosDestinatario.Pendiente)
            .OrderBy(d => d.Estado == EstadosDestinatario.Activo ? 0 : 1).ThenBy(d => d.IdDestinatario)
            .Select(d => new
            {
                d.IdDestinatario, d.Correo, d.Estado, d.IdReemplaza, ReemplazaCorreo = d.Reemplaza == null ? null : d.Reemplaza.Correo,
                d.FechaSolicitud, d.FechaConfirmacion, d.Vence, d.RecibeEnLinea, d.RecibePresenciales, d.RowVersion
            })
            .ToListAsync(ct);
        var historial = await db.DestinatariosAvisosHistorial.AsNoTracking()
            .OrderByDescending(h => h.Fecha).ThenByDescending(h => h.IdHistorial)
            .Take(MaximoHistorial)
            .Select(h => new { h.Fecha, h.Accion, h.CorreoEnmascarado, Empleado = h.Empleado == null ? null : h.Empleado.Nombre + " " + h.Empleado.Apellido, h.Detalle })
            .ToListAsync(ct);

        var destinos = vigentes.Select(d => new VistaDestino(d.IdDestinatario, d.Correo, d.Estado,
            d.IdReemplaza is int idReemplaza && d.ReemplazaCorreo is not null ? new DestinoReemplazado(idReemplaza, d.ReemplazaCorreo) : null,
            HoraChile.Convertir(d.FechaSolicitud),
            d.FechaConfirmacion is { } confirmacion ? HoraChile.Convertir(confirmacion) : null,
            d.Vence is { } vence ? HoraChile.Convertir(vence) : null, d.RecibeEnLinea, d.RecibePresenciales, d.RowVersion)).ToList();
        return new VistaAvisosTaller(destinos, !vigentes.Any(d => d.Estado == EstadosDestinatario.Activo),
            historial.Select(h => new VistaHistorialAvisos(HoraChile.Convertir(h.Fecha), h.Accion, h.CorreoEnmascarado, h.Empleado, h.Detalle)).ToList());
    }

    // POST /avisos-taller/destinos. Sin idReemplaza agrega un destino; con el, reemplaza ese activo. El
    // nuevo queda pendiente y recibe el enlace; nada cambia para los avisos hasta que se confirme.
    public async Task<ResultadoGestion<VistaDestino>> SolicitarAsync(int idEmpleado, string? correoEscrito, int? idReemplaza, CancellationToken ct)
    {
        var correo = ValidacionMantenedores.NormalizarCorreo(correoEscrito);
        if (!Reglas.EsCorreo(correo) || correo.Length > ValidacionCuenta.LargoCorreo)
            return new ResultadoGestion<VistaDestino>.Invalido(new Dictionary<string, string[]>
            {
                ["correo"] = ["Escribe un correo válido, por ejemplo taller@gmail.com."]
            });
        if (UrlSitio() is not { } sitio)
            return new ResultadoGestion<VistaDestino>.NoDisponible("Falta la dirección pública del sitio (Sitio:UrlPublica): no se puede armar el enlace de confirmación.");

        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        // Un pendiente vencido no bloquea: se cancela (con su historial) y se sigue.
        await CancelarVencidosAsync(db.DestinatariosAvisos.Where(d => d.Correo == correo || (idReemplaza != null && d.IdReemplaza == idReemplaza)),
            idEmpleado, ahora, ct);
        if (await db.DestinatariosAvisos.AnyAsync(d => d.Correo == correo && (d.Estado == EstadosDestinatario.Activo || d.Estado == EstadosDestinatario.Pendiente), ct))
            return YaExiste();
        if (await db.DestinatariosAvisos.CountAsync(d => d.Estado == EstadosDestinatario.Pendiente && d.Vence > ahora, ct) >= MaximoPendientes)
            return new ResultadoGestion<VistaDestino>.Conflicto("demasiados_pendientes",
                $"Ya hay {MaximoPendientes} cambios esperando confirmación. Confirma o cancela alguno antes de pedir otro.");
        if (await LimiteDeEnviosAsync(correo, ahora, ct) is { } limite)
            return limite;

        DestinatarioAviso? reemplazado = null;
        if (idReemplaza is int idViejo)
        {
            reemplazado = await db.DestinatariosAvisos.FirstOrDefaultAsync(d => d.IdDestinatario == idViejo, ct);
            if (reemplazado is null)
                return new ResultadoGestion<VistaDestino>.NoEncontrado();
            if (reemplazado.Estado != EstadosDestinatario.Activo)
                return new ResultadoGestion<VistaDestino>.Conflicto(CodigosMantenedores.TransicionInvalida, "Solo se reemplaza un destino activo.");
            if (await db.DestinatariosAvisos.AnyAsync(d => d.IdReemplaza == idViejo && d.Estado == EstadosDestinatario.Pendiente && d.Vence > ahora, ct))
                return new ResultadoGestion<VistaDestino>.Conflicto(CodigosMantenedores.ReemplazoPendiente,
                    "Este destino ya tiene un cambio pendiente de confirmar. Reenvía su enlace o cancélalo antes de pedir otro.");
        }

        var token = Secretos.Nuevo();
        var destino = new DestinatarioAviso
        {
            Correo = correo,
            Estado = EstadosDestinatario.Pendiente,
            IdReemplaza = reemplazado?.IdDestinatario,
            TokenHash = Secretos.Hash(token),
            Vence = ahora.AddHours(CorreosDestinosAvisos.HorasVigenciaEnlace),
            IdEmpleadoSolicita = idEmpleado,
            FechaSolicitud = ahora
        };
        db.DestinatariosAvisos.Add(destino);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceVigente))
        {
            return YaExiste(desdeLaBase: true);
        }

        var accion = reemplazado is null ? AccionesDestinatario.Agregar : AccionesDestinatario.Reemplazar;
        Historial(accion, correo, destino.IdDestinatario, idEmpleado, ahora,
            reemplazado is null ? "Pendiente de confirmar." : $"Reemplaza a {CorreosDestinosAvisos.Enmascarar(reemplazado.Correo)}. Pendiente de confirmar.");
        await EncolarConfirmacionAsync(destino, token, sitio, idEmpleado, ct);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        cola.AvisarAlTrabajador();
        return new ResultadoGestion<VistaDestino>.Correcto(await VistaAsync(destino.IdDestinatario));
    }

    // Fase 11 (decision 1): que solicitudes avisa un destino activo o pendiente: las en linea, las presenciales o
    // ambas (al menos una). Con la version vista (If-Match) y en el historial de la seccion. No pide contraseña.
    public async Task<ResultadoGestion<VistaDestino>> PreferenciaAsync(int idEmpleado, int id, bool? enLinea, bool? presenciales, byte[]? version,
        CancellationToken ct)
    {
        if (enLinea is not bool recibeEnLinea || presenciales is not bool recibePresenciales)
            return new ResultadoGestion<VistaDestino>.Invalido(new Dictionary<string, string[]>
            {
                ["preferencia"] = ["Indica si recibe las solicitudes en línea y las presenciales (recibeEnLinea y recibePresenciales: true o false)."]
            });
        if (!recibeEnLinea && !recibePresenciales)
            return new ResultadoGestion<VistaDestino>.Invalido(new Dictionary<string, string[]>
            {
                ["preferencia"] = ["Elige al menos un tipo de solicitud: en línea o presenciales."]
            });
        if (version is not { Length: > 0 })
            return new ResultadoGestion<VistaDestino>.SinVersion();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        var destino = await db.DestinatariosAvisos.FirstOrDefaultAsync(d => d.IdDestinatario == id, ct);
        if (destino is null)
            return new ResultadoGestion<VistaDestino>.NoEncontrado();
        if (destino.Estado is not (EstadosDestinatario.Activo or EstadosDestinatario.Pendiente))
            return new ResultadoGestion<VistaDestino>.Conflicto(CodigosMantenedores.TransicionInvalida,
                "Solo se cambia la preferencia de un destino activo o pendiente de confirmar.");
        if (!Versiones.Iguales(version, destino.RowVersion))
            return new ResultadoGestion<VistaDestino>.VersionObsoleta();

        destino.RecibeEnLinea = recibeEnLinea;
        destino.RecibePresenciales = recibePresenciales;
        db.Entry(destino).Property(d => d.RowVersion).OriginalValue = version;
        Historial(AccionesDestinatario.Preferencia, destino.Correo, id, idEmpleado, reloj.GetUtcNow(), TextoPreferencia(recibeEnLinea, recibePresenciales));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<VistaDestino>.VersionObsoleta();
        }
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoGestion<VistaDestino>.Correcto(await VistaAsync(id));
    }

    private static string TextoPreferencia(bool enLinea, bool presenciales) => (enLinea, presenciales) switch
    {
        (true, true) => "Recibe las solicitudes en línea y las presenciales.",
        (true, false) => "Recibe solo las solicitudes en línea.",
        _ => "Recibe solo las solicitudes presenciales."
    };

    // Enlace nuevo para un pendiente: el anterior deja de servir. Uno por minuto y 5 por hora por destino.
    public async Task<ResultadoGestion<VistaDestino>> ReenviarAsync(int idEmpleado, int id, CancellationToken ct)
    {
        if (UrlSitio() is not { } sitio)
            return new ResultadoGestion<VistaDestino>.NoDisponible("Falta la dirección pública del sitio (Sitio:UrlPublica): no se puede armar el enlace de confirmación.");
        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        var destino = await db.DestinatariosAvisos.FirstOrDefaultAsync(d => d.IdDestinatario == id, ct);
        if (destino is null)
            return new ResultadoGestion<VistaDestino>.NoEncontrado();
        if (destino.Estado != EstadosDestinatario.Pendiente)
            return new ResultadoGestion<VistaDestino>.Conflicto(CodigosMantenedores.TransicionInvalida, "Solo se reenvía el enlace de un destino pendiente de confirmar.");

        if (await LimiteDeEnviosAsync(destino.Correo, ahora, ct) is { } limite)
            return limite;

        var token = Secretos.Nuevo();
        destino.TokenHash = Secretos.Hash(token);
        destino.Vence = ahora.AddHours(CorreosDestinosAvisos.HorasVigenciaEnlace);
        Historial(AccionesDestinatario.Reenviar, destino.Correo, id, idEmpleado, ahora, "Enlace nuevo; el anterior dejó de servir.");
        await EncolarConfirmacionAsync(destino, token, sitio, idEmpleado, ct);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        cola.AvisarAlTrabajador();
        return new ResultadoGestion<VistaDestino>.Correcto(await VistaAsync(id));
    }

    // El pendiente pasa a «cancelado»: su enlace deja de servir. Si reemplazaba a otro, ese sigue activo.
    public async Task<ResultadoGestion<VistaDestino>> CancelarAsync(int idEmpleado, int id, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        var destino = await db.DestinatariosAvisos.FirstOrDefaultAsync(d => d.IdDestinatario == id, ct);
        if (destino is null)
            return new ResultadoGestion<VistaDestino>.NoEncontrado();
        if (destino.Estado != EstadosDestinatario.Pendiente)
            return new ResultadoGestion<VistaDestino>.Conflicto(CodigosMantenedores.TransicionInvalida, "Solo se cancela un cambio pendiente de confirmar.");

        destino.Estado = EstadosDestinatario.Cancelado;
        destino.TokenHash = null;
        destino.Vence = null;
        destino.FechaRetiro = ahora;
        Historial(AccionesDestinatario.Cancelar, destino.Correo, id, idEmpleado, ahora, null);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoGestion<VistaDestino>.Correcto(await VistaAsync(id));
    }

    // Quita un activo (pasa a «retirado») y le avisa. Siempre queda al menos uno activo (decision 3).
    public async Task<ResultadoGestion<bool>> QuitarAsync(int idEmpleado, int id, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        var destino = await db.DestinatariosAvisos.FirstOrDefaultAsync(d => d.IdDestinatario == id, ct);
        if (destino is null)
            return new ResultadoGestion<bool>.NoEncontrado();
        if (destino.Estado != EstadosDestinatario.Activo)
            return new ResultadoGestion<bool>.Conflicto(CodigosMantenedores.TransicionInvalida,
                "Solo se quita un destino activo. Un cambio pendiente se cancela.");
        if (await db.DestinatariosAvisos.AnyAsync(d => d.IdReemplaza == id && d.Estado == EstadosDestinatario.Pendiente && d.Vence > ahora, ct))
            return new ResultadoGestion<bool>.Conflicto(CodigosMantenedores.ReemplazoPendiente, MensajeReemplazoPendienteAlQuitar);
        if (await db.DestinatariosAvisos.CountAsync(d => d.Estado == EstadosDestinatario.Activo, ct) <= 1)
            return new ResultadoGestion<bool>.Conflicto(CodigosMantenedores.UltimoDestino, MensajeUltimoDestino);

        destino.Estado = EstadosDestinatario.Retirado;
        destino.FechaRetiro = ahora;
        Historial(AccionesDestinatario.Quitar, destino.Correo, id, idEmpleado, ahora, null);
        var quien = await NombreAsync(idEmpleado, ct);
        await cola.EncolarAsync(CorreosDestinosAvisos.Quitado(destino.Correo, quien, HoraChile.Convertir(ahora), opcionesCorreo.Value.ContactoTaller), ct);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        cola.AvisarAlTrabajador();
        return new ResultadoGestion<bool>.Correcto(true);
    }

    // POST /avisos-taller/confirmar (publica, la abre quien recibio el correo). Un enlace invalido, vencido o
    // ya usado responde lo mismo, sin decir de que correo era. Al confirmar: el pendiente pasa a activo; si
    // reemplazaba a otro, ese se retira y recibe el aviso del cambio; si era uno agregado, cada activo
    // anterior recibe «Se agrego un destino». Todo en una transaccion.
    public async Task<ResultadoGestion<DestinoConfirmado>> ConfirmarAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return EnlaceInvalido();
        var hash = Secretos.Hash(token.Trim());
        var ahora = reloj.GetUtcNow();
        // Ruta publica: un enlace inventado no toma el bloqueo comun (no puede frenar a gestion). Solo uno que
        // existe y no vencio sigue, y se vuelve a revisar con el bloqueo tomado.
        if (!await db.DestinatariosAvisos.AsNoTracking().AnyAsync(d => d.TokenHash == hash && d.Estado == EstadosDestinatario.Pendiente && d.Vence > ahora, ct))
            return EnlaceInvalido();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        var destino = await db.DestinatariosAvisos.Include(d => d.EmpleadoSolicita)
            .FirstOrDefaultAsync(d => d.TokenHash == hash && d.Estado == EstadosDestinatario.Pendiente, ct);
        if (destino is null || destino.Vence is not { } vence || vence <= ahora
            || !Secretos.IgualesEnTiempoConstante(destino.TokenHash!, hash))
            return EnlaceInvalido();

        var anteriores = await db.DestinatariosAvisos.Where(d => d.Estado == EstadosDestinatario.Activo).ToListAsync(ct);
        var reemplazado = anteriores.FirstOrDefault(d => d.IdDestinatario == destino.IdReemplaza);
        destino.Estado = EstadosDestinatario.Activo;
        destino.TokenHash = null;
        destino.Vence = null;
        destino.FechaConfirmacion = ahora;

        var enmascarado = CorreosDestinosAvisos.Enmascarar(destino.Correo);
        var pidio = NombreDePila(destino.EmpleadoSolicita?.Nombre);
        var fechaChile = HoraChile.Convertir(ahora);
        var contacto = opcionesCorreo.Value.ContactoTaller;
        if (reemplazado is not null)
        {
            reemplazado.Estado = EstadosDestinatario.Retirado;
            reemplazado.FechaRetiro = ahora;
            await cola.EncolarAsync(CorreosDestinosAvisos.Cambio(reemplazado.Correo, enmascarado, pidio, fechaChile, contacto), ct);
            Historial(AccionesDestinatario.Confirmar, destino.Correo, destino.IdDestinatario, null, ahora,
                $"Reemplazó a {CorreosDestinosAvisos.Enmascarar(reemplazado.Correo)}, que dejó de recibir los avisos.");
        }
        else
        {
            // Si no habia activos, los avisos iban al respaldo de la configuracion: sus direcciones se enteran
            // de que ahora llegan a otra (la lectura sigue en IDestinatariosAvisos).
            var avisar = anteriores.Select(a => a.Correo).ToList();
            if (avisar.Count == 0)
                avisar = (await destinatarios.LeerAsync(ct)).Correos
                    .Where(c => !string.Equals(c, destino.Correo, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var correoAnterior in avisar)
                await cola.EncolarAsync(CorreosDestinosAvisos.Agregado(correoAnterior, enmascarado, pidio, fechaChile, contacto), ct);
            Historial(AccionesDestinatario.Confirmar, destino.Correo, destino.IdDestinatario, null, ahora,
                anteriores.Count == 0 ? "Primer destino guardado: deja de usarse el respaldo de la configuración." : "Destino agregado.");
        }
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        cola.AvisarAlTrabajador();
        return new ResultadoGestion<DestinoConfirmado>.Correcto(new DestinoConfirmado(enmascarado));
    }

    // «Enviar correo de prueba»: un aviso de ejemplo a cada destinatario actual (de la base o del respaldo),
    // en la cola y en la misma transaccion que su registro. No espera el envio: la pantalla consulta el
    // estado. Limite propio de 3 cada 10 minutos; no gasta el tope de avisos por hora.
    public async Task<ResultadoGestion<PruebaEncolada>> ProbarAsync(int idEmpleado, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, Bloqueo, BloqueoDeAplicacion.Espera(configuracion), ct);
        var desde = ahora - VentanaPruebas;
        if (await db.DestinatariosAvisosHistorial.CountAsync(h => h.Accion == AccionesDestinatario.Probar && h.Fecha > desde, ct) >= PruebasPorVentana)
            return new ResultadoGestion<PruebaEncolada>.DemasiadosIntentos(
                $"Ya se enviaron {PruebasPorVentana} correos de prueba en los últimos {VentanaPruebas.TotalMinutes:0} minutos. Espera un poco antes de enviar otro.");

        var vigentes = await destinatarios.LeerAsync(ct);
        if (vigentes.Correos.Count == 0)
            return new ResultadoGestion<PruebaEncolada>.Conflicto("sin_destinos",
                "No hay ningún correo que reciba los avisos. Agrega uno antes de enviar la prueba.");

        var urlPublica = configuracion["Sitio:UrlPublica"];
        var ahoraChile = HoraChile.Convertir(ahora);
        var filas = vigentes.Correos
            .Select(correo => FilaDeCola.Crear(AvisoSolicitudNueva.ComponerPrueba(correo, ahoraChile, urlPublica), ahora))
            .ToList();
        db.CorreosSalientes.AddRange(filas);
        var enmascarados = string.Join(", ", vigentes.Correos.Select(CorreosDestinosAvisos.Enmascarar));
        Historial(AccionesDestinatario.Probar, enmascarados.Length > 254 ? enmascarados[..254] : enmascarados, null, idEmpleado, ahora,
            vigentes.DesdeRespaldo
                ? $"Prueba a {vigentes.Correos.Count} destino(s) del respaldo de la configuración."
                : $"Prueba a {vigentes.Correos.Count} destino(s).");
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        cola.AvisarAlTrabajador();
        return new ResultadoGestion<PruebaEncolada>.Correcto(new PruebaEncolada(filas.Select(f => f.IdCorreo).ToList(), vigentes.Correos, vigentes.DesdeRespaldo));
    }

    // Solo responde por correos de prueba: no sirve para mirar otros correos de la cola.
    public async Task<IReadOnlyList<EstadoCorreoPrueba>> EstadoPruebaAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return [];
        var filas = await db.CorreosSalientes.AsNoTracking()
            .Where(c => ids.Contains(c.IdCorreo) && c.Tipo == TiposCorreo.PruebaAvisos)
            .OrderBy(c => c.IdCorreo)
            .Select(c => new { c.IdCorreo, c.Destinatarios, c.Estado, c.Intentos, c.UltimoError })
            .ToListAsync(ct);
        // ultimo_error ya es un texto fijo en español (ClasificadorFallos), sin datos del servidor.
        return filas.Select(c => new EstadoCorreoPrueba(c.IdCorreo, c.Destinatarios,
            c.Estado switch
            {
                EstadosCorreo.Enviado => EstadosCorreo.Enviado,
                EstadosCorreo.Fallido => EstadosCorreo.Fallido,
                _ => EstadosCorreo.Pendiente
            },
            c.Intentos, c.Estado == EstadosCorreo.Enviado ? null : c.UltimoError)).ToList();
    }

    // Por direccion (aunque se cancele y se vuelva a pedir): un envio por minuto y 5 por hora entre agregar,
    // reemplazar y reenviar.
    private async Task<ResultadoGestion<VistaDestino>?> LimiteDeEnviosAsync(string correo, DateTimeOffset ahora, CancellationToken ct)
    {
        var haceUnMinuto = ahora - EsperaEntreEnvios;
        var haceUnaHora = ahora.AddHours(-1);
        var envios = db.DestinatariosAvisosHistorial.Where(h => Envios.Contains(h.Accion) && h.Fecha > haceUnaHora
            && db.DestinatariosAvisos.Any(d => d.IdDestinatario == h.IdDestinatario && d.Correo == correo));
        if (await envios.AnyAsync(h => h.Fecha > haceUnMinuto, ct) || await envios.CountAsync(ct) >= ReenviosPorHora)
            return new ResultadoGestion<VistaDestino>.DemasiadosIntentos(
                $"Ya enviamos un enlace a ese correo hace poco. Se puede enviar una vez por minuto y {ReenviosPorHora} veces por hora.");
        return null;
    }

    private async Task CancelarVencidosAsync(IQueryable<DestinatarioAviso> candidatos, int idEmpleado, DateTimeOffset ahora, CancellationToken ct)
    {
        var vencidos = await candidatos.Where(d => d.Estado == EstadosDestinatario.Pendiente && d.Vence <= ahora).ToListAsync(ct);
        foreach (var vencido in vencidos)
        {
            vencido.Estado = EstadosDestinatario.Cancelado;
            vencido.TokenHash = null;
            vencido.Vence = null;
            vencido.FechaRetiro = ahora;
            Historial(AccionesDestinatario.Cancelar, vencido.Correo, vencido.IdDestinatario, idEmpleado, ahora, "Venció sin confirmarse.");
        }
        if (vencidos.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private async Task EncolarConfirmacionAsync(DestinatarioAviso destino, string token, string sitio, int idEmpleado, CancellationToken ct)
    {
        var enlace = $"{sitio}/avisos/confirmar?token={Uri.EscapeDataString(token)}";
        var nombre = await NombreAsync(idEmpleado, ct);
        await cola.EncolarAsync(CorreosDestinosAvisos.Confirmar(destino.Correo, nombre, enlace, opcionesCorreo.Value.ContactoTaller), ct);
    }

    private void Historial(string accion, string correo, int? idDestinatario, int? idEmpleado, DateTimeOffset fecha, string? detalle) =>
        db.DestinatariosAvisosHistorial.Add(new DestinatarioAvisoHistorial
        {
            Accion = accion,
            // En la prueba ya llega enmascarado (puede ser una lista).
            CorreoEnmascarado = accion == AccionesDestinatario.Probar ? correo : CorreosDestinosAvisos.Enmascarar(correo),
            IdDestinatario = idDestinatario,
            IdEmpleado = idEmpleado,
            Fecha = fecha,
            Detalle = detalle
        });

    private async Task<string> NombreAsync(int idEmpleado, CancellationToken ct) =>
        NombreDePila(await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idEmpleado).Select(e => e.Nombre).FirstOrDefaultAsync(ct));

    // Solo el nombre de pila: basta para reconocer a quien lo pidio sin exponer mas datos.
    private static string NombreDePila(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? "Alguien del taller" : nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    private async Task<VistaDestino> VistaAsync(int id)
    {
        var d = await db.DestinatariosAvisos.AsNoTracking().Where(x => x.IdDestinatario == id)
            .Select(x => new { x.IdDestinatario, x.Correo, x.Estado, x.IdReemplaza, ReemplazaCorreo = x.Reemplaza == null ? null : x.Reemplaza.Correo,
                x.FechaSolicitud, x.FechaConfirmacion, x.Vence, x.RecibeEnLinea, x.RecibePresenciales, x.RowVersion })
            .SingleAsync(CancellationToken.None);
        return new VistaDestino(d.IdDestinatario, d.Correo, d.Estado,
            d.IdReemplaza is int r && d.ReemplazaCorreo is not null ? new DestinoReemplazado(r, d.ReemplazaCorreo) : null,
            HoraChile.Convertir(d.FechaSolicitud),
            d.FechaConfirmacion is { } c ? HoraChile.Convertir(c) : null,
            d.Vence is { } v ? HoraChile.Convertir(v) : null, d.RecibeEnLinea, d.RecibePresenciales, d.RowVersion);
    }

    private string? UrlSitio()
    {
        var url = configuracion["Sitio:UrlPublica"];
        return string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
               || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            ? null
            : url.Trim().TrimEnd('/');
    }

    private static ResultadoGestion<VistaDestino> YaExiste(bool desdeLaBase = false) =>
        new ResultadoGestion<VistaDestino>.Conflicto(CodigosMantenedores.YaExiste, "Ese correo ya recibe los avisos o tiene un cambio pendiente de confirmar.", desdeLaBase);

    private static ResultadoGestion<DestinoConfirmado> EnlaceInvalido() =>
        new ResultadoGestion<DestinoConfirmado>.Invalido(new Dictionary<string, string[]>(), MensajeEnlaceInvalido);
}
