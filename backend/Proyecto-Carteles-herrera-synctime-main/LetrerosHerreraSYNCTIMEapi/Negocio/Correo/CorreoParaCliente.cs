namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Correos que recibe un cliente o un visitante (las confirmaciones de las fases siguientes). Salen desde
// la casilla automatica de avisos, que nadie revisa: el pie lo dice y «Responder» va al correo publico de
// contacto del taller (Correo:ContactoTaller). Los avisos internos al taller no usan esto: alli
// «Responder» le escribe al cliente que hizo la solicitud o la consulta.
public static class CorreoParaCliente
{
    public static string Pie(string correoContacto) =>
        $"Este correo se envió desde una casilla automática que no se revisa: no respondas a este mensaje. Para cualquier consulta, escríbenos a {correoContacto}.";

    public static MensajeCorreo Componer(
        string tipo, string para, string asunto, string titulo, string preencabezado, CuerpoCorreo cuerpo,
        string correoContacto, string? referencia = null)
    {
        if (!Reglas.EsCorreo(para))
            throw new ArgumentException("El destinatario no es un correo válido.", nameof(para));
        if (!Reglas.EsCorreo(correoContacto))
            throw new ArgumentException("Falta el correo de contacto del taller (Correo:ContactoTaller).", nameof(correoContacto));

        var (html, texto) = PlantillaCorreo.Componer(titulo, preencabezado, cuerpo, Pie(correoContacto));
        return new MensajeCorreo(tipo, [para], PlantillaCorreo.AsuntoSeguro(asunto), html, texto,
            ResponderA: correoContacto, Referencia: referencia);
    }
}
