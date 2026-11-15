using LetrerosHerreraSYNCTIMEapi.Datos.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;

// Comando de mantenimiento: envia ahora, por SMTP y sin pasar por la cola, un correo de prueba a los
// destinatarios de los avisos del taller, y dice si funciono. Sirve para comprobar la cuenta de Gmail
// recien configurada. Usa SMTP aunque Correo:Modo sea Bandeja: su objetivo es probar el envio real.
//   dotnet run --launch-profile https -- probar-correo
// Muestra cuantos destinatarios hubo, nunca las direcciones ni las credenciales.
public static class PruebaDeCorreo
{
    public const string Comando = "probar-correo";

    public static async Task<int> EjecutarAsync(IServiceProvider servicios)
    {
        await using var ambito = servicios.CreateAsyncScope();
        var destinatarios = await ambito.ServiceProvider.GetRequiredService<IDestinatariosAvisos>().ObtenerAsync(CancellationToken.None);
        if (destinatarios.Count == 0)
        {
            Console.Error.WriteLine($"No hay a quien enviarlo: escriba el correo de destino en User Secrets (clave {DestinatariosAvisosDesdeBaseYConfiguracion.Clave}) o agréguelo desde el perfil del panel.");
            return 2;
        }

        var smtp = servicios.GetRequiredService<EnviadorSmtp>();
        if (!smtp.Listo(out var motivo))
        {
            Console.Error.WriteLine($"No se puede enviar: {motivo}");
            return 2;
        }

        var opciones = servicios.GetRequiredService<IOptions<OpcionesCorreo>>().Value;
        var reloj = servicios.GetRequiredService<TimeProvider>();
        var mensaje = CorreoDePrueba.Componer(destinatarios, reloj.GetLocalNow().DateTime, opciones.Smtp.ToString());
        var correo = new CorreoSaliente
        {
            Tipo = mensaje.Tipo,
            Referencia = mensaje.Referencia,
            Destinatarios = string.Join(';', mensaje.Para),
            Asunto = mensaje.Asunto,
            CuerpoHtml = mensaje.Html,
            CuerpoTexto = mensaje.Texto,
            FechaCreacion = reloj.GetUtcNow()
        };

        try
        {
            await smtp.EnviarAsync(new CorreoParaEnviar(0, mensaje.Referencia, ConstructorMime.Construir(correo, opciones)), CancellationToken.None);
            Console.WriteLine($"Correo de prueba enviado a {destinatarios.Count} destinatario(s) por {smtp.Descripcion}. Revise la bandeja de entrada (y la carpeta de spam).");
            return 0;
        }
        catch (Exception error)
        {
            var fallo = ClasificadorFallos.Clasificar(error);
            Console.Error.WriteLine($"No se pudo enviar. {fallo.Texto}");
            if (fallo.Categoria == "autenticacion")
                Console.Error.WriteLine("Revise que Correo:Smtp:Password sea una contraseña de aplicación de Google (16 letras, sin espacios) y que la cuenta tenga activa la verificación en dos pasos.");
            return 1;
        }
    }
}
