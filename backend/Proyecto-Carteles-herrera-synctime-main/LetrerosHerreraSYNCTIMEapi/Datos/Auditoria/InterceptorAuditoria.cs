using System.Runtime.CompilerServices;
using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;

// Quien hace la operacion. Sin usuario (el trabajador de la cola, la purga, un enlace sin sesion) el rol es «sistema».
public sealed record ActorAuditoria(int? IdEmpleado, int? IdCliente, string Rol, string? Ip)
{
    public static readonly ActorAuditoria Sistema = new(null, null, AccionesAuditoria.RolSistema, null);
}

// Lo implementa la capa de la API (con la peticion en curso); los datos no conocen HTTP.
public interface IActorActual
{
    ActorAuditoria Actual();
}

public sealed class SinActor : IActorActual
{
    public ActorAuditoria Actual() => ActorForzado.Actual ?? ActorAuditoria.Sistema;
}

// Fase 11: para las acciones que hace alguien distinto de quien tiene la sesion. La baja desde el enlace del
// correo la hace el cliente duenio del enlace, aunque el navegador tenga abierta la sesion de otra persona: mientras
// dura el bloque, la bitacora (el interceptor y el registro a mano) la anota a su nombre.
public static class ActorForzado
{
    private static readonly AsyncLocal<ActorAuditoria?> actual = new();

    public static ActorAuditoria? Actual => actual.Value;

    public static IDisposable Usar(ActorAuditoria actor)
    {
        var previo = actual.Value;
        actual.Value = actor;
        return new Restaurar(previo);
    }

    private sealed class Restaurar(ActorAuditoria? previo) : IDisposable
    {
        public void Dispose() => actual.Value = previo;
    }
}

// Registro a mano: para las escrituras que el interceptor no ve (ExecuteUpdate, ExecuteDelete) y para las acciones
// que no son un cambio de una entidad (aviso enviado, baja, descarga de datos). Se guarda con el contexto de quien
// llama, asi que entra en su misma transaccion.
public interface IAuditoria
{
    Task RegistrarAsync(string entidad, string? idEntidad, string accion, string? detalle, CancellationToken ct, ActorAuditoria? actor = null);

    // Solo lo agrega al contexto: se guarda con el proximo SaveChanges de quien llama (y se pierde con el, si falla).
    void Anotar(string entidad, string? idEntidad, string accion, string? detalle, ActorAuditoria? actor = null);
}

public sealed class Auditoria(LetrerosHerreradbContext db, IActorActual actores, TimeProvider reloj) : IAuditoria
{
    public async Task RegistrarAsync(string entidad, string? idEntidad, string accion, string? detalle, CancellationToken ct, ActorAuditoria? actor = null)
    {
        var fila = Fila.Crear(actor ?? actores.Actual(), entidad, idEntidad, accion, detalle, reloj.GetUtcNow());
        db.Auditoria.Add(fila);
        await db.SaveChangesAsync(ct);
        db.Entry(fila).State = EntityState.Detached;
    }

    public void Anotar(string entidad, string? idEntidad, string accion, string? detalle, ActorAuditoria? actor = null) =>
        db.Auditoria.Add(Fila.Crear(actor ?? actores.Actual(), entidad, idEntidad, accion, detalle, reloj.GetUtcNow()));
}

public static class Fila
{
    public static RegistroAuditoria Crear(ActorAuditoria actor, string entidad, string? idEntidad, string accion, string? detalle, DateTimeOffset fecha) => new()
    {
        Fecha = fecha,
        IdEmpleado = actor.IdEmpleado,
        IdCliente = actor.IdEmpleado is null ? actor.IdCliente : null,
        Rol = Recortar(actor.Rol, 20),
        Entidad = Recortar(entidad, 40)!,
        IdEntidad = Recortar(idEntidad, 40),
        Accion = Recortar(accion, 30)!,
        // Un detalle demasiado largo no se pierde en silencio: queda marcado como truncado.
        Detalle = detalle is { Length: > DetalleAuditoria.Maximo } ? "{\"truncado\":true}" : detalle,
        Ip = Recortar(actor.Ip, 45)
    };

    private static string? Recortar(string? texto, int largo) => texto is null || texto.Length <= largo ? texto : texto[..largo];
}

// Fase 11 (RFS12): registra solo, con cada SaveChanges, quien creo, modifico o elimino una entidad auditada
// (ReglasAuditoria). Las modificaciones y eliminaciones se registran en el mismo guardado (sus identificadores ya se
// conocen). Las creaciones, despues de guardar, cuando la base ya asigno el identificador, en la misma transaccion:
// si quien guarda no abrio una, el interceptor abre la suya para que el cambio y su registro se confirmen juntos.
public sealed class InterceptorAuditoria(IActorActual actores, TimeProvider reloj) : SaveChangesInterceptor
{
    private sealed class Pendiente
    {
        public List<(EntityEntry Entrada, EntidadAuditada Reglas)> Creadas { get; } = [];
        public List<RegistroAuditoria> Listos { get; } = [];
        public List<RegistroAuditoria> EnLote { get; } = [];
        public IDbContextTransaction? Propia { get; set; }
        public bool Escribiendo { get; set; }
    }

    private readonly ConditionalWeakTable<DbContext, Pendiente> estados = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } contexto && Tomar(contexto) is { Creadas.Count: > 0 } pendiente && pendiente.Propia is null
            && contexto.Database.CurrentTransaction is null)
            pendiente.Propia = contexto.Database.BeginTransaction();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } contexto && Tomar(contexto) is { Creadas.Count: > 0 } pendiente && pendiente.Propia is null
            && contexto.Database.CurrentTransaction is null)
            pendiente.Propia = await contexto.Database.BeginTransactionAsync(cancellationToken);
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } contexto && estados.TryGetValue(contexto, out var pendiente) && !pendiente.Escribiendo)
        {
            try
            {
                Soltar(contexto, pendiente.EnLote);
                if (Completar(contexto, pendiente))
                {
                    pendiente.Escribiendo = true;
                    contexto.SaveChanges();
                    Soltar(contexto, pendiente.Listos);
                }
                pendiente.Propia?.Commit();
            }
            finally
            {
                Terminar(contexto, pendiente);
            }
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } contexto && estados.TryGetValue(contexto, out var pendiente) && !pendiente.Escribiendo)
        {
            try
            {
                Soltar(contexto, pendiente.EnLote);
                if (Completar(contexto, pendiente))
                {
                    pendiente.Escribiendo = true;
                    // Sin el token: el cambio ya se guardo y su registro no debe quedar a medias.
                    await contexto.SaveChangesAsync(CancellationToken.None);
                    Soltar(contexto, pendiente.Listos);
                }
                if (pendiente.Propia is { } propia)
                    await propia.CommitAsync(CancellationToken.None);
            }
            finally
            {
                await TerminarAsync(contexto, pendiente);
            }
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } contexto && estados.TryGetValue(contexto, out var pendiente) && !pendiente.Escribiendo)
            Terminar(contexto, pendiente);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } contexto && estados.TryGetValue(contexto, out var pendiente) && !pendiente.Escribiendo)
            await TerminarAsync(contexto, pendiente);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        if (eventData.Context is { } contexto && estados.TryGetValue(contexto, out var pendiente) && !pendiente.Escribiendo)
            Terminar(contexto, pendiente);
    }

    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } contexto && estados.TryGetValue(contexto, out var pendiente) && !pendiente.Escribiendo)
            await TerminarAsync(contexto, pendiente);
    }

    // Toma los cambios de las entidades auditadas. null si no hay nada que registrar (o si es la escritura de la bitacora).
    private Pendiente? Tomar(DbContext contexto)
    {
        if (estados.TryGetValue(contexto, out var enCurso) && enCurso.Escribiendo)
            return null;
        var actor = actores.Actual();
        var ahora = reloj.GetUtcNow();
        var pendiente = new Pendiente();
        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (!ReglasAuditoria.Entidades.TryGetValue(entrada.Metadata.ClrType, out var reglas))
                continue;
            switch (entrada.State)
            {
                case EntityState.Added:
                    pendiente.Creadas.Add((entrada, reglas));
                    break;
                case EntityState.Modified:
                    var cambios = entrada.Properties
                        .Where(p => p.IsModified && !p.Metadata.IsPrimaryKey() && !reglas.Ignora(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue))
                        .Select(p => new CambioDeCampo(p.Metadata.Name, p.OriginalValue, p.CurrentValue))
                        .ToList();
                    if (cambios.Count == 0)
                        break;
                    pendiente.Listos.Add(Fila.Crear(actor, reglas.Nombre, Clave(entrada), AccionesAuditoria.Modificar,
                        DetalleAuditoria.DeCambios(reglas, cambios, Contexto(entrada, reglas)), ahora));
                    break;
                case EntityState.Deleted:
                    pendiente.Listos.Add(Fila.Crear(actor, reglas.Nombre, Clave(entrada), AccionesAuditoria.Eliminar,
                        DetalleAuditoria.DeCambios(reglas, [], Contexto(entrada, reglas)), ahora));
                    break;
            }
        }
        if (pendiente.Creadas.Count == 0 && pendiente.Listos.Count == 0)
        {
            estados.Remove(contexto);
            return null;
        }
        // Las modificaciones y eliminaciones van en este mismo guardado.
        contexto.Set<RegistroAuditoria>().AddRange(pendiente.Listos);
        pendiente.EnLote.AddRange(pendiente.Listos);
        pendiente.Listos.Clear();
        estados.AddOrUpdate(contexto, pendiente);
        return pendiente;
    }

    // Despues de guardar: los creados ya tienen su identificador (y los valores que puso la base).
    private bool Completar(DbContext contexto, Pendiente pendiente)
    {
        var actor = actores.Actual();
        var ahora = reloj.GetUtcNow();
        foreach (var (entrada, reglas) in pendiente.Creadas)
        {
            var campos = entrada.Properties
                .Where(p => !p.Metadata.IsPrimaryKey() && !reglas.Ignora(p.Metadata.Name) && p.CurrentValue is not null)
                .Select(p => new CambioDeCampo(p.Metadata.Name, null, p.CurrentValue))
                .ToList();
            pendiente.Listos.Add(Fila.Crear(actor, reglas.Nombre, Clave(entrada), AccionesAuditoria.Crear, DetalleAuditoria.DeCambios(reglas, campos), ahora));
        }
        pendiente.Creadas.Clear();
        if (pendiente.Listos.Count == 0)
            return false;
        contexto.Set<RegistroAuditoria>().AddRange(pendiente.Listos);
        return true;
    }

    // La bitacora no queda en seguimiento: solo se inserta. Si el guardado fallo, sus filas tampoco quedan pendientes
    // (quien reintenta con el mismo contexto no debe registrar un cambio que no ocurrio).
    private static void Soltar(DbContext contexto, List<RegistroAuditoria> filas)
    {
        foreach (var fila in filas)
            contexto.Entry(fila).State = EntityState.Detached;
        filas.Clear();
    }

    private void Terminar(DbContext contexto, Pendiente pendiente)
    {
        estados.Remove(contexto);
        Soltar(contexto, pendiente.EnLote);
        if (pendiente.Propia is { } propia)
        {
            pendiente.Propia = null;
            propia.Dispose();
        }
    }

    private async Task TerminarAsync(DbContext contexto, Pendiente pendiente)
    {
        estados.Remove(contexto);
        Soltar(contexto, pendiente.EnLote);
        if (pendiente.Propia is { } propia)
        {
            pendiente.Propia = null;
            await propia.DisposeAsync();
        }
    }

    private static List<CambioDeCampo>? Contexto(EntityEntry entrada, EntidadAuditada reglas) =>
        reglas.Siempre?.Select(nombre => new CambioDeCampo(nombre, entrada.Property(nombre).OriginalValue, entrada.Property(nombre).CurrentValue)).ToList();

    private static string Clave(EntityEntry entrada) =>
        string.Join("-", entrada.Metadata.FindPrimaryKey()!.Properties.Select(p => Convert.ToString(entrada.Property(p.Name).CurrentValue,
            System.Globalization.CultureInfo.InvariantCulture)));
}
