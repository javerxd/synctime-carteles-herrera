namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Correos que recibe un cliente o un visitante (las confirmaciones de las fases siguientes). Salen desde
// la casilla automatica de avisos, que nadie revisa: el pie lo dice y «Responder» va al correo publico de
// contacto del taller (Correo:ContactoTaller). Los avisos internos al taller no usan esto: alli
// «Responder» le escribe al cliente que hizo la solicitud o la consulta.
public static class CorreoParaCliente
{
    public static string Pie(string correoContacto) =>
        $"Este correo se envió desde una casilla automática que no se revisa: no respondas a este mensaje. Para cualquier consulta, escríbenos a {correoContacto}.";

    // Fase 11: un correo opcional (aviso de estado) recibe sus enlaces de baja: el de la pagina va en el cuerpo y
    // el de la baja con un clic, en la cabecera List-Unsubscribe.
    public static MensajeCorreo Componer(
        string tipo, string para, string asunto, string titulo, string preencabezado, CuerpoCorreo cuerpo,
        string correoContacto, string? referencia = null, EnlacesBaja? baja = null)
    {
        if (!Reglas.EsCorreo(para))
            throw new ArgumentException("El destinatario no es un correo válido.", nameof(para));
        if (!Reglas.EsCorreo(correoContacto))
            throw new ArgumentException("Falta el correo de contacto del taller (Correo:ContactoTaller).", nameof(correoContacto));

        if (baja is not null)
            cuerpo.Nota("Recibes este aviso porque tienes una cuenta en Letreros Herrera. Si no quieres recibir más avisos de estado, date de baja aquí:")
                .Enlaces(new EnlaceCorreo("Dejar de recibir estos avisos", baja.Pagina));
        var (html, texto) = PlantillaCorreo.Componer(titulo, preencabezado, cuerpo, Pie(correoContacto));
        return new MensajeCorreo(tipo, [para], PlantillaCorreo.AsuntoSeguro(asunto), html, texto,
            ResponderA: correoContacto, Referencia: referencia, EnlaceBaja: baja?.UnClic);
    }
}
