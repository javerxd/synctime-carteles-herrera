using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Correo;

// Despierta al trabajador de la cola en cuanto hay algo nuevo, para que un aviso salga en segundos y no
// al proximo sondeo. Si nadie espera, el aviso queda guardado (como mucho uno) y no se pierde.
public sealed class SenalColaCorreo
{
    private readonly SemaphoreSlim senal = new(0, 1);

    public void Avisar()
    {
        try
        {
            senal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Ya habia un aviso pendiente: basta con uno.
        }
    }

    // true si llego un aviso; false si se cumplio la espera sin novedades.
    public Task<bool> EsperarAsync(TimeSpan espera, CancellationToken ct) => senal.WaitAsync(espera, ct);
}

// Implementacion de IServicioCorreo: guarda el mensaje en correos_salientes y avisa al trabajador.
// Usa su propio contexto de base de datos, asi que nunca guarda por accidente cambios a medio hacer de
// quien lo llama; y como solo inserta una fila, la peticion no espera al servidor de correo.
public sealed class ServicioCorreoEnCola(IServiceScopeFactory ambitos, SenalColaCorreo senal, TimeProvider reloj) : IServicioCorreo
{
    public async Task EncolarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        if (mensaje.Para.Count == 0)
            throw new ArgumentException("El correo no tiene destinatarios.", nameof(mensaje));
        if (mensaje.Para.Any(d => !Reglas.EsCorreo(d)))
            throw new ArgumentException("Uno de los destinatarios no es una dirección válida.", nameof(mensaje));

        var destinatarios = string.Join(';', mensaje.Para);
        if (destinatarios.Length > 1000)
            throw new ArgumentException("Demasiados destinatarios para un solo correo.", nameof(mensaje));

        var ahora = reloj.GetUtcNow();
        // La columna guarda segundos y SQL Server redondea: sin truncar, un correo encolado a las :00,6
        // quedaria para las :01 y el trabajador no lo veria hasta el segundo siguiente.
        var disponibleDesde = new DateTimeOffset(ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, ahora.Offset);
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        db.CorreosSalientes.Add(new CorreoSaliente
        {
            Tipo = mensaje.Tipo,
            Referencia = mensaje.Referencia,
            Destinatarios = destinatarios,
            ResponderA = Reglas.EsCorreo(mensaje.ResponderA) ? mensaje.ResponderA : null,
            Asunto = PlantillaCorreo.AsuntoSeguro(mensaje.Asunto),
            CuerpoHtml = mensaje.Html,
            CuerpoTexto = mensaje.Texto,
            Estado = EstadosCorreo.Pendiente,
            ProximoIntento = disponibleDesde,
            FechaCreacion = ahora
        });
        await db.SaveChangesAsync(ct);
        senal.Avisar();
    }

    public async Task<int> ContarAsync(string tipo, DateTimeOffset desde, CancellationToken ct)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var db = ambito.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        return await db.CorreosSalientes.CountAsync(c => c.Tipo == tipo && c.FechaCreacion >= desde, ct);
    }
}
