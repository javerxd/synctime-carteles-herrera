using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 11: correos al cliente y baja (RFS08, RFU09, S09) ──

// La clave que firma los enlaces de baja: Correo:ClaveBaja si existe; si no, una derivada de Jwt:Key con HKDF
// (etiqueta «synctime:baja-correo»). Cambiar Jwt:Key invalida los enlaces de baja ya enviados.
public sealed class ClaveEnlaceBaja(IConfiguration configuracion)
{
    private readonly Lazy<byte[]> clave = new(() =>
        configuracion["Correo:ClaveBaja"] is { Length: > 0 } propia
            ? propia.Length >= 32 ? EnlaceBaja.DerivarClave(propia) : throw new InvalidOperationException("Correo:ClaveBaja debe tener al menos 32 caracteres.")
        : configuracion["Jwt:Key"] is { Length: > 0 } jwt ? EnlaceBaja.DerivarClave(jwt)
        : throw new InvalidOperationException("No se configuro Jwt:Key."));

    public byte[] Valor => clave.Value;
}

// A quien se le puede enviar un correo opcional (aviso de estado), con su token de baja.
public sealed record DestinoOpcional(int IdCliente, string Correo, string Nombre, string TokenBaja);

// Fase 11: encola los avisos de estado al cliente en la transaccion de quien llama (outbox). Solo a una cuenta con
// el correo confirmado, no dada de baja y con la preferencia avisos_estado activa; sin direccion publica del sitio
// no hay enlace de baja y el correo opcional no se envia.
public sealed class AvisosAlCliente(
    LetrerosHerreradbContext db,
    ClaveEnlaceBaja clave,
    IOptions<OpcionesCorreo> opcionesCorreo,
    IConfiguration configuracion,
    TimeProvider reloj,
    ILogger<AvisosAlCliente> logger)
{
    public async Task<DestinoOpcional?> DestinoAsync(int idCliente, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().Where(c => c.IdCliente == idCliente)
            .Select(c => new { c.IdCliente, c.Email, c.Nombre, c.FechaVerificacionCorreo, c.InhabilitadoEn, c.RecibeAvisosEstado })
            .FirstOrDefaultAsync(ct);
        if (cliente is null || !Reglas.EsCorreo(cliente.Email) || cliente.FechaVerificacionCorreo is null || cliente.InhabilitadoEn is not null
            || !cliente.RecibeAvisosEstado)
            return null;
        var token = EnlaceBaja.Token(clave.Valor, cliente.IdCliente, cliente.Email!, cliente.InhabilitadoEn);
        if (EnlaceBaja.Enlaces(configuracion["Sitio:UrlPublica"], token) is null)
        {
            logger.LogWarning("Falta Sitio:UrlPublica o no es valida: el aviso de estado al cliente {IdCliente} no se envia (no tendria enlace de baja).", idCliente);
            return null;
        }
        return new DestinoOpcional(cliente.IdCliente, cliente.Email!, cliente.Nombre, token);
    }

    // true si encolo el correo.
    public async Task<bool> EstadoSolicitudAsync(IServicioCorreo cola, int idSolicitud, int idCliente, string estado, string? motivo, CancellationToken ct)
    {
        if (await DestinoAsync(idCliente, ct) is not { } destino)
            return false;
        await cola.EncolarAsync(CorreosDeEstado.Solicitud(destino.Correo, destino.Nombre, idSolicitud, estado, motivo, HoraChile.Convertir(reloj.GetUtcNow()),
            opcionesCorreo.Value.ContactoTaller, configuracion["Sitio:UrlPublica"], destino.TokenBaja), ct);
        return true;
    }

    public async Task<bool> EstadoPedidoAsync(IServicioCorreo cola, int idPedido, string estado, string? motivo, CancellationToken ct)
    {
        var idCliente = await db.Pedidos.AsNoTracking().Where(p => p.IdPedido == idPedido).Select(p => (int?)p.Solicitud!.IdCliente).FirstOrDefaultAsync(ct);
        if (idCliente is not int id || await DestinoAsync(id, ct) is not { } destino)
            return false;
        await cola.EncolarAsync(CorreosDeEstado.Pedido(destino.Correo, destino.Nombre, idPedido, estado, motivo, HoraChile.Convertir(reloj.GetUtcNow()),
            opcionesCorreo.Value.ContactoTaller, configuracion["Sitio:UrlPublica"], destino.TokenBaja), ct);
        return true;
    }
}

// La preferencia avisos_estado: en la columna del cliente y, cada cambio, en consentimientos (aceptacion o revocacion,
// version vigente y fecha). Si no habia una aceptacion registrada (la preferencia nace activa), la revocacion queda
// como un registro con la aceptacion y la revocacion en el mismo instante.
public static class PreferenciaAvisosEstado
{
    public static async Task<bool> CambiarAsync(LetrerosHerreradbContext db, IServicioConsentimientos consentimientos, Cliente cliente, bool recibe,
        TimeProvider reloj, CancellationToken ct)
    {
        if (cliente.RecibeAvisosEstado == recibe)
            return false;
        // Revision de la fase 11: si otra persona guardo el cliente entretanto (la ficha del personal, otra pestaña), se
        // recarga y se vuelve a aplicar: una baja nunca falla por una version vieja.
        for (var intento = 1; ; intento++)
        {
            cliente.RecibeAvisosEstado = recibe;
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (intento < 3)
            {
                await db.Entry(cliente).ReloadAsync(ct);
                if (cliente.RecibeAvisosEstado == recibe)
                    return false;
            }
        }
        var titular = TitularConsentimiento.Cliente(cliente.IdCliente);
        if (recibe)
        {
            if ((await consentimientos.RegistrarAsync(titular, DocumentosLegales.AvisosEstado, DocumentosLegales.VersionVigente(DocumentosLegales.AvisosEstado)!, ct))
                .Error is { } error)
                throw new InvalidOperationException($"No se pudo registrar el consentimiento de los avisos de estado: {error}");
        }
        else if (!await consentimientos.RevocarAsync(titular, DocumentosLegales.AvisosEstado, ct))
        {
            var ahora = reloj.GetUtcNow();
            db.Consentimientos.Add(new Consentimiento
            {
                IdCliente = cliente.IdCliente,
                Tipo = DocumentosLegales.AvisosEstado,
                VersionDocumento = DocumentosLegales.VersionVigente(DocumentosLegales.AvisosEstado)!,
                FechaAceptacion = ahora,
                FechaRevocacion = ahora
            });
            await db.SaveChangesAsync(ct);
        }
        return true;
    }
}

public sealed record EstadoBaja(string CorreoEnmascarado, bool AvisosEstado, bool Marketing);
public sealed record PreferenciasBaja(bool AvisosEstado, bool Marketing);

// Fase 11: la baja desde el enlace del correo, sin sesion. El GET solo muestra; el POST apaga. Un token invalido,
// alterado o viejo responde lo mismo que uno inexistente (null: 404), sin decir de quien era.
public sealed class ServicioBajaCorreos(
    LetrerosHerreradbContext db,
    ClaveEnlaceBaja clave,
    IServicioConsentimientos consentimientos,
    IAuditoria auditoria,
    TimeProvider reloj)
{
    public async Task<EstadoBaja?> VerAsync(string? token, CancellationToken ct)
    {
        if (await ClienteAsync(token, seguimiento: false, ct) is not { } cliente)
            return null;
        return new EstadoBaja(CorreosDestinosAvisos.Enmascarar(cliente.Email!), cliente.RecibeAvisosEstado, await MarketingAsync(cliente.IdCliente, ct));
    }

    // Apaga lo pedido (sin nada indicado, los dos). Repetirla da el mismo resultado; solo un cambio queda en la auditoria.
    public async Task<PreferenciasBaja?> BajaAsync(string? token, bool apagarAvisos, bool apagarMarketing, CancellationToken ct)
    {
        if (await ClienteAsync(token, seguimiento: true, ct) is not { } cliente)
            return null;
        // La hace el cliente del enlace, aunque la peticion traiga la sesion de otra persona.
        var actor = new ActorAuditoria(null, cliente.IdCliente, Security.Roles.Cliente, null);
        using var comoCliente = ActorForzado.Usar(actor);
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var cambios = new Dictionary<string, object?>();
        if (apagarAvisos && await PreferenciaAvisosEstado.CambiarAsync(db, consentimientos, cliente, recibe: false, reloj, ct))
            cambios["avisosEstado"] = false;
        if (apagarMarketing && await consentimientos.RevocarAsync(TitularConsentimiento.Cliente(cliente.IdCliente), DocumentosLegales.Marketing, ct))
            cambios["marketing"] = false;
        if (cambios.Count > 0)
            await auditoria.RegistrarAsync("cliente", cliente.IdCliente.ToString(System.Globalization.CultureInfo.InvariantCulture), AccionesAuditoria.BajaCorreo,
                DetalleAuditoria.Libre(new Dictionary<string, object?>(cambios) { ["via"] = "enlace" }), ct, actor);
        await transaccion.CommitAsync(CancellationToken.None);
        return new PreferenciasBaja(cliente.RecibeAvisosEstado, await MarketingAsync(cliente.IdCliente, CancellationToken.None));
    }

    private async Task<Cliente?> ClienteAsync(string? token, bool seguimiento, CancellationToken ct)
    {
        if (!EnlaceBaja.TryLeerId(token?.Trim(), out var id))
            return null;
        var consulta = seguimiento ? db.Clientes : db.Clientes.AsNoTracking();
        var cliente = await consulta.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        return cliente is not null && EnlaceBaja.Valido(clave.Valor, token!.Trim(), id, cliente.Email, cliente.InhabilitadoEn) ? cliente : null;
    }

    private async Task<bool> MarketingAsync(int idCliente, CancellationToken ct) =>
        await consentimientos.VigenteAsync(TitularConsentimiento.Cliente(idCliente), DocumentosLegales.Marketing, ct) is not null;
}
