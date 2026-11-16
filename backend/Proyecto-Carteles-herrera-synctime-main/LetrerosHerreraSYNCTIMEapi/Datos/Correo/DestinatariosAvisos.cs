using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Correo;

// UNICO lugar que conoce Correo:AvisosTaller (lo comprueba ArquitecturaTests). Fase 9: primero se usan los
// destinos «activo» que el dueño o el administrador guardaron desde su perfil (destinatarios_avisos); solo
// si no hay ninguno se usa ese valor de la configuracion, que queda como respaldo. Admite uno o mas correos
// separados por coma o punto y coma; en desarrollo se escribe en User Secrets y en produccion en una
// variable de entorno (Correo__AvisosTaller).
// Sin cache: cada aviso lee la base, asi un destino recien confirmado recibe el siguiente aviso.
public sealed class DestinatariosAvisosDesdeBaseYConfiguracion(
    LetrerosHerreradbContext db,
    IConfiguration configuracion,
    TablaDestinosAusente tablaAusente,
    ILogger<DestinatariosAvisosDesdeBaseYConfiguracion> logger) : IDestinatariosAvisos
{
    public const string Clave = "Correo:AvisosTaller";

    public async Task<IReadOnlyList<string>> ObtenerAsync(CancellationToken ct) => (await LeerAsync(ct)).Correos;

    public Task<DestinatariosVigentes> LeerAsync(CancellationToken ct) => LeerAsync(origen: null, ct);

    public Task<DestinatariosVigentes> LeerAsync(OrigenAviso origen, CancellationToken ct) => LeerAsync((OrigenAviso?)origen, ct);

    // Sin origen: todos los activos (la prueba desde el perfil y los avisos de cambios de destino).
    private async Task<DestinatariosVigentes> LeerAsync(OrigenAviso? origen, CancellationToken ct)
    {
        List<DestinoLeido> activos = [];
        if (!tablaAusente.Recordada())
        {
            try
            {
                activos = await db.DestinatariosAvisos.AsNoTracking()
                    .Where(d => d.Estado == EstadosDestinatario.Activo)
                    .OrderBy(d => d.IdDestinatario)
                    .Select(d => new DestinoLeido(d.Correo, d.RecibeEnLinea, d.RecibePresenciales))
                    .ToListAsync(ct);
            }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 208 && e.Message.Contains("destinatarios_avisos", StringComparison.Ordinal))
            {
                // Transitorio: una base sin la migracion MantenedoresYAvisos (como la real, hasta aplicarla con
                // respaldo). Los avisos siguen llegando al respaldo en vez de perderse; se recuerda 5 minutos y se
                // registra una vez. Cualquier otro error de la base sigue su curso.
                if (tablaAusente.Recordar())
                    logger.LogWarning("La base aun no tiene la tabla de destinos de avisos (falta una migracion): se usa {Clave}.", Clave);
            }
        }
        if (activos.Count > 0)
            return new DestinatariosVigentes(activos
                .Where(d => origen switch
                {
                    OrigenAviso.EnLinea => d.EnLinea,
                    OrigenAviso.Presencial => d.Presenciales,
                    _ => true
                })
                .Select(d => d.Correo).ToList(), DesdeRespaldo: false);

        // El respaldo de la configuracion no tiene preferencia: recibe lo que recibia antes de la fase 11.
        if (origen == OrigenAviso.Presencial)
            return new DestinatariosVigentes([], DesdeRespaldo: true);
        var destinatarios = ListaDeCorreos.Separar(configuracion[Clave], out var invalidos);

        // Sin las direcciones en el registro: son datos personales y no hacen falta para corregir el error.
        if (invalidos > 0)
            logger.LogWarning("{Clave} tiene {Invalidos} direccion(es) no valida(s); se omiten.", Clave, invalidos);
        if (destinatarios.Count == 0)
            logger.LogWarning("No hay destinatarios para los avisos del taller: agregue uno desde el perfil o configure {Clave}.", Clave);

        return new DestinatariosVigentes(destinatarios, DesdeRespaldo: true);
    }

    private sealed record DestinoLeido(string Correo, bool EnLinea, bool Presenciales);
}

// Recuerda por 5 minutos que la base no tiene la tabla destinatarios_avisos, para no consultar (ni registrar)
// el mismo error en cada aviso. Unica instancia por API.
public sealed class TablaDestinosAusente(TimeProvider reloj)
{
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(5);
    private long hasta = DateTimeOffset.MinValue.UtcTicks;

    public bool Recordada() => reloj.GetUtcNow().UtcTicks < Interlocked.Read(ref hasta);

    // true si es la primera vez en este periodo (para registrarlo una sola vez).
    public bool Recordar()
    {
        var nuevo = reloj.GetUtcNow().Add(Duracion).UtcTicks;
        var anterior = Interlocked.Exchange(ref hasta, nuevo);
        return reloj.GetUtcNow().UtcTicks >= anterior;
    }

    public void Olvidar() => Interlocked.Exchange(ref hasta, DateTimeOffset.MinValue.UtcTicks);
}
