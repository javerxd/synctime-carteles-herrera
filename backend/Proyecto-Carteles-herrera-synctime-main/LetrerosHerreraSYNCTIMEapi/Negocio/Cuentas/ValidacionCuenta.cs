namespace LetrerosHerreraSYNCTIMEapi.Negocio;

// Reglas de los datos de una cuenta de cliente (E01). Los errores van por campo, con el mismo nombre que
// usa el formulario, para mostrarlos junto a cada casilla.
public static class ValidacionCuenta
{
    public const int LargoCorreo = 100;
    public const int LargoDireccion = 255;

    public static Dictionary<string, string[]> Validar(string? rut, string? nombre, string? apellido, string? telefono, string? direccion)
    {
        var errores = new Dictionary<string, string[]>();
        if (ReglaNombre.Error(nombre, "Escribe tu nombre.") is { } errorNombre)
            errores["nombre"] = [errorNombre];
        if (ReglaNombre.Error(apellido, "Escribe tu apellido.") is { } errorApellido)
            errores["apellido"] = [errorApellido];
        if (NormalizarTelefono(telefono) is null)
            errores["telefono"] = ["Escribe un teléfono de 9 dígitos, por ejemplo 9 1234 5678."];
        if (!string.IsNullOrWhiteSpace(rut) && NormalizarRut(rut) is null)
            errores["rut"] = ["El RUT no es válido. Revisa el número y el dígito verificador, por ejemplo 12.345.678-5."];
        if ((direccion?.Trim().Length ?? 0) > LargoDireccion)
            errores["direccion"] = [$"La dirección puede tener hasta {LargoDireccion} caracteres."];
        return errores;
    }

    public static void ValidarCorreo(string correoNormalizado, IDictionary<string, string[]> errores)
    {
        if (!Reglas.EsCorreo(correoNormalizado) || correoNormalizado.Length > LargoCorreo)
            errores["correo"] = ["Escribe un correo válido, por ejemplo nombre@correo.cl. Con él ingresas y recuperas tu contraseña."];
    }

    // Nueve digitos, sin espacios ni +56: asi un mismo numero escrito de dos formas no crea dos clientes.
    public static string? NormalizarTelefono(string? telefono)
    {
        if (!Reglas.EsTelefono(telefono))
            return null;
        var limpio = telefono!.Replace(" ", "");
        return limpio.StartsWith("+56", StringComparison.Ordinal) ? limpio[3..] : limpio;
    }

    public static string? NormalizarRut(string? rut) =>
        string.IsNullOrWhiteSpace(rut) ? null : Rut.TryNormalizar(rut, out var normalizado) ? normalizado : null;
}
