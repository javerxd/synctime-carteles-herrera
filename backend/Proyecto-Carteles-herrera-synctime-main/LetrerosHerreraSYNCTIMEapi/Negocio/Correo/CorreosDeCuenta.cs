namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Correos de la cuenta (fase 6): confirmar el correo y restablecer la contraseña. Salen de la casilla
// automatica, con el pie de «no respondas» (CorreoParaCliente). Nunca llevan la contraseña ni datos del
// pedido; el codigo o el enlace solo sirven una vez y vencen pronto.
public static class CorreosDeCuenta
{
    public static MensajeCorreo Verificacion(string para, string nombre, string enlace, string correoContacto)
    {
        EnlaceSeguro.Validar(enlace);
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {nombre}. Para terminar de crear tu cuenta en Letreros Herrera, confirma que este correo es tuyo.")
            .Boton("Confirmar mi correo", enlace)
            .Nota("El enlace sirve una sola vez y vence en 24 horas. Si no creaste una cuenta, ignora este correo: sin la confirmación no se usa tu dirección.");
        return CorreoParaCliente.Componer(TiposCorreo.VerificacionCorreo, para, "Confirma tu correo", "Confirma tu correo",
            "Un paso más para usar tu cuenta", cuerpo, correoContacto);
    }

    // Al correo anterior, cuando la cuenta pasa a otro correo: si no fue el titular, se entera y puede avisar.
    public static MensajeCorreo AvisoCambioCorreo(string para, string nombre, string correoContacto)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {nombre}. El correo de tu cuenta en Letreros Herrera se cambió por otro. Desde ahora los avisos llegan a la nueva dirección.")
            .Nota($"Si no lo hiciste tú, escríbenos a {correoContacto} o pasa por el local para recuperar tu cuenta.");
        return CorreoParaCliente.Componer(TiposCorreo.AvisoCambioCorreo, para, "El correo de tu cuenta cambió", "El correo de tu cuenta cambió",
            "Si no fuiste tú, avísanos", cuerpo, correoContacto);
    }

    public static MensajeCorreo Recuperacion(string para, string nombre, string codigo, int minutos, string correoContacto)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {nombre}. Recibimos una solicitud para restablecer la contraseña de tu cuenta. Tu código es:")
            .Datos(new DatoCorreo("Código", codigo))
            .Nota($"Escríbelo en la página donde lo pediste. Vence en {minutos} minutos y sirve una sola vez. Si no lo pediste tú, ignora este correo: tu contraseña no cambia.");
        return CorreoParaCliente.Componer(TiposCorreo.RecuperacionCuenta, para, "Tu código para restablecer la contraseña",
            "Restablece tu contraseña", $"Tu código vence en {minutos} minutos", cuerpo, correoContacto);
    }
}
