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

    // Lo que necesita el aviso al taller de una solicitud nueva, en una sola consulta.
    Task<SolicitudParaAviso?> DatosParaAvisoAsync(int idSolicitud, CancellationToken ct);
}

public sealed record SolicitudParaAviso(
    int IdSolicitud,
    DateTime FechaSolicitud,
    string Origen,
    string? RegistradaPor,
    string ClienteNombre,
    string ClienteTelefono,
    string? ClienteEmail,
    string? Descripcion,
    IReadOnlyList<DetalleParaAviso> Detalles);

public sealed record DetalleParaAviso(string Producto, string Descripcion, int? AnchoMm, int? AltoMm, int Cantidad);
