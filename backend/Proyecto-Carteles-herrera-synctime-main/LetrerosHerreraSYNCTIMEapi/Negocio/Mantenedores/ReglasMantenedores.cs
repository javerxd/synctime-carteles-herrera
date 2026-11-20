namespace LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;

// ── Fase 9: reglas de los mantenedores (RFS07, RFU17, RFU18, E12-E15, RNFO08) ──
// Sin HTTP ni base de datos: se prueban solas en LetrerosHerreraSYNCTIMEapi.UnitTests. Los errores van por
// campo, con el nombre que usa el formulario, para mostrarlos junto a cada casilla.

// Codigos estables de los 409 de la fase 9 (el frontend decide con ellos que mostrar).
public static class CodigosMantenedores
{
    public const string YaExiste = "ya_existe";
    public const string Propio = "propio";
    public const string UltimoDueno = "ultimo_dueno";
    public const string LogoSinAutorizacion = "logo_sin_autorizacion";
    public const string UltimoDestino = "ultimo_destino";
    public const string ReemplazoPendiente = "reemplazo_pendiente";
    public const string TransicionInvalida = "transicion_invalida";
}

// Filtro «activo» (o «visible») de los listados: true, false o todos. Sin filtro, gestion ve todos; quien no
// es de gestion ve siempre solo los activos, lo pida o no (eso lo decide el servicio).
public static class FiltroActivo
{
    public const string Todos = "todos";

    // null: mostrar todos; true o false: solo esos. Devuelve false si el texto no es valido.
    public static bool TryLeer(string? texto, out bool? activo)
    {
        activo = null;
        if (string.IsNullOrWhiteSpace(texto))
            return true;
        switch (texto.Trim().ToLowerInvariant())
        {
            case "true":
                activo = true;
                return true;
            case "false":
                activo = false;
                return true;
            case Todos:
                activo = null;
                return true;
            default:
                return false;
        }
    }
}

public sealed record EntradaCategoria(string? Nombre, string? Descripcion);
public sealed record EntradaProducto(int? IdCategoria, string? Nombre, string? Descripcion, decimal? PrecioBaseRef);
public sealed record EntradaMaterialMantenedor(string? Nombre, string? UnidadMedida, decimal? CostoUnitarioAprox);
public sealed record EntradaEmpleado(string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Email, string? Rol);
public sealed record EntradaCartel(bool AbiertoEnLocal, string? MensajeEstado, string? TiempoEstimadoRegreso);

public static class ValidacionMantenedores
{
    public const int LargoNombreCategoria = 80;
    public const int LargoDescripcionCategoria = 300;
    public const int LargoNombreProducto = 100;
    public const int LargoDescripcionProducto = 500;
    public const int LargoNombreMaterial = 100;
    public const int LargoUnidad = 20;
    public const int LargoCorreoEmpleado = 100;
    public const int LargoMensajeCartel = 200;
    public const int LargoRegresoCartel = 100;

    public static Dictionary<string, string[]> Categoria(EntradaCategoria e)
    {
        var errores = new Dictionary<string, string[]>();
        Texto(errores, "nombre", e.Nombre, 2, LargoNombreCategoria, "El nombre");
        Opcional(errores, "descripcion", e.Descripcion, LargoDescripcionCategoria, "La descripción");
        return errores;
    }

    public static Dictionary<string, string[]> Producto(EntradaProducto e)
    {
        var errores = new Dictionary<string, string[]>();
        if (e.IdCategoria is not > 0)
            errores["idCategoria"] = ["Elige la categoría."];
        Texto(errores, "nombre", e.Nombre, 2, LargoNombreProducto, "El nombre");
        Opcional(errores, "descripcion", e.Descripcion, LargoDescripcionProducto, "La descripción");
        if (e.PrecioBaseRef is decimal precio && !Reglas.EsMontoEnPesos(precio))
            errores["precioBaseRef"] = ["El precio base es un monto en pesos, entero y no negativo."];
        return errores;
    }

    public static Dictionary<string, string[]> Material(EntradaMaterialMantenedor e)
    {
        var errores = new Dictionary<string, string[]>();
        Texto(errores, "nombre", e.Nombre, 2, LargoNombreMaterial, "El nombre");
        Texto(errores, "unidadMedida", e.UnidadMedida, 1, LargoUnidad, "La unidad de medida");
        if (e.CostoUnitarioAprox is not decimal costo)
            errores["costoUnitarioAprox"] = ["Escribe el costo unitario aproximado."];
        else if (!Reglas.EsMontoEnPesos(costo))
            errores["costoUnitarioAprox"] = ["El costo es un monto en pesos, entero y no negativo."];
        return errores;
    }

    // El rol debe ser uno de los cuatro del personal (RNFO08); quien llama entrega la lista.
    public static Dictionary<string, string[]> Empleado(EntradaEmpleado e, IReadOnlyCollection<string> rolesValidos)
    {
        var errores = new Dictionary<string, string[]>();
        if (!Negocio.Rut.TryNormalizar(e.Rut, out _))
            errores["rut"] = ["El RUT no es válido. Revisa el número y el dígito verificador, por ejemplo 12.345.678-5."];
        if (ReglaNombre.Error(e.Nombre, "El nombre es obligatorio.") is { } errorNombre)
            errores["nombre"] = [errorNombre];
        if (ReglaNombre.Error(e.Apellido, "El apellido es obligatorio.") is { } errorApellido)
            errores["apellido"] = [errorApellido];
        if (ValidacionCuenta.NormalizarTelefono(e.Telefono) is null)
            errores["telefono"] = ["Escribe un teléfono de 9 dígitos, por ejemplo 9 1234 5678."];
        var correo = NormalizarCorreo(e.Email);
        if (!Reglas.EsCorreo(correo) || correo.Length > LargoCorreoEmpleado)
            errores["email"] = ["Escribe un correo válido, por ejemplo nombre@letrerosherrera.cl. Con él se ingresa al panel."];
        if (e.Rol is null || !rolesValidos.Contains(e.Rol))
            errores["rol"] = [$"Rol inválido. Valores válidos: {string.Join(", ", rolesValidos)}."];
        return errores;
    }

    public static Dictionary<string, string[]> Cartel(EntradaCartel e)
    {
        var errores = new Dictionary<string, string[]>();
        Texto(errores, "mensajeEstado", e.MensajeEstado, 1, LargoMensajeCartel, "El mensaje");
        Opcional(errores, "tiempoEstimadoRegreso", e.TiempoEstimadoRegreso, LargoRegresoCartel, "El regreso estimado");
        return errores;
    }

    public static string NormalizarCorreo(string? correo) => (correo ?? "").Trim().ToLowerInvariant();

    public static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    public static string Resumen(IReadOnlyDictionary<string, string[]> errores, string general = "Revisa los datos del formulario.") =>
        errores.Count == 1 ? errores.First().Value[0] : general;

    private static void Texto(Dictionary<string, string[]> errores, string campo, string? valor, int minimo, int maximo, string nombre)
    {
        var largo = valor?.Trim().Length ?? 0;
        if (largo == 0)
            errores[campo] = [$"{nombre} es obligatorio."];
        else if (largo < minimo || largo > maximo)
            errores[campo] = [minimo > 1 ? $"{nombre} debe tener entre {minimo} y {maximo} caracteres." : $"{nombre} puede tener hasta {maximo} caracteres."];
    }

    private static void Opcional(Dictionary<string, string[]> errores, string campo, string? valor, int maximo, string nombre)
    {
        if ((valor?.Trim().Length ?? 0) > maximo)
            errores[campo] = [$"{nombre} puede tener hasta {maximo} caracteres."];
    }
}

// Reglas del personal que dependen del estado del taller (las cuentas las entrega el servicio).
public static class ReglasPersonal
{
    public const string MensajePropio = "No puedes desactivarte ni cambiarte el rol a ti mismo. Pídeselo a otra persona de gestión.";
    public const string MensajeUltimoDueno = "Debe quedar al menos un dueño activo en el taller.";

    // null si el cambio se puede hacer; si no, el codigo del 409. duenosActivos cuenta al empleado si hoy es
    // dueño activo.
    public static string? RevisarCambio(int idQuienCambia, int idEmpleado, string rolActual, bool activoActual, string rolNuevo, bool activoNuevo,
        int duenosActivos, string rolDueno)
    {
        var cambiaRol = rolActual != rolNuevo;
        var seDesactiva = activoActual && !activoNuevo;
        if (idQuienCambia == idEmpleado && (cambiaRol || seDesactiva))
            return CodigosMantenedores.Propio;
        var dejaDeSerDuenoActivo = activoActual && rolActual == rolDueno && (rolNuevo != rolDueno || !activoNuevo);
        if (dejaDeSerDuenoActivo && duenosActivos <= 1)
            return CodigosMantenedores.UltimoDueno;
        return null;
    }
}
