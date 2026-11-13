using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Correo;

// UNICO lugar que conoce Correo:AvisosTaller (lo comprueba ArquitecturaTests). Admite uno o mas correos
// separados por coma o punto y coma, para no fijar el del dueño en el codigo; en desarrollo se escribe
// en User Secrets y en produccion en una variable de entorno (Correo__AvisosTaller).
// Fase 9: leera primero el correo que el dueño guarde en la base y, si no hay, este valor.
public sealed class DestinatariosAvisosDesdeConfiguracion(IConfiguration configuracion, ILogger<DestinatariosAvisosDesdeConfiguracion> logger)
    : IDestinatariosAvisos
{
    public const string Clave = "Correo:AvisosTaller";

    public Task<IReadOnlyList<string>> ObtenerAsync(CancellationToken ct)
    {
        var destinatarios = ListaDeCorreos.Separar(configuracion[Clave], out var invalidos);

        // Sin las direcciones en el registro: son datos personales y no hacen falta para corregir el error.
        if (invalidos > 0)
            logger.LogWarning("{Clave} tiene {Invalidos} direccion(es) no valida(s); se omiten.", Clave, invalidos);
        if (destinatarios.Count == 0)
            logger.LogWarning("No hay destinatarios para los avisos del taller: configure {Clave}.", Clave);

        return Task.FromResult(destinatarios);
    }
}
