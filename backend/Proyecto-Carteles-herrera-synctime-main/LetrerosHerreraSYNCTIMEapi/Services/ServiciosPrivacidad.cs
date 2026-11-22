using System.Globalization;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 11: «Mis datos» y la baja de la cuenta (RFS12, RFU34, RNFE01, E24, S23) ──

public sealed record ArchivoMisDatos(byte[] Contenido, string Nombre);

public sealed record ComprobanteBaja(string Numero, DateTimeOffset Fecha);

public abstract record ResultadoPrivacidad<T>
{
    public sealed record Correcto(T Valor) : ResultadoPrivacidad<T>;
    public sealed record Invalido(IReadOnlyDictionary<string, string[]> Errores, string Mensaje) : ResultadoPrivacidad<T>;
    public sealed record NoEncontrado : ResultadoPrivacidad<T>;
}

public sealed class ServicioPrivacidad(
    LetrerosHerreradbContext db,
    AuthService auth,
    IServicioConsentimientos consentimientos,
    ColaEnTransaccion cola,
    IAuditoria auditoria,
    IOptions<OpcionesCorreo> opcionesCorreo,
    TimeProvider reloj)
{
    public const string ConfirmacionBaja = "DAR DE BAJA";
    public const string MensajePassword = "La contraseña no es correcta.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static IReadOnlyDictionary<string, string[]> ErrorPassword() => new Dictionary<string, string[]> { ["password"] = [MensajePassword] };

    private Task<Cliente?> CuentaAsync(int idCliente, bool seguimiento, CancellationToken ct) =>
        (seguimiento ? db.Clientes : db.Clientes.AsNoTracking())
            .FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null && c.PasswordHash != null, ct);

    // POST /mi-cuenta/datos: todos los datos personales y el historial del cliente, y nada de nadie mas.
    public async Task<ResultadoPrivacidad<ArchivoMisDatos>> DescargarAsync(int idCliente, string? password, CancellationToken ct)
    {
        var cliente = await CuentaAsync(idCliente, seguimiento: false, ct);
        if (cliente is null)
            return new ResultadoPrivacidad<ArchivoMisDatos>.NoEncontrado();
        if (string.IsNullOrEmpty(password) || !auth.VerifyPassword(password, cliente.PasswordHash!))
            return new ResultadoPrivacidad<ArchivoMisDatos>.Invalido(ErrorPassword(), MensajePassword);

        var titular = TitularConsentimiento.Cliente(idCliente);
        var marketing = await consentimientos.VigenteAsync(titular, DocumentosLegales.Marketing, ct) is not null;
        var consentimientosDelCliente = await db.Consentimientos.AsNoTracking().Where(c => c.IdCliente == idCliente)
            .OrderBy(c => c.FechaAceptacion).ThenBy(c => c.IdConsentimiento)
            .Select(c => new { c.Tipo, Version = c.VersionDocumento, c.FechaAceptacion, c.FechaRevocacion })
            .ToListAsync(ct);
        var solicitudes = await db.Solicitudes.AsNoTracking().Where(s => s.IdCliente == idCliente)
            .OrderBy(s => s.IdSolicitud)
            .Select(s => new
            {
                s.IdSolicitud,
                Numero = "#" + s.IdSolicitud.ToString(),
                Fecha = s.FechaSolicitud,
                s.Origen,
                s.Estado,
                s.MotivoRechazo,
                s.DescripcionGeneral,
                Eliminada = s.EliminadoLogico,
                Detalles = s.Detalles.OrderBy(d => d.IdSolicitudDetalle)
                    .Select(d => new { Producto = d.Producto!.Nombre, d.Descripcion, d.AnchoMm, d.AltoMm, d.Cantidad }).ToList(),
                Adjuntos = s.Adjuntos.OrderBy(a => a.IdAdjunto).Select(a => new { Nombre = a.NombreOriginal, a.Tipo, Tamano = a.Bytes }).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);
        var cotizaciones = await db.Cotizaciones.AsNoTracking().Where(c => c.Solicitud!.IdCliente == idCliente)
            .OrderBy(c => c.IdCotizacion)
            .Select(c => new
            {
                c.IdCotizacion, Numero = "#" + c.IdCotizacion.ToString(), c.IdSolicitud, Fecha = c.FechaCotizacion, c.Estado,
                Total = c.TotalCotizacion, c.Observaciones, c.FechaRespuesta, c.MotivoRechazo, Eliminada = c.EliminadoLogico
            })
            .ToListAsync(ct);
        var pedidos = await db.Pedidos.AsNoTracking().Where(p => p.Solicitud!.IdCliente == idCliente)
            .OrderBy(p => p.IdPedido)
            .Select(p => new
            {
                p.IdPedido, Numero = "#" + p.IdPedido.ToString(), p.IdSolicitud, Fecha = p.FechaPedido, p.EstadoPedido, p.EstadoPago,
                p.MontoTotalFinal, p.MontoAbonado, p.SaldoPendiente, p.FechaEntregaEstimada, p.FechaEntregaReal, p.ModalidadEntrega,
                p.DireccionInstalacion, p.NumeroFactura, Eliminado = p.EliminadoLogico,
                Pagos = p.Pagos.OrderBy(x => x.IdPago).Select(x => new { x.Monto, x.FechaPago, x.MedioPago, x.Anulado }).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        // Los correos que se le enviaron: por su referencia (sobrevive a la purga) o por su direccion actual. Nunca los
        // avisos al taller, que no le llegaron a el.
        var referencias = solicitudes.SelectMany(s => new[] { $"confirmacion:{s.IdSolicitud}", $"estado:solicitud:{s.IdSolicitud}" })
            .Concat(cotizaciones.Select(c => $"cotizacion:{c.IdCotizacion}"))
            .Concat(pedidos.Select(p => $"estado:pedido:{p.IdPedido}"))
            .ToList();
        // Por la direccion, solo desde que el cliente se registro: la misma direccion pudo ser de otra cuenta antes.
        var correo = cliente.Email;
        var desde = new DateTimeOffset(cliente.FechaRegistro, HoraChile.ZonaHoraria.GetUtcOffset(cliente.FechaRegistro));
        var correos = await db.CorreosSalientes.AsNoTracking()
            .Where(c => (c.Referencia != null && referencias.Contains(c.Referencia))
                || (correo != null && c.Destinatarios == correo && c.FechaCreacion >= desde))
            .Where(c => c.Tipo != TiposCorreo.AvisoSolicitudNueva && c.Tipo != TiposCorreo.ConsultaContacto && c.Tipo != TiposCorreo.AvisoTopeSolicitudes)
            .OrderBy(c => c.IdCorreo)
            .Select(c => new { c.Tipo, Fecha = c.FechaCreacion, c.Estado })
            .ToListAsync(ct);

        var ahora = reloj.GetUtcNow();
        var archivo = new
        {
            Generado = ahora,
            Cuenta = new
            {
                cliente.IdCliente, cliente.Rut, cliente.Nombre, cliente.Apellido, cliente.Telefono, Correo = cliente.Email, cliente.Direccion,
                cliente.FechaRegistro, CorreoConfirmado = cliente.FechaVerificacionCorreo is not null, AvisosEstado = cliente.RecibeAvisosEstado, Marketing = marketing
            },
            // Fase 14A: el vinculo con Google es un dato suyo (sin el identificador interno de Google, que no le sirve y es sensible).
            IngresoConGoogle = await db.IdentidadesExternas.AsNoTracking().Where(i => i.IdCliente == idCliente)
                .Select(i => new { i.Proveedor, i.Correo, i.FechaVinculacion, i.FechaUltimoIngreso }).FirstOrDefaultAsync(ct),
            Consentimientos = consentimientosDelCliente,
            Solicitudes = solicitudes,
            Cotizaciones = cotizaciones,
            Pedidos = pedidos,
            CorreosEnviados = correos
        };
        await auditoria.RegistrarAsync("cliente", idCliente.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.DescargaDatos, null, CancellationToken.None);
        var nombre = $"mis-datos-letreros-herrera-{HoraChile.Convertir(ahora):yyyyMMdd}.json";
        return new ResultadoPrivacidad<ArchivoMisDatos>.Correcto(new ArchivoMisDatos(JsonSerializer.SerializeToUtf8Bytes(archivo, Json), nombre));
    }

    // POST /mi-cuenta/baja: inhabilita la cuenta en una transaccion. Se conservan solicitudes, cotizaciones, pedidos y
    // abonos; se borran la contraseña, los codigos y las sesiones; se apagan los correos opcionales. El comprobante
    // queda en pantalla y, si el correo estaba confirmado, en un correo esencial.
    public async Task<ResultadoPrivacidad<ComprobanteBaja>> DarDeBajaAsync(int idCliente, string? password, string? confirmacion, CancellationToken ct)
    {
        var cliente = await CuentaAsync(idCliente, seguimiento: true, ct);
        if (cliente is null)
            return new ResultadoPrivacidad<ComprobanteBaja>.NoEncontrado();
        if (!string.Equals(confirmacion?.Trim(), ConfirmacionBaja, StringComparison.Ordinal))
            return new ResultadoPrivacidad<ComprobanteBaja>.Invalido(
                new Dictionary<string, string[]> { ["confirmacion"] = [$"Escribe {ConfirmacionBaja} para confirmar."] }, $"Escribe {ConfirmacionBaja} para confirmar.");
        if (string.IsNullOrEmpty(password) || !auth.VerifyPassword(password, cliente.PasswordHash!))
            return new ResultadoPrivacidad<ComprobanteBaja>.Invalido(ErrorPassword(), MensajePassword);

        var ahora = reloj.GetUtcNow();
        var fechaChile = HoraChile.Convertir(ahora);
        var numero = $"BAJA-{idCliente}-{fechaChile:yyyyMMdd}";
        var avisar = Reglas.EsCorreo(cliente.Email) && cliente.FechaVerificacionCorreo is not null ? cliente.Email : null;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await PreferenciaAvisosEstado.CambiarAsync(db, consentimientos, cliente, recibe: false, reloj, ct);
        cliente.InhabilitadoEn = ahora;
        cliente.PasswordHash = null;
        // Fase 14A: la baja tambien libera su Google (ya no puede entrar por ahi, y ese Google queda libre).
        db.IdentidadesExternas.RemoveRange(await db.IdentidadesExternas.Where(i => i.IdCliente == idCliente).ToListAsync(ct));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra peticion (un doble clic) la dio de baja primero, o el personal la cambio: no se repite.
            return new ResultadoPrivacidad<ComprobanteBaja>.NoEncontrado();
        }
        await db.TokensRenovacion.Where(t => t.IdCliente == idCliente).ExecuteDeleteAsync(ct);
        await db.CodigosCuenta.Where(c => c.IdCliente == idCliente).ExecuteDeleteAsync(ct);
        await consentimientos.RevocarAsync(TitularConsentimiento.Cliente(idCliente), DocumentosLegales.Marketing, ct);
        if (avisar is not null)
            await cola.EncolarAsync(CorreosDeEstado.ComprobanteBaja(avisar, cliente.Nombre, numero, fechaChile, opcionesCorreo.Value.ContactoTaller), ct);
        await db.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("cliente", idCliente.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.BajaCuenta,
            DetalleAuditoria.Libre(new Dictionary<string, object?> { ["comprobante"] = numero, ["comprobantePorCorreo"] = avisar is not null }), ct);
        await transaccion.CommitAsync(CancellationToken.None);
        if (avisar is not null)
            cola.AvisarAlTrabajador();
        return new ResultadoPrivacidad<ComprobanteBaja>.Correcto(new ComprobanteBaja(numero, ahora));
    }
}
