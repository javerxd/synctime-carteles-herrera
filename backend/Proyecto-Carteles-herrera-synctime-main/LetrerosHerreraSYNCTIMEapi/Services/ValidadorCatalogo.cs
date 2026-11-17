using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 12B: valida los letreros de una solicitud o de un pedido directo contra el catalogo real: el producto debe
// estar ofrecido, y sus medidas y opciones, ser las que el taller hace. Lo usan ServicioSolicitudes y ServicioPedidos.
public static class ValidadorCatalogo
{
    public static async Task ValidarAsync(LetrerosHerreradbContext db, IReadOnlyList<EntradaDetalle>? detalles,
        Dictionary<string, string[]> errores, CancellationToken ct)
    {
        if (detalles is not { Count: > 0 and <= ValidacionSolicitud.MaxDetalles })
            return;

        var pedidos = detalles.Select(d => d.IdProducto).Distinct().ToList();
        var productos = await db.Productos.AsNoTracking()
            .Where(p => pedidos.Contains(p.IdProducto) && p.Activo && p.Categoria!.Activo)
            .Select(p => new
            {
                p.IdProducto, p.Nombre, p.AnchoMinMm, p.AnchoMaxMm, p.AltoMinMm, p.AltoMaxMm,
                Fijas = p.MedidasFijas.Select(m => new { m.AnchoMm, m.AltoMm }).ToList(),
                Opciones = p.Opciones.OrderBy(o => o.Orden).Select(o => new { o.Grupo, o.Valor }).ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);
        var limites = productos.ToDictionary(p => p.IdProducto, p => new LimitesProducto(
            p.Nombre, p.AnchoMinMm, p.AnchoMaxMm, p.AltoMinMm, p.AltoMaxMm,
            p.Fijas.Select(m => (m.AnchoMm, m.AltoMm)).ToList(),
            p.Opciones.GroupBy(o => o.Grupo).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(o => o.Valor).ToList())));

        for (var i = 0; i < detalles.Count; i++)
        {
            var campo = $"detalles[{i}]";
            var detalle = detalles[i];
            if (!limites.TryGetValue(detalle.IdProducto, out var producto))
            {
                if (!errores.ContainsKey($"{campo}.idProducto"))
                    errores[$"{campo}.idProducto"] = [ValidacionSolicitud.MensajeProducto];
                continue;
            }

            // Las medidas fuera del rango general (1 a 50 000 mm, par incompleto) ya tienen su error: no se repite otro.
            if (!errores.ContainsKey($"{campo}.anchoMm") && !errores.ContainsKey($"{campo}.altoMm"))
            {
                foreach (var (clave, mensajes) in ReglasCatalogo.ValidarMedidas(detalle.AnchoMm, detalle.AltoMm, producto))
                    errores[$"{campo}.{clave}"] = mensajes;
            }
            if (ReglasCatalogo.ValidarOpciones(detalle.Opciones, producto) is { } error)
                errores[$"{campo}.opciones"] = [error];
        }
    }
}
