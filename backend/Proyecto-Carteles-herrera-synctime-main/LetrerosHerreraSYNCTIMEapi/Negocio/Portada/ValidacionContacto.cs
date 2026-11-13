namespace LetrerosHerreraSYNCTIMEapi.Negocio.Portada;

// Datos del formulario de contacto de la portada (E22).
public sealed record ConsultaContactoEntrada(
    string? Nombre,
    string? Correo,
    string? Telefono,
    string? Asunto,
    string? Mensaje,
    bool AceptaPrivacidad);

// Validacion del formulario de contacto, campo por campo y en español (RNFO07). Son los mismos limites
// que muestra el formulario, asi el visitante ve el mismo mensaje en su pantalla y en la respuesta.
public static class ValidacionContacto
{
    public const int LargoNombre = 100;
    public const int LargoAsunto = 150;
    public const int MinimoMensaje = 10;
    public const int LargoMensaje = 2000;

    public static IReadOnlyDictionary<string, string[]> Validar(ConsultaContactoEntrada consulta)
    {
        var errores = new Dictionary<string, string[]>();
        void Error(string campo, string mensaje) => errores[campo] = [mensaje];

        var nombre = consulta.Nombre?.Trim() ?? "";
        if (nombre.Length == 0)
            Error("nombre", "Escribe tu nombre.");
        else if (nombre.Length > LargoNombre)
            Error("nombre", $"El nombre puede tener hasta {LargoNombre} caracteres.");

        if (!Reglas.EsCorreo(consulta.Correo?.Trim()))
            Error("correo", "Escribe un correo válido, por ejemplo nombre@correo.cl, para poder responderte.");

        if (!string.IsNullOrWhiteSpace(consulta.Telefono) && !Reglas.EsTelefono(consulta.Telefono))
            Error("telefono", "El teléfono debe tener 9 dígitos, por ejemplo 9 1234 5678. Es opcional.");

        var asunto = consulta.Asunto?.Trim() ?? "";
        if (asunto.Length == 0)
            Error("asunto", "Escribe el asunto de tu consulta.");
        else if (asunto.Length > LargoAsunto)
            Error("asunto", $"El asunto puede tener hasta {LargoAsunto} caracteres.");

        var mensaje = consulta.Mensaje?.Trim() ?? "";
        if (mensaje.Length < MinimoMensaje)
            Error("mensaje", $"Cuéntanos un poco más: el mensaje debe tener al menos {MinimoMensaje} caracteres.");
        else if (mensaje.Length > LargoMensaje)
            Error("mensaje", $"El mensaje puede tener hasta {LargoMensaje} caracteres.");

        if (!consulta.AceptaPrivacidad)
            Error("aceptaPrivacidad", "Para enviar la consulta debes aceptar la política de privacidad.");

        return errores;
    }
}
