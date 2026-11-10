using LetrerosHerreraSYNCTIMEapi.Models;

namespace LetrerosHerreraSYNCTIMEapi.Repositories;

public interface ISolicitudRepository
{
    Task<IReadOnlyList<Solicitud>> GetAllAsync(CancellationToken ct);
    Task<Solicitud?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ClienteExistsAsync(int idCliente, CancellationToken ct);
    Task<bool> ProductosDisponiblesAsync(IEnumerable<int> idsProducto, CancellationToken ct);
    Task<bool> TienePedidoAsync(int idSolicitud, CancellationToken ct);
    Task AddAsync(Solicitud solicitud, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
