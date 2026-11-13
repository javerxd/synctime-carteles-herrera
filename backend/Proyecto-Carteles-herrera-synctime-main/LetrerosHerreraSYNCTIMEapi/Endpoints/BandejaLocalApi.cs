using System.Text;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using MimeKit;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Bandeja local: muestra los correos que la API "envio" en modo Bandeja. SOLO existe en desarrollo
// (Program la registra con IsDevelopment) y no forma parte del contrato de la API.
//   /dev/bandeja                    lista de correos
//   /dev/bandeja/{archivo}          el correo en HTML, aislado (sin scripts ni formularios)
//   /dev/bandeja/{archivo}/texto    su version de texto
//   /dev/bandeja/{archivo}/eml      el archivo original, para abrirlo en un programa de correo
public static class BandejaLocalApi
{
    private const string PoliticaVista = "default-src 'none'; img-src data:; style-src 'unsafe-inline'; sandbox";

    public static void MapBandejaLocal(this WebApplication app)
    {
        // Solo desde este mismo equipo: si por error la API corriera en desarrollo con acceso desde la red,
        // los correos (con datos de clientes) no quedarian expuestos.
        var grupo = app.MapGroup("/dev/bandeja").ExcludeFromDescription().AllowAnonymous()
            .AddEndpointFilter(async (contexto, siguiente) =>
            {
                var ip = contexto.HttpContext.Connection.RemoteIpAddress;
                return ip is null || System.Net.IPAddress.IsLoopback(ip) ? await siguiente(contexto) : Results.NotFound();
            });

        grupo.MapGet("/", async (EnviadorBandejaLocal bandeja, CancellationToken ct) =>
        {
            var archivos = Directory.Exists(bandeja.Carpeta)
                ? new DirectoryInfo(bandeja.Carpeta).GetFiles("*.eml").Where(a => EnviadorBandejaLocal.NombreValido().IsMatch(a.Name))
                    .OrderByDescending(a => a.Name).Take(100).ToList()
                : [];

            var filas = new StringBuilder();
            foreach (var archivo in archivos)
            {
                var mensaje = await MimeMessage.LoadAsync(archivo.FullName, ct);
                var nombre = TextoHtml.Escapar(archivo.Name);
                filas.Append($"""
                    <tr><td>{archivo.LastWriteTime:dd-MM-yyyy HH:mm:ss}</td>
                    <td>{TextoHtml.Escapar(string.Join(", ", mensaje.To.Mailboxes.Select(m => m.Address)))}</td>
                    <td><a href="/dev/bandeja/{nombre}">{TextoHtml.Escapar(mensaje.Subject ?? "(sin asunto)")}</a></td>
                    <td><a href="/dev/bandeja/{nombre}/texto">texto</a> · <a href="/dev/bandeja/{nombre}/eml">.eml</a></td></tr>
                    """);
            }

            var html = $$"""
                <!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><title>Bandeja local · SyncTime</title>
                <style>body{font-family:system-ui,sans-serif;margin:2rem;color:#0F172A}table{border-collapse:collapse;width:100%}
                td,th{text-align:left;padding:.5rem;border-bottom:1px solid #E2E8F0;font-size:14px}a{color:#B45309}</style></head>
                <body><h1>Bandeja local</h1><p>Correos guardados en la carpeta de la bandeja local (solo desarrollo; no salió ninguno del equipo).</p>
                <table><thead><tr><th>Fecha</th><th>Para</th><th>Asunto</th><th></th></tr></thead><tbody>{{filas}}</tbody></table></body></html>
                """;
            return Results.Content(html, "text/html; charset=utf-8");
        });

        grupo.MapGet("/{archivo}", async (string archivo, EnviadorBandejaLocal bandeja, HttpContext http, CancellationToken ct) =>
        {
            if (await CargarAsync(archivo, bandeja, ct) is not { } mensaje)
                return Results.NotFound();

            // Las imagenes incrustadas (cid:) no existen para el navegador: se cambian por su contenido.
            var html = mensaje.HtmlBody ?? "<p>(sin versión HTML)</p>";
            foreach (var parte in mensaje.BodyParts.OfType<MimePart>().Where(p => p.ContentId is not null && p.Content is not null))
            {
                using var copia = new MemoryStream();
                await parte.Content!.DecodeToAsync(copia, ct);
                html = html.Replace($"cid:{parte.ContentId}", $"data:{parte.ContentType.MimeType};base64,{Convert.ToBase64String(copia.ToArray())}");
            }

            http.Response.Headers.ContentSecurityPolicy = PoliticaVista;
            return Results.Content(html, "text/html; charset=utf-8");
        });

        grupo.MapGet("/{archivo}/texto", async (string archivo, EnviadorBandejaLocal bandeja, CancellationToken ct) =>
            await CargarAsync(archivo, bandeja, ct) is { } mensaje
                ? Results.Text(mensaje.TextBody ?? "(sin versión de texto)", "text/plain; charset=utf-8")
                : Results.NotFound());

        grupo.MapGet("/{archivo}/eml", (string archivo, EnviadorBandejaLocal bandeja) =>
            Ruta(archivo, bandeja) is { } ruta
                ? Results.File(ruta, "message/rfc822", archivo)
                : Results.NotFound());
    }

    // Solo nombres con el formato que escribe la bandeja: nada fuera de su carpeta.
    private static string? Ruta(string archivo, EnviadorBandejaLocal bandeja)
    {
        if (!EnviadorBandejaLocal.NombreValido().IsMatch(archivo))
            return null;
        var ruta = Path.Combine(bandeja.Carpeta, archivo);
        return File.Exists(ruta) ? ruta : null;
    }

    private static async Task<MimeMessage?> CargarAsync(string archivo, EnviadorBandejaLocal bandeja, CancellationToken ct) =>
        Ruta(archivo, bandeja) is { } ruta ? await MimeMessage.LoadAsync(ruta, ct) : null;
}
