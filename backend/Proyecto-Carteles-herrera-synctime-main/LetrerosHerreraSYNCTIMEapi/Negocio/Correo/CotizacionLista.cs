using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Fase 8 (decision 3 del 14-11-2026): «Tu cotizacion #N esta lista». Sale desde la casilla automatica con el
// pie de «no respondas», y el boton lleva a la solicitud en la cuenta del cliente, donde la aprueba o la
// rechaza. Todo texto se escapa al componer (PlantillaCorreo), tambien las observaciones del taller.
public static class CotizacionLista
{
    public static MensajeCorreo Componer(FichaCotizacion cotizacion, string para, string correoContacto, string? urlPublica)
    {
        var nombre = AvisoSolicitudNueva.Linea(cotizacion.Cliente.Nombre);
        var total = FormatoChile.Pesos(cotizacion.TotalRedondeado);
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Hola, {nombre}. Tu cotización {cotizacion.Numero} para la solicitud {cotizacion.NumeroSolicitud} está lista.")
            .Datos(
                new DatoCorreo("Cotización", cotizacion.Numero),
                new DatoCorreo("Solicitud", cotizacion.NumeroSolicitud),
                new DatoCorreo("Total", $"{total} (IVA incluido)"));

        if (cotizacion.Items.Count > 0)
            cuerpo.Lista("Lo que cotizamos", cotizacion.Items.Select(Item));
        if (!string.IsNullOrWhiteSpace(cotizacion.Observaciones))
            cuerpo.Cita("Observaciones", AvisoSolicitudNueva.Linea(cotizacion.Observaciones));
        cuerpo.Parrafo("Puedes aprobarla o rechazarla desde tu cuenta. Si tienes dudas, escríbenos antes de decidir.");
        if (UrlSolicitud(urlPublica, cotizacion.IdSolicitud) is { } url)
            cuerpo.Boton("Ver cotización", url);

        return CorreoParaCliente.Componer(
            TiposCorreo.CotizacionLista,
            para,
            $"Tu cotización {cotizacion.Numero} está lista",
            "Tu cotización está lista",
            $"Cotización {cotizacion.Numero} por {total}: apruébala o recházala desde tu cuenta.",
            cuerpo,
            correoContacto,
            referencia: $"cotizacion:{cotizacion.IdCotizacion}");
    }

    // «2 × Letrero luminoso, con logo, 100 × 200 cm, 4 m²», igual que en la confirmacion de la solicitud.
    private static string Item(ItemCotizacion item) =>
        AvisoSolicitudNueva.Detalle(new DetalleComprobante(0, item.Producto, item.Descripcion, item.AnchoMm, item.AltoMm, item.Cantidad, item.AreaM2));

    // La solicitud en la cuenta del cliente; si no hay sesion, la pagina pide ingresar y vuelve a ella.
    private static string? UrlSolicitud(string? urlPublica, int idSolicitud)
    {
        if (string.IsNullOrWhiteSpace(urlPublica))
            return null;
        return Uri.TryCreate($"{urlPublica.Trim().TrimEnd('/')}/cuenta/solicitudes/{idSolicitud}", UriKind.Absolute, out var url)
               && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url.ToString()
            : null;
    }
}
