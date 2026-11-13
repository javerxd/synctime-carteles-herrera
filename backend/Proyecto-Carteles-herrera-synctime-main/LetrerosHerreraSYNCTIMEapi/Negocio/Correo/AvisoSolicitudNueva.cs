using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Repositories;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// El aviso al taller por cada solicitud nueva (objetivo principal del sistema, 13-11-2026). Pensado para
// leerse en el telefono: quien pide, como contactarlo de un toque (llamar, WhatsApp, responder el correo)
// y que pidio. Si el cliente dejo su correo, "Responder" le escribe a el directamente.
// Lo que escribio el visitante se limpia de saltos de linea y caracteres invisibles: asi no puede
// fabricar lineas falsas en el correo (por ejemplo, otro "Teléfono:") ni invertir el texto.
public static class AvisoSolicitudNueva
{
    public static MensajeCorreo Componer(SolicitudParaAviso solicitud, IReadOnlyList<string> para, string? urlPublica)
    {
        var enLinea = solicitud.Origen != "presencial";
        var origen = enLinea ? "en línea" : "presencial";
        var fecha = FormatoChile.Fecha(solicitud.FechaSolicitud);
        var hora = FormatoChile.Hora(solicitud.FechaSolicitud);
        var nombre = Linea(solicitud.ClienteNombre);
        var telefonoEscrito = Linea(solicitud.ClienteTelefono);
        var telefono = TelefonoChile.Interpretar(telefonoEscrito);
        var correoCliente = Reglas.EsCorreo(solicitud.ClienteEmail) ? solicitud.ClienteEmail : null;
        var registradaPor = solicitud.RegistradaPor is null ? null : Linea(solicitud.RegistradaPor);

        var entrada = enLinea
            ? $"{nombre} envió una solicitud en línea el {fecha} a las {hora}."
            : $"{registradaPor ?? "El personal"} registró una solicitud presencial el {fecha} a las {hora}.";

        var cuerpo = new CuerpoCorreo().Parrafo(entrada);

        var datos = new List<DatoCorreo>
        {
            new("Solicitud", $"N.º {solicitud.IdSolicitud}, {origen}"),
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

        if (!string.IsNullOrWhiteSpace(solicitud.Descripcion))
            cuerpo.Cita("Descripción", Linea(solicitud.Descripcion));

        if (solicitud.Detalles.Count > 0)
            cuerpo.Lista("Lo que pidió", solicitud.Detalles.Select(Detalle));

        if (UrlPanel(urlPublica) is { } panel)
            cuerpo.Boton("Abrir el panel", panel);

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
            PlantillaCorreo.AsuntoSeguro($"Nueva solicitud N.º {solicitud.IdSolicitud} · {nombre}"),
            html,
            texto,
            ResponderA: correoCliente,
            Referencia: $"solicitud:{solicitud.IdSolicitud}");
    }

    // El enlace al panel solo si la direccion del sitio es una web valida; si esta mal escrita, el aviso
    // sale igual, sin el boton (es preferible a no avisar).
    internal static string? UrlPanel(string? urlPublica)
    {
        if (string.IsNullOrWhiteSpace(urlPublica))
            return null;
        return Uri.TryCreate($"{urlPublica.Trim().TrimEnd('/')}/panel", UriKind.Absolute, out var panel)
               && (panel.Scheme == Uri.UriSchemeHttps || panel.Scheme == Uri.UriSchemeHttp)
            ? panel.ToString()
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

    private static string Detalle(DetalleParaAviso detalle)
    {
        var partes = new List<string> { $"{detalle.Cantidad} × {Linea(detalle.Producto)}" };
        if (!string.IsNullOrWhiteSpace(detalle.Descripcion))
            partes.Add(Linea(detalle.Descripcion));
        if (detalle.AnchoMm is int ancho && detalle.AltoMm is int alto)
            partes.Add($"{FormatoChile.Centimetros(ancho)} × {FormatoChile.Centimetros(alto)} cm");
        return string.Join(", ", partes);
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
