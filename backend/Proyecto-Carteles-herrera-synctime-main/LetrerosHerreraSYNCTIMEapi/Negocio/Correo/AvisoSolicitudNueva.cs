using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// El aviso al taller por cada solicitud nueva (objetivo principal del sistema, 13-11-2026). Pensado para
// leerse en el telefono: quien pide, como contactarlo de un toque (llamar, WhatsApp, responder el correo)
// y que pidio. Si el cliente dejo su correo, "Responder" le escribe a el directamente.
// Lo que escribio el visitante se limpia de saltos de linea y caracteres invisibles: asi no puede
// fabricar lineas falsas en el correo (por ejemplo, otro "Teléfono:") ni invertir el texto.
public static class AvisoSolicitudNueva
{
    // Fase 7: el aviso se arma con el mismo comprobante que ve el cliente. registradaPor solo en las
    // presenciales (hoy no avisan al taller, pero el aviso sabe decirlo si se vuelve a activar).
    public static MensajeCorreo Componer(ComprobanteSolicitud solicitud, string? registradaPorNombre, IReadOnlyList<string> para, string? urlPublica)
    {
        var enLinea = solicitud.Origen != "presencial";
        var origen = enLinea ? "en línea" : "presencial";
        var fecha = FormatoChile.Fecha(solicitud.FechaChile);
        var hora = FormatoChile.Hora(solicitud.FechaChile);
        var nombre = Linea($"{solicitud.Cliente.Nombre} {solicitud.Cliente.Apellido}");
        var telefonoEscrito = Linea(solicitud.Cliente.Telefono);
        var telefono = TelefonoChile.Interpretar(telefonoEscrito);
        var correoCliente = Reglas.EsCorreo(solicitud.Cliente.Correo) ? solicitud.Cliente.Correo : null;
        var registradaPor = registradaPorNombre is null ? null : Linea(registradaPorNombre);

        var entrada = enLinea
            ? $"{nombre} envió una solicitud en línea el {fecha} a las {hora}."
            : $"{registradaPor ?? "El personal"} registró una solicitud presencial el {fecha} a las {hora}.";

        var cuerpo = new CuerpoCorreo().Parrafo(entrada);

        var datos = new List<DatoCorreo>
        {
            new("Solicitud", $"{solicitud.Numero}, {origen}"),
            new("Cliente", nombre),
            telefono is null
                ? new("Teléfono", telefonoEscrito)
                : new("Teléfono", telefono.Legible, telefono.EnlaceLlamada),
            correoCliente is null
                ? new("Correo", "Sin correo")
                : new("Correo", correoCliente, Mailto(correoCliente))
        };
        if (!enLinea && registradaPor is not null)
            datos.Add(new("Registrada por", registradaPor));
        cuerpo.Datos([.. datos]);

        // En el telefono, lo siguiente que hace el dueño es contactar al cliente: las acciones van justo
        // debajo de sus datos.
        var contacto = new List<EnlaceCorreo>();
        if (telefono is not null)
            contacto.Add(new("Llamar", telefono.EnlaceLlamada));
        if (telefono?.EsCelular == true)
            contacto.Add(new("WhatsApp", telefono.EnlaceWhatsApp));
        if (correoCliente is not null)
            contacto.Add(new("Escribirle", Mailto(correoCliente)));
        if (contacto.Count > 0)
            cuerpo.Enlaces([.. contacto]);

        if (!string.IsNullOrWhiteSpace(solicitud.DescripcionGeneral))
            cuerpo.Cita("Descripción", Linea(solicitud.DescripcionGeneral));

        if (solicitud.Detalles.Count > 0)
            cuerpo.Lista("Lo que pidió", solicitud.Detalles.Select(Detalle));

        // Los archivos no viajan en el correo: se ven en el panel, con sesion.
        cuerpo.Parrafo(Adjuntos(solicitud.Adjuntos.Count));

        if (UrlSolicitud(urlPublica, solicitud.IdSolicitud) is { } ficha)
            cuerpo.Boton("Ver solicitud", ficha);

        if (enLinea)
            cuerpo.Nota("Los datos de contacto los escribió quien envió la solicitud: confírmalos antes de compartir información.");

        var (html, texto) = PlantillaCorreo.Componer(
            titulo: $"Nueva solicitud {origen}",
            preencabezado: $"{nombre} · {telefono?.Legible ?? telefonoEscrito}",
            cuerpo,
            pie: "Recibes este aviso porque tu correo está configurado para los avisos del taller en SyncTime.");

        return new MensajeCorreo(
            TiposCorreo.AvisoSolicitudNueva,
            para,
            PlantillaCorreo.AsuntoSeguro($"Nueva solicitud {solicitud.Numero} - {nombre}"),
            html,
            texto,
            ResponderA: correoCliente,
            Referencia: $"solicitud:{solicitud.IdSolicitud}");
    }

    internal static string Adjuntos(int cantidad) => cantidad switch
    {
        0 => "Sin archivos adjuntos.",
        1 => "1 archivo adjunto (se ve en el panel).",
        _ => $"{cantidad} archivos adjuntos (se ven en el panel)."
    };

    // El enlace al panel solo si la direccion del sitio es una web valida; si esta mal escrita, el aviso
    // sale igual, sin el boton (es preferible a no avisar).
    internal static string? UrlPanel(string? urlPublica) => UrlDelSitio(urlPublica, "/panel");

    // La ficha de la solicitud en el panel: si no hay sesion, el panel pide ingresar y despues vuelve a ella.
    internal static string? UrlSolicitud(string? urlPublica, int idSolicitud) => UrlDelSitio(urlPublica, $"/panel/solicitudes/{idSolicitud}");

    private static string? UrlDelSitio(string? urlPublica, string ruta)
    {
        if (string.IsNullOrWhiteSpace(urlPublica))
            return null;
        return Uri.TryCreate($"{urlPublica.Trim().TrimEnd('/')}{ruta}", UriKind.Absolute, out var url)
               && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url.ToString()
            : null;
    }

    // Una direccion con "?" o "&" en su nombre podria agregar destinatarios o un asunto al hacer clic: se
    // escapa la parte antes de la arroba (el dominio ya solo admite letras, numeros, puntos y guiones).
    internal static string Mailto(string correo)
    {
        var arroba = correo.LastIndexOf('@');
        return $"mailto:{Uri.EscapeDataString(correo[..arroba])}@{correo[(arroba + 1)..]}";
    }

    // Una sola linea, sin caracteres de control ni de formato invisibles (como los que invierten el texto).
    internal static string Linea(string texto)
    {
        var limpio = new string(texto.Select(c =>
            char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format ? ' ' : c).ToArray());
        return string.Join(' ', limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // «2 × Letrero luminoso, con logo, 100 × 200 cm, 4 m²»
    internal static string Detalle(DetalleComprobante detalle)
    {
        var partes = new List<string> { $"{detalle.Cantidad} × {Linea(detalle.Producto)}" };
        if (!string.IsNullOrWhiteSpace(detalle.Descripcion))
            partes.Add(Linea(detalle.Descripcion));
        if (detalle.AnchoMm is int ancho && detalle.AltoMm is int alto)
            partes.Add($"{FormatoChile.Centimetros(ancho)} × {FormatoChile.Centimetros(alto)} cm");
        if (detalle.AreaM2 is decimal area)
            partes.Add(MedidasSolicitud.Area(area));
        return string.Join(", ", partes);
    }
}

// Fase 7: confirmacion al cliente con su comprobante. Sale desde la casilla automatica, con el pie de
// «no respondas»; «Responder» va al correo de contacto del taller.
public static class ConfirmacionSolicitud
{
    public static MensajeCorreo Componer(ComprobanteSolicitud solicitud, string para, string correoContacto, string? urlPublica)
    {
        var nombre = AvisoSolicitudNueva.Linea(solicitud.Cliente.Nombre);
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {nombre}. Recibimos tu solicitud {solicitud.Numero} el {FormatoChile.Fecha(solicitud.FechaChile)} a las {FormatoChile.Hora(solicitud.FechaChile)}. La revisaremos y te contactaremos con la cotización.")
            .Datos(
                new DatoCorreo("Solicitud", solicitud.Numero),
                new DatoCorreo("Estado", Estado(solicitud.Estado)),
                new DatoCorreo("Cliente", AvisoSolicitudNueva.Linea($"{solicitud.Cliente.Nombre} {solicitud.Cliente.Apellido}")),
                new DatoCorreo("Teléfono", TelefonoChile.Interpretar(solicitud.Cliente.Telefono)?.Legible ?? AvisoSolicitudNueva.Linea(solicitud.Cliente.Telefono)));

        if (solicitud.Detalles.Count > 0)
            cuerpo.Lista("Lo que pediste", solicitud.Detalles.Select(AvisoSolicitudNueva.Detalle));
        if (solicitud.AreaTotalM2 is decimal total)
            cuerpo.Parrafo($"Área total: {MedidasSolicitud.Area(total)}.");
        if (!string.IsNullOrWhiteSpace(solicitud.DescripcionGeneral))
            cuerpo.Cita("Descripción", AvisoSolicitudNueva.Linea(solicitud.DescripcionGeneral));
        if (solicitud.Adjuntos.Count > 0)
            cuerpo.Lista("Archivos adjuntos", solicitud.Adjuntos.Select(a => AvisoSolicitudNueva.Linea(a.Nombre)));
        if (UrlMisSolicitudes(urlPublica) is { } url)
            cuerpo.Boton("Ver mis solicitudes", url);

        return CorreoParaCliente.Componer(
            TiposCorreo.ConfirmacionSolicitud,
            para,
            $"Recibimos tu solicitud {solicitud.Numero}",
            "Recibimos tu solicitud",
            $"Solicitud {solicitud.Numero}: la revisaremos y te contactaremos.",
            cuerpo,
            correoContacto,
            referencia: $"confirmacion:{solicitud.IdSolicitud}");
    }

    private static string Estado(string estado) => estado switch
    {
        "solicitado" => "En revisión",
        "cotizado" => "Cotizada",
        "aceptado" => "Aceptada",
        "rechazado" => "Rechazada",
        _ => estado
    };

    private static string? UrlMisSolicitudes(string? urlPublica)
    {
        if (string.IsNullOrWhiteSpace(urlPublica))
            return null;
        return Uri.TryCreate($"{urlPublica.Trim().TrimEnd('/')}/cuenta/solicitudes", UriKind.Absolute, out var url)
               && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url.ToString()
            : null;
    }
}

// Un solo aviso cuando llegan mas solicitudes de las normales en una hora (posible envio masivo): las
// solicitudes se guardan todas, pero no se avisa de cada una hasta que baje el ritmo.
public static class AvisoTopeSolicitudes
{
    public static MensajeCorreo Componer(IReadOnlyList<string> para, int tope, string? urlPublica)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"En la última hora llegaron {tope} solicitudes o más. Para no llenar tu correo, mientras siga ese ritmo no te avisaremos de cada una por separado. Todas quedaron guardadas y las ves en el panel.")
            .Parrafo("Si no las reconoces, puede ser un envío automático (spam). En ese caso, avísale a quien administra SyncTime.");
        if (AvisoSolicitudNueva.UrlPanel(urlPublica) is { } panel)
            cuerpo.Boton("Abrir el panel", panel);

        var (html, texto) = PlantillaCorreo.Componer(
            "Llegaron muchas solicitudes",
            $"{tope} o más solicitudes en la última hora.",
            cuerpo,
            "Recibes este aviso porque tu correo está configurado para los avisos del taller en SyncTime.");

        return new MensajeCorreo(TiposCorreo.AvisoTopeSolicitudes, para, "Llegaron muchas solicitudes en la última hora", html, texto, Referencia: "tope");
    }
}

// Correo para comprobar la cuenta SMTP (comando probar-correo).
public static class CorreoDePrueba
{
    public static MensajeCorreo Componer(IReadOnlyList<string> para, DateTime ahoraLocal, string servidor)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo("Si estás leyendo esto, la cuenta de correo del taller quedó bien configurada: los avisos de solicitudes nuevas llegarán a esta casilla.")
            .Datos(
                new DatoCorreo("Enviado", $"{FormatoChile.Fecha(ahoraLocal)} a las {FormatoChile.Hora(ahoraLocal)}"),
                new DatoCorreo("Servidor", servidor));

        var (html, texto) = PlantillaCorreo.Componer(
            "Correo de prueba de SyncTime",
            "La cuenta de correo del taller funciona.",
            cuerpo,
            "Este correo lo pidió quien administra SyncTime. No necesitas responderlo.");

        return new MensajeCorreo(TiposCorreo.Prueba, para, "Correo de prueba de SyncTime", html, texto, Referencia: "prueba");
    }
}

// Una consulta del formulario de contacto de la portada (RFS08, S09): llega al taller con el mensaje
// citado tal cual y «Responder» le escribe directo a quien consulto.
public sealed record ConsultaParaAviso(int IdConsulta, DateTime FechaChile, string Nombre, string Correo, string? Telefono, string Asunto, string Mensaje);

public static class AvisoConsultaContacto
{
    public static MensajeCorreo Componer(ConsultaParaAviso consulta, IReadOnlyList<string> para, string? urlPublica)
    {
        var nombre = AvisoSolicitudNueva.Linea(consulta.Nombre);
        var asunto = AvisoSolicitudNueva.Linea(consulta.Asunto);
        var telefonoEscrito = consulta.Telefono is null ? null : AvisoSolicitudNueva.Linea(consulta.Telefono);
        var telefono = TelefonoChile.Interpretar(telefonoEscrito);
        var correo = Reglas.EsCorreo(consulta.Correo) ? consulta.Correo : null;

        var cuerpo = new CuerpoCorreo()
            .Parrafo($"{nombre} escribió desde el formulario de contacto el {FormatoChile.Fecha(consulta.FechaChile)} a las {FormatoChile.Hora(consulta.FechaChile)}.");

        var datos = new List<DatoCorreo> { new("Asunto", asunto), new("Nombre", nombre) };
        datos.Add(correo is null ? new("Correo", "Sin correo válido") : new("Correo", correo, AvisoSolicitudNueva.Mailto(correo)));
        if (telefono is not null)
            datos.Add(new("Teléfono", telefono.Legible, telefono.EnlaceLlamada));
        else if (!string.IsNullOrWhiteSpace(telefonoEscrito))
            datos.Add(new("Teléfono", telefonoEscrito));
        cuerpo.Datos([.. datos]);

        var contacto = new List<EnlaceCorreo>();
        if (correo is not null)
            contacto.Add(new("Responder", AvisoSolicitudNueva.Mailto(correo)));
        if (telefono is not null)
            contacto.Add(new("Llamar", telefono.EnlaceLlamada));
        if (telefono?.EsCelular == true)
            contacto.Add(new("WhatsApp", telefono.EnlaceWhatsApp));
        if (contacto.Count > 0)
            cuerpo.Enlaces([.. contacto]);

        cuerpo.Cita("Mensaje", AvisoSolicitudNueva.Linea(consulta.Mensaje));
        if (AvisoSolicitudNueva.UrlPanel(urlPublica) is { } panel)
            cuerpo.Boton("Abrir el panel", panel);
        cuerpo.Nota("Los datos los escribió quien envió la consulta: confírmalos antes de compartir información.");

        var (html, texto) = PlantillaCorreo.Componer(
            titulo: "Nueva consulta desde la página",
            preencabezado: $"{nombre}: {asunto}",
            cuerpo,
            pie: "Recibes este aviso porque tu correo está configurado para los avisos del taller en SyncTime.");

        return new MensajeCorreo(
            TiposCorreo.ConsultaContacto,
            para,
            PlantillaCorreo.AsuntoSeguro($"Consulta: {asunto} · {nombre}"),
            html,
            texto,
            ResponderA: correo,
            Referencia: $"contacto:{consulta.IdConsulta}");
    }
}
