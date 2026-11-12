namespace LetrerosHerreraSYNCTIMEapi.Negocio;

// Parametros comunes de todo listado de la API (RFS32): ?pagina=&tamano=&orden=&buscar=
// Los valores fuera de rango se ajustan en vez de rechazarse: un listado nunca falla por su paginacion.
public sealed record ParametrosListado
{
    public const int TamanoPredeterminado = 20;
    public const int TamanoMaximo = 100;
    public const int PaginaMaxima = 100_000;
    public const int LargoMaximoBusqueda = 100;

    public ParametrosListado(int? pagina = null, int? tamano = null, string? orden = null, string? buscar = null)
    {
        Pagina = Math.Clamp(pagina ?? 1, 1, PaginaMaxima);
        Tamano = Math.Clamp(tamano ?? TamanoPredeterminado, 1, TamanoMaximo);
        Orden = Limpiar(orden, 60);
        Buscar = Limpiar(buscar, LargoMaximoBusqueda);
    }

    public int Pagina { get; }
    public int Tamano { get; }

    // "campo" ordena de menor a mayor; "-campo", de mayor a menor.
    public string? Orden { get; }
    public string? Buscar { get; }

    public int Saltar => (Pagina - 1) * Tamano;

    private static string? Limpiar(string? texto, int largoMaximo)
    {
        var limpio = texto?.Trim();
        if (string.IsNullOrEmpty(limpio))
            return null;
        return limpio.Length > largoMaximo ? limpio[..largoMaximo] : limpio;
    }
}

// Respuesta comun de todo listado paginado.
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Elementos, int Total, int Pagina, int Tamano)
{
    public int TotalPaginas => (int)Math.Ceiling(Total / (double)Tamano);
}
