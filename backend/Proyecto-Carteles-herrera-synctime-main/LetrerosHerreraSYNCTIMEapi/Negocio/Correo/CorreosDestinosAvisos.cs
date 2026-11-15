namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Fase 9: los correos de los destinos de los avisos del taller (pedido del usuario del 14-11-2026). Salen
// desde la casilla automatica, con el pie de «no respondas» (CorreoParaCliente). Ninguno muestra completa
// otra direccion que no sea la del propio destinatario: las demas van enmascaradas.
public static class CorreosDestinosAvisos
{
    public const int HorasVigenciaEnlace = 24;

    // «contacto@gmail.com» -> «c•••o@gmail.com». El dominio va completo, para reconocer el proveedor.
    public static string Enmascarar(string correo)
    {
        var arroba = correo.LastIndexOf('@');
        if (arroba <= 0)
            return "•••";
        var local = correo[..arroba];
        var dominio = correo[(arroba + 1)..];
        var visible = local.Length <= 2 ? $"{local[0]}•••" : $"{local[0]}•••{local[^1]}";
        return $"{visible}@{dominio}";
    }

    // A la direccion nueva: hasta que alguien abra el enlace, no recibe ningun aviso.
    public static MensajeCorreo Confirmar(string para, string pidioNombre, string enlace, string correoContacto)
    {
        EnlaceSeguro.Validar(enlace);
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"{AvisoSolicitudNueva.Linea(pidioNombre)} pidió que este correo reciba los avisos de solicitudes nuevas de Letreros Herrera. Para empezar a recibirlos, confirma que la dirección es correcta.")
            .Boton("Confirmar este correo", enlace)
            .Nota($"El enlace sirve una sola vez y vence en {HorasVigenciaEnlace} horas. Si no esperabas este correo, ignóralo: sin la confirmación, esta dirección no recibe nada.");
        return CorreoParaCliente.Componer(TiposCorreo.ConfirmacionDestinoAvisos, para, "Confirma el destino de los avisos de Letreros Herrera",
            "Confirma el destino de los avisos", "Un paso más para recibir los avisos del taller", cuerpo, correoContacto, referencia: "destino_avisos");
    }

    // A la direccion reemplazada, cuando la nueva se confirma.
    public static MensajeCorreo Cambio(string para, string nuevaEnmascarada, string pidioNombre, DateTime fechaChile, string correoContacto)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Desde el {FormatoChile.Fecha(fechaChile)} a las {FormatoChile.Hora(fechaChile)}, los avisos de solicitudes nuevas del taller llegan a {nuevaEnmascarada} en vez de a este correo.")
            .Datos(
                new DatoCorreo("Nuevo destino", nuevaEnmascarada),
                new DatoCorreo("Lo pidió", AvisoSolicitudNueva.Linea(pidioNombre)))
            .Nota("Si no reconoces este cambio, avisa de inmediato a quien administra SyncTime.");
        return CorreoParaCliente.Componer(TiposCorreo.DestinoAvisosCambiado, para, "El destino de los avisos de Letreros Herrera cambió",
            "El destino de los avisos cambió", $"Ahora llegan a {nuevaEnmascarada}", cuerpo, correoContacto, referencia: "destino_avisos");
    }

    // A cada destino activo, cuando se confirma uno agregado (decision 4 del 14-11-2026).
    public static MensajeCorreo Agregado(string para, string nuevaEnmascarada, string pidioNombre, DateTime fechaChile, string correoContacto)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Desde el {FormatoChile.Fecha(fechaChile)} a las {FormatoChile.Hora(fechaChile)}, {nuevaEnmascarada} también recibe los avisos de solicitudes nuevas del taller. Este correo los sigue recibiendo.")
            .Datos(
                new DatoCorreo("Destino agregado", nuevaEnmascarada),
                new DatoCorreo("Lo pidió", AvisoSolicitudNueva.Linea(pidioNombre)))
            .Nota("Si no reconoces este cambio, avisa de inmediato a quien administra SyncTime.");
        return CorreoParaCliente.Componer(TiposCorreo.DestinoAvisosAgregado, para, "Se agregó un destino a los avisos del taller",
            "Se agregó un destino a los avisos", $"{nuevaEnmascarada} también recibe los avisos", cuerpo, correoContacto, referencia: "destino_avisos");
    }

    // A la direccion quitada.
    public static MensajeCorreo Quitado(string para, string quitoNombre, DateTime fechaChile, string correoContacto)
    {
        var cuerpo = new CuerpoCorreo()
            .Parrafo($"Desde el {FormatoChile.Fecha(fechaChile)} a las {FormatoChile.Hora(fechaChile)}, este correo ya no recibe los avisos de solicitudes nuevas de Letreros Herrera.")
            .Datos(new DatoCorreo("Lo quitó", AvisoSolicitudNueva.Linea(quitoNombre)))
            .Nota("Si no reconoces este cambio, avisa de inmediato a quien administra SyncTime.");
        return CorreoParaCliente.Componer(TiposCorreo.DestinoAvisosQuitado, para, "Este correo ya no recibe los avisos del taller",
            "Ya no recibes los avisos del taller", "Este correo dejó de recibir los avisos", cuerpo, correoContacto, referencia: "destino_avisos");
    }
}
