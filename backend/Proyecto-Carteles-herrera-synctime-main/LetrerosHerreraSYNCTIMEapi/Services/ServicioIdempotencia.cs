using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 11, seccion 7: Idempotency-Key en las rutas que crean ──

public abstract record ReservaIdempotencia
{
    // Primera vez: la peticion sigue; al terminar se guarda su respuesta (2xx) o se libera la clave (error).
    public sealed record Nueva(long IdClave) : ReservaIdempotencia;
    public sealed record Repetida(int Estado, string Respuesta) : ReservaIdempotencia;
    public sealed record Reutilizada : ReservaIdempotencia;
    public sealed record EnProceso : ReservaIdempotencia;
}

// Reserva la clave con un INSERT que el indice unico no deja repetir. Si choca: con otro cuerpo, «clave
// reutilizada»; con el mismo y ya terminada, la respuesta guardada; si la otra peticion sigue en curso, espera unos
// segundos y, si no termina, «en proceso». Una reserva de mas de 10 minutos sin respuesta se da por abandonada (el
// proceso se cayo a medias) y se puede tomar. Usa su propio contexto: nunca se mezcla con lo que guarda la peticion.
public sealed class ServicioIdempotencia(IServiceScopeFactory ambitos, TimeProvider reloj)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(24);
    // Mas que cualquier peticion legitima: solo una reserva que de verdad se abandono (el proceso se cayo) se puede retomar.
    public static readonly TimeSpan Abandonada = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Pausa = TimeSpan.FromMilliseconds(150);
    private const string IndiceUnico = "uq_claves_idempotencia";

    public async Task<ReservaIdempotencia> ReservarAsync(string clave, string ambito, string tipoUsuario, int idUsuario, string hash, CancellationToken ct)
    {
        var limite = reloj.GetUtcNow() + EsperaMaxima;
        var insertar = true;
        while (true)
        {
            await using var alcance = ambitos.CreateAsyncScope();
            var db = alcance.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
            var ahora = reloj.GetUtcNow();
            if (insertar)
            {
                var fila = new ClaveIdempotencia
                {
                    Clave = clave, Ambito = ambito, TipoUsuario = tipoUsuario, IdUsuario = idUsuario, HashCuerpo = hash,
                    Fecha = Segundo(ahora), Vence = Segundo(ahora + Vigencia)
                };
                db.ClavesIdempotencia.Add(fila);
                try
                {
                    await db.SaveChangesAsync(ct);
                    return new ReservaIdempotencia.Nueva(fila.IdClave);
                }
                catch (DbUpdateException e) when (ErroresSql.EsDuplicado(e, IndiceUnico))
                {
                    db.Entry(fila).State = EntityState.Detached;
                }
            }

            // Mientras la otra peticion sigue en curso, solo se lee (sin insertar de nuevo en cada vuelta).
            var existente = await db.ClavesIdempotencia.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Clave == clave && c.Ambito == ambito && c.TipoUsuario == tipoUsuario && c.IdUsuario == idUsuario, ct);
            insertar = existente is null;
            if (existente is null)
                continue;
            // Vencida, o reservada por una peticion que nunca termino: se borra (solo si sigue igual) y se vuelve a intentar.
            if (existente.Vence <= ahora || (existente.EstadoHttp is null && existente.Fecha < ahora - Abandonada))
            {
                await db.ClavesIdempotencia.Where(c => c.IdClave == existente.IdClave && c.EstadoHttp == existente.EstadoHttp).ExecuteDeleteAsync(ct);
                insertar = true;
                continue;
            }
            if (!string.Equals(existente.HashCuerpo, hash, StringComparison.Ordinal))
                return new ReservaIdempotencia.Reutilizada();
            if (existente.EstadoHttp is int estado)
                return new ReservaIdempotencia.Repetida(estado, existente.Respuesta ?? "");
            if (reloj.GetUtcNow() >= limite)
                return new ReservaIdempotencia.EnProceso();
            await Task.Delay(Pausa, reloj, ct);
        }
    }

    public async Task GuardarAsync(long idClave, int estado, string respuesta)
    {
        await using var alcance = ambitos.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        await db.ClavesIdempotencia.Where(c => c.IdClave == idClave)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EstadoHttp, estado).SetProperty(c => c.Respuesta, respuesta), CancellationToken.None);
    }

    // Un error no se guarda: la clave queda libre para reintentar con el formulario corregido.
    public async Task LiberarAsync(long idClave)
    {
        await using var alcance = ambitos.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        await db.ClavesIdempotencia.Where(c => c.IdClave == idClave && c.EstadoHttp == null).ExecuteDeleteAsync(CancellationToken.None);
    }

    // La columna guarda segundos: se trunca para que la comparacion de vencimiento sea exacta.
    private static DateTimeOffset Segundo(DateTimeOffset instante) => new(instante.Ticks - instante.Ticks % TimeSpan.TicksPerSecond, instante.Offset);
}
