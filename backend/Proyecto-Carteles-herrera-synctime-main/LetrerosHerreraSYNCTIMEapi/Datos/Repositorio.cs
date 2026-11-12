using System.Linq.Expressions;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Datos;

// Capa de acceso a datos (RNFO10). Los servicios de negocio no usan el DbContext: piden lo que
// necesitan a un repositorio y confirman los cambios con la unidad de trabajo.
//
// Patron Repository: cada entidad principal tiene su repositorio, que hereda de esta base y agrega
// sus propias consultas. Asi las consultas viven en un solo lugar y el negocio se prueba sin base.
public abstract class RepositorioBase<T>(LetrerosHerreradbContext db) where T : class
{
    protected LetrerosHerreradbContext Db { get; } = db;

    // Consulta de solo lectura: no rastrea cambios, que es lo correcto para listar y mostrar.
    protected IQueryable<T> Lectura => Db.Set<T>().AsNoTracking();

    public ValueTask<T?> BuscarAsync(int id, CancellationToken ct = default) => Db.Set<T>().FindAsync([id], ct);

    public void Agregar(T entidad) => Db.Set<T>().Add(entidad);

    public void Quitar(T entidad) => Db.Set<T>().Remove(entidad);
}

// Patron Unit of Work: varios repositorios comparten un mismo contexto por peticion, y todos sus
// cambios se guardan juntos en una sola transaccion.
public interface IUnidadDeTrabajo
{
    Task<int> GuardarAsync(CancellationToken ct = default);
}

public sealed class UnidadDeTrabajo(LetrerosHerreradbContext db) : IUnidadDeTrabajo
{
    public Task<int> GuardarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public static class ConsultasPaginadas
{
    // Cuenta el total y trae solo la pagina pedida. La consulta debe llegar ya ordenada:
    // sin un orden estable, dos paginas podrian repetir u omitir filas.
    public static async Task<ResultadoPaginado<T>> PaginarAsync<T>(
        this IQueryable<T> consulta, ParametrosListado parametros, CancellationToken ct = default)
    {
        var total = await consulta.CountAsync(ct);
        IReadOnlyList<T> elementos = total <= parametros.Saltar
            ? []
            : await consulta.Skip(parametros.Saltar).Take(parametros.Tamano).ToListAsync(ct);
        return new ResultadoPaginado<T>(elementos, total, parametros.Pagina, parametros.Tamano);
    }

    // Ordena por un campo de la lista permitida: "campo" ascendente, "-campo" descendente.
    // Un campo desconocido usa el orden predeterminado en vez de fallar, y nunca llega texto del
    // usuario a la consulta: solo se elige entre expresiones escritas en el codigo.
    //
    // "desempate" debe ser un valor unico por fila, normalmente el id. Sin el, dos filas con el mismo
    // valor en el campo elegido (un estado, una fecha) podrian cambiar de pagina entre una consulta y otra.
    public static IQueryable<T> Ordenar<T>(
        this IQueryable<T> consulta,
        string? orden,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> campos,
        string predeterminado,
        Expression<Func<T, object?>> desempate)
    {
        var (campoPredeterminado, predeterminadoDescendente) = Separar(predeterminado);
        if (!campos.TryGetValue(campoPredeterminado, out var expresion))
            throw new ArgumentException($"El orden predeterminado '{predeterminado}' no esta en la lista de campos permitidos.", nameof(predeterminado));
        var descendente = predeterminadoDescendente;

        if (!string.IsNullOrWhiteSpace(orden))
        {
            var (campo, pedidoDescendente) = Separar(orden.Trim());
            if (campos.TryGetValue(campo, out var pedida))
                (expresion, descendente) = (pedida, pedidoDescendente);
        }

        var ordenada = descendente ? consulta.OrderByDescending(expresion) : consulta.OrderBy(expresion);
        return ordenada.ThenBy(desempate);
    }

    private static (string Campo, bool Descendente) Separar(string orden) =>
        orden.StartsWith('-') ? (orden[1..], true) : (orden, false);
}
