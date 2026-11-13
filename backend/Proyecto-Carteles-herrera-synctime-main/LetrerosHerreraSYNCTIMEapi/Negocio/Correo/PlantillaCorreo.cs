using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Contenido de un correo, armado por bloques. Todo texto se escapa al componer: quien arma un correo
// nunca escribe HTML, asi que un nombre o una descripcion con "<script>" llegan como texto.
public sealed class CuerpoCorreo
{
    internal abstract record Bloque;
    internal sealed record Parrafo_(string Texto) : Bloque;
    internal sealed record Datos_(IReadOnlyList<DatoCorreo> Filas) : Bloque;
    internal sealed record Lista_(string Titulo, IReadOnlyList<string> Elementos) : Bloque;
    internal sealed record Boton_(string Texto, string Url) : Bloque;
    internal sealed record Enlaces_(IReadOnlyList<EnlaceCorreo> Enlaces) : Bloque;
    internal sealed record Cita_(string Titulo, string Texto) : Bloque;
    internal sealed record Nota_(string Texto) : Bloque;

    internal List<Bloque> Bloques { get; } = [];

    public CuerpoCorreo Parrafo(string texto)
    {
        Bloques.Add(new Parrafo_(texto));
        return this;
    }

    // Pares etiqueta/valor, uno debajo del otro (se leen bien en un telefono).
    public CuerpoCorreo Datos(params DatoCorreo[] filas)
    {
        foreach (var fila in filas.Where(f => f.Enlace is not null))
            EnlaceSeguro.Validar(fila.Enlace!);
        Bloques.Add(new Datos_(filas));
        return this;
    }

    public CuerpoCorreo Lista(string titulo, IEnumerable<string> elementos)
    {
        Bloques.Add(new Lista_(titulo, elementos.ToList()));
        return this;
    }

    // La accion principal del correo: la unica en ambar.
    public CuerpoCorreo Boton(string texto, string url)
    {
        EnlaceSeguro.Validar(url);
        Bloques.Add(new Boton_(texto, url));
        return this;
    }

    public CuerpoCorreo Enlaces(params EnlaceCorreo[] enlaces)
    {
        foreach (var enlace in enlaces)
            EnlaceSeguro.Validar(enlace.Url);
        Bloques.Add(new Enlaces_(enlaces));
        return this;
    }

    // Texto que escribio otra persona (por ejemplo, la descripcion del cliente), citado tal cual.
    public CuerpoCorreo Cita(string titulo, string texto)
    {
        Bloques.Add(new Cita_(titulo, texto));
        return this;
    }

    // Aclaracion secundaria, en letra mas chica.
    public CuerpoCorreo Nota(string texto)
    {
        Bloques.Add(new Nota_(texto));
        return this;
    }
}

public sealed record DatoCorreo(string Etiqueta, string Valor, string? Enlace = null);
public sealed record EnlaceCorreo(string Texto, string Url);

internal static class EnlaceSeguro
{
    private static readonly string[] Esquemas = ["https", "http", "tel", "mailto"];

    // Solo enlaces web, de llamada o de correo: nunca javascript:, data: ni otros esquemas.
    public static void Validar(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !Esquemas.Contains(uri.Scheme))
            throw new ArgumentException("El enlace del correo debe ser https, http, tel o mailto.", nameof(url));
    }
}

// Plantilla base de todos los correos de SyncTime: modo claro, una columna de hasta 600 px que se adapta
// al telefono, tinta #0F172A, ambar #B45309 solo en la accion, y Plus Jakarta Sans con letras del
// sistema como respaldo (los programas de correo casi nunca cargan fuentes externas, y no se pide nada
// a terceros). El logotipo viaja dentro del mensaje (cid:), asi que se ve sin descargar imagenes.
// Las tablas son a proposito: es lo unico que todos los programas de correo dibujan igual.
public static class PlantillaCorreo
{
    public const string IdLogo = "logo-lh";
    public const string Marca = "Letrería Los Herrera";

    private const string Tinta = "#0F172A";
    private const string TextoSecundario = "#334155";
    private const string TextoTenue = "#64748B";
    private const string Ambar = "#B45309";
    private const string Fondo = "#F1F5F9";
    private const string Borde = "#E2E8F0";
    // El mismo borde de los controles del sistema de diseño (contraste 3:1 sobre blanco, WCAG 1.4.11).
    private const string BordeControl = "#828DA0";
    private const string Fuente = "'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    private static string E(string texto) => TextoHtml.Escapar(texto);

    // Un asunto es una cabecera: sin saltos de linea ni caracteres de control, y de largo razonable.
    public static string AsuntoSeguro(string asunto)
    {
        var limpio = new string(asunto.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        limpio = string.Join(' ', limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return limpio.Length <= 150 ? limpio : limpio[..149] + "…";
    }

    public static (string Html, string Texto) Componer(string titulo, string preencabezado, CuerpoCorreo cuerpo, string pie)
    {
        var html = new StringBuilder();
        var texto = new StringBuilder();

        html.Append($$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="light">
            <meta name="supported-color-schemes" content="light">
            <title>{{E(titulo)}}</title>
            <style>
              @media (max-width: 620px) {
                .marco { padding: 16px 12px !important; }
                .tarjeta { padding: 24px 20px !important; }
              }
              a { color: {{Ambar}}; }
            </style>
            </head>
            <body style="margin:0;padding:0;background:{{Fondo}};-webkit-text-size-adjust:100%;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{{E(preencabezado)}}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{{Fondo}};">
            <tr><td class="marco" align="center" style="padding:32px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;">
            <tr><td style="padding:0 4px 16px 4px;">
              <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                <td style="vertical-align:middle;padding-right:12px;"><img src="cid:{{IdLogo}}" width="40" height="40" alt="" style="display:block;border:0;"></td>
                <td style="vertical-align:middle;font-family:{{Fuente}};font-size:16px;font-weight:700;color:{{Tinta}};">{{E(Marca)}}<br><span style="font-size:13px;font-weight:500;color:{{TextoTenue}};">SyncTime</span></td>
              </tr></table>
            </td></tr>
            <tr><td class="tarjeta" style="background:#FFFFFF;border:1px solid {{Borde}};border-radius:12px;padding:32px;font-family:{{Fuente}};">
            <h1 style="margin:0 0 16px 0;font-size:22px;line-height:1.3;font-weight:700;color:{{Tinta}};">{{E(titulo)}}</h1>

            """);
        texto.AppendLine(titulo);
        texto.AppendLine(new string('=', Math.Min(titulo.Length, 60)));
        texto.AppendLine();

        foreach (var bloque in cuerpo.Bloques)
        {
            switch (bloque)
            {
                case CuerpoCorreo.Parrafo_ p:
                    html.Append($"""<p style="margin:0 0 16px 0;font-size:16px;line-height:1.6;color:{TextoSecundario};">{E(p.Texto)}</p>""").AppendLine();
                    texto.AppendLine(p.Texto).AppendLine();
                    break;

                case CuerpoCorreo.Datos_ d:
                    html.Append($"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px 0;border-top:1px solid {Borde};">""").AppendLine();
                    foreach (var fila in d.Filas)
                    {
                        var valor = fila.Enlace is null
                            ? E(fila.Valor)
                            : $"""<a href="{E(fila.Enlace)}" style="color:{Ambar};text-decoration:underline;">{E(fila.Valor)}</a>""";
                        html.Append($"""
                            <tr><td style="padding:12px 0;border-bottom:1px solid {Borde};">
                            <div style="font-size:13px;line-height:1.4;color:{TextoTenue};">{E(fila.Etiqueta)}</div>
                            <div style="font-size:16px;line-height:1.5;font-weight:600;color:{Tinta};word-break:break-word;">{valor}</div>
                            </td></tr>
                            """).AppendLine();
                        texto.AppendLine($"{fila.Etiqueta}: {fila.Valor}");
                    }
                    html.AppendLine("</table>");
                    texto.AppendLine();
                    break;

                case CuerpoCorreo.Lista_ l:
                    html.Append($"""<p style="margin:0 0 8px 0;font-size:13px;line-height:1.4;color:{TextoTenue};">{E(l.Titulo)}</p>""").AppendLine();
                    html.Append($"""<ul style="margin:0 0 20px 0;padding:0 0 0 20px;font-size:16px;line-height:1.6;color:{Tinta};">""").AppendLine();
                    texto.AppendLine($"{l.Titulo}:");
                    foreach (var elemento in l.Elementos)
                    {
                        html.Append($"""<li style="margin:0 0 6px 0;">{E(elemento)}</li>""").AppendLine();
                        texto.AppendLine($"- {elemento}");
                    }
                    html.AppendLine("</ul>");
                    texto.AppendLine();
                    break;

                case CuerpoCorreo.Boton_ b:
                    html.Append($"""
                        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:4px 0 20px 0;"><tr>
                        <td style="background:{Ambar};border-radius:8px;">
                        <a href="{E(b.Url)}" style="display:inline-block;padding:14px 24px;font-family:{Fuente};font-size:16px;font-weight:600;line-height:1.2;color:#FFFFFF;text-decoration:none;border-radius:8px;">{E(b.Texto)}</a>
                        </td></tr></table>
                        """).AppendLine();
                    texto.AppendLine($"{b.Texto}: {b.Url}").AppendLine();
                    break;

                // Acciones secundarias: botones en tinta con borde, con area de toque comoda en el telefono.
                // El ambar queda solo para la accion principal.
                case CuerpoCorreo.Enlaces_ en:
                    var enlaces = en.Enlaces.Select(x =>
                        $"""<a href="{E(x.Url)}" style="display:inline-block;margin:0 8px 8px 0;padding:10px 16px;border:1px solid {BordeControl};border-radius:8px;font-family:{Fuente};font-size:15px;font-weight:600;line-height:1.2;color:{Tinta};text-decoration:none;">{E(x.Texto)}</a>""");
                    html.Append($"""<p style="margin:0 0 16px 0;">{string.Concat(enlaces)}</p>""").AppendLine();
                    foreach (var x in en.Enlaces)
                        texto.AppendLine($"{x.Texto}: {x.Url}");
                    texto.AppendLine();
                    break;

                case CuerpoCorreo.Cita_ c:
                    html.Append($"""
                        <p style="margin:0 0 8px 0;font-size:13px;line-height:1.4;color:{TextoTenue};">{E(c.Titulo)}</p>
                        <p style="margin:0 0 20px 0;padding:2px 0 2px 14px;border-left:3px solid {Borde};font-size:16px;line-height:1.6;color:{Tinta};word-break:break-word;">{E(c.Texto)}</p>
                        """).AppendLine();
                    texto.AppendLine($"{c.Titulo}:").AppendLine($"  {c.Texto}").AppendLine();
                    break;

                case CuerpoCorreo.Nota_ n:
                    html.Append($"""<p style="margin:4px 0 0 0;font-size:13px;line-height:1.5;color:{TextoTenue};">{E(n.Texto)}</p>""").AppendLine();
                    texto.AppendLine(n.Texto).AppendLine();
                    break;
            }
        }

        html.Append($$"""
            </td></tr>
            <tr><td style="padding:16px 4px 0 4px;font-family:{{Fuente}};font-size:13px;line-height:1.5;color:{{TextoTenue}};">{{E(pie)}}</td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);
        texto.AppendLine("--");
        texto.AppendLine(pie);
        texto.AppendLine($"{Marca} · SyncTime");

        return (html.ToString(), texto.ToString());
    }
}

// Escape minimo y suficiente para texto y atributos entre comillas.
public static class TextoHtml
{
    // Escapa lo que el HTML interpreta (&, <, >, comillas y apostrofe), tanto en texto como en atributos
    // entre comillas. Las tildes, la ñ, el "+" de los telefonos y el "×" quedan tal cual: el mensaje va en UTF-8.
    public static string Escapar(string texto)
    {
        var salida = new StringBuilder(texto.Length + 16);
        foreach (var c in texto)
        {
            salida.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&#39;",
                _ => c.ToString()
            });
        }
        return salida.ToString();
    }
}
