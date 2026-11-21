using System.Reflection;
using PdfSharp.Fonts;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RFS10): la fuente del PDF. Plus Jakarta Sans (la de toda la interfaz, SIL OFL 1.1) viaja como recurso incrustado en el
// programa: el PDF sale igual en Windows y en Linux y no depende de las fuentes instaladas. Los dos tipos de letra son los TrueType
// de la fuente, sin cambios. El texto del PDF queda como texto (seleccionable), con la fuente incrustada.
public static class FuentesReporte
{
    public const string Familia = "Plus Jakarta Sans";
    private const string Regular = "PlusJakartaSans-Regular";
    private const string Negrita = "PlusJakartaSans-Bold";

    private static readonly Lazy<byte[]> DatosRegular = new(() => Leer(Regular));
    private static readonly Lazy<byte[]> DatosNegrita = new(() => Leer(Negrita));
    private static readonly object Candado = new();
    private static bool registrada;

    public static byte[] Datos(bool negrita) => negrita ? DatosNegrita.Value : DatosRegular.Value;

    /// <summary>Registra el resolvedor de PDFsharp una sola vez por proceso (la libreria no admite cambiarlo despues).</summary>
    public static void Registrar()
    {
        lock (Candado)
        {
            if (registrada)
                return;
            GlobalFontSettings.FontResolver = new ResolvedorDeFuentes();
            registrada = true;
        }
    }

    private static byte[] Leer(string nombre)
    {
        using var flujo = typeof(FuentesReporte).Assembly.GetManifestResourceStream($"SyncTime.fuente.{nombre}.ttf")
            ?? throw new InvalidOperationException($"Falta la fuente incrustada {nombre}.");
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    private sealed class ResolvedorDeFuentes : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            // Cualquier familia pedida cae en la nuestra: nunca se busca una fuente del equipo.
            new(isBold ? Negrita : Regular);

        public byte[]? GetFont(string faceName) => faceName switch
        {
            Regular => Datos(negrita: false),
            Negrita => Datos(negrita: true),
            _ => null
        };
    }
}
