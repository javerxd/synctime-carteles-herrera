using System.Text.RegularExpressions;

namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;

// Fase 16C-1 (H12, D1): donde se guardan los archivos de los clientes (adjuntos privados, imagenes de la vitrina y reportes exportados).
//   Almacen:Modo   Disco (desarrollo y compose: las carpetas de Adjuntos, Imagenes y Reportes) o Blob (la nube: Azure Blob Storage).
//   Render gratuito no tiene disco persistente: sin Blob, cada reinicio borraria los adjuntos de los clientes.
// Son clases y no records a proposito: un record imprime todas sus propiedades en ToString, y bastaria un registro descuidado para que la
// cadena de conexion (lleva la clave de la cuenta) terminara en un log.
public sealed partial class OpcionesAlmacen
{
    public const string Seccion = "Almacen";
    public const string ModoDisco = "Disco";
    public const string ModoBlob = "Blob";

    public string Modo { get; set; } = ModoDisco;
    public OpcionesBlob Blob { get; set; } = new();

    public bool EsBlob => string.Equals(Modo, ModoBlob, StringComparison.OrdinalIgnoreCase);
    public bool EsDisco => string.Equals(Modo, ModoDisco, StringComparison.OrdinalIgnoreCase);

    // Reglas de nombre de un contenedor de Azure: 3 a 63 caracteres, minusculas, numeros y guiones, sin guion al borde ni doble.
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){2,62}\z")]
    private static partial Regex NombreDeContenedor();

    /// <summary>Por que la configuracion no sirve, o null si es valida. El texto nunca repite la cadena de conexion.</summary>
    public string? Problema()
    {
        if (!EsDisco && !EsBlob)
            return $"Almacen:Modo debe ser {ModoDisco} o {ModoBlob}.";
        if (!EsBlob)
            return null;
        if (string.IsNullOrWhiteSpace(Blob.CadenaDeConexion))
            return "Almacen:Blob:CadenaDeConexion es obligatoria con Almacen:Modo=Blob (variable Almacen__Blob__CadenaDeConexion; es un secreto, no se escribe en el repositorio).";
        if (!NombreDeContenedor().IsMatch(Blob.Contenedor ?? ""))
            return "Almacen:Blob:Contenedor debe tener de 3 a 63 caracteres: minusculas, numeros y guiones (sin guion al principio, al final ni doble).";
        if (Blob.TiempoEsperaSegundos is < 1 or > 300)
            return "Almacen:Blob:TiempoEsperaSegundos debe estar entre 1 y 300.";
        if (Blob.ReintentosMaximos is < 0 or > 10)
            return "Almacen:Blob:ReintentosMaximos debe estar entre 0 y 10.";
        return null;
    }

    public override string ToString() => $"Almacen {Modo}";
}

public sealed class OpcionesBlob
{
    // La clave de la cuenta de almacenamiento va aqui: se carga como variable de entorno (secreto de Render), nunca en un archivo.
    public string? CadenaDeConexion { get; set; }

    // Contenedor PRIVADO (sin acceso publico). Las carpetas adjuntos/, imagenes/ y exportaciones/ viven dentro como prefijos.
    public string Contenedor { get; set; } = "archivos";

    // Tiempo maximo de cada llamada a la nube y reintentos del SDK ante fallos transitorios.
    public int TiempoEsperaSegundos { get; set; } = 30;
    public int ReintentosMaximos { get; set; } = 3;

    public override string ToString() => Contenedor;
}
