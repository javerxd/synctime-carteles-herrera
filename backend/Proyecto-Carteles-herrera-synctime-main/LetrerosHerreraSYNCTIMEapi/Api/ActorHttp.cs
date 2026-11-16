using System.Globalization;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Security;

namespace LetrerosHerreraSYNCTIMEapi.Api;

// Fase 11: quien hace la operacion en curso, para la bitacora, sacado del token de la peticion. Sin peticion o sin
// sesion (el trabajador de la cola, la purga, un enlace publico) es el «sistema».
public sealed class ActorHttp(IHttpContextAccessor accesor) : IActorActual
{
    public ActorAuditoria Actual()
    {
        var contexto = accesor.HttpContext;
        var ip = contexto?.Connection.RemoteIpAddress?.ToString();
        if (ActorForzado.Actual is { } forzado)
            return forzado with { Ip = forzado.Ip ?? ip };
        if (contexto is null)
            return ActorAuditoria.Sistema;
        var usuario = contexto.User;
        if (usuario.Identity?.IsAuthenticated != true
            || !int.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return ActorAuditoria.Sistema with { Ip = ip };
        var rol = usuario.FindFirstValue(ClaimTypes.Role) ?? AccionesAuditoriaRol.Desconocido;
        return usuario.IsInRole(Roles.Cliente)
            ? new ActorAuditoria(null, id, Roles.Cliente, ip)
            : new ActorAuditoria(id, null, rol, ip);
    }
}

internal static class AccionesAuditoriaRol
{
    public const string Desconocido = "desconocido";
}
