using LetrerosHerreraSYNCTIMEapi.Models;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Repositories;

public class SolicitudRepository(LetrerosHerreradbContext db) : ISolicitudRepository
{
    // Las solicitudes con eliminacion logica solo siguen contando en las metricas.
    private IQueryable<Solicitud> Vigentes => db.Solicitudes.Where(s => !s.EliminadoLogico);

    public async Task<IReadOnlyList<Solicitud>> GetAllAsync(CancellationToken ct) =>
        await Vigentes
            .AsNoTracking()
            .Include(s => s.Detalles)
            .Include(s => s.Cliente)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync(ct);

    public async Task<Solicitud?> GetByIdAsync(int id, CancellationToken ct) =>
        await Vigentes
            .Include(s => s.Detalles)
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.IdSolicitud == id, ct);

    public async Task<bool> ClienteExistsAsync(int idCliente, CancellationToken ct) =>
        await db.Clientes.AnyAsync(c => c.IdCliente == idCliente, ct);

    public async Task<bool> ProductosDisponiblesAsync(IEnumerable<int> idsProducto, CancellationToken ct)
    {
        var ids = idsProducto.Distinct().ToList();
        return await db.Productos.CountAsync(p => ids.Contains(p.IdProducto) && p.Activo && p.Categoria!.Activo, ct) == ids.Count;
    }

    public async Task<bool> TienePedidoAsync(int idSolicitud, CancellationToken ct) =>
        await db.Pedidos.AnyAsync(p => p.IdSolicitud == idSolicitud, ct);

    public async Task AddAsync(Solicitud solicitud, CancellationToken ct)
    {
        await db.Solicitudes.AddAsync(solicitud, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct) =>
        await db.SaveChangesAsync(ct);
}
