namespace LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;

// Fase 10: medida del giro de la cabeza a partir de los 68 puntos del rostro (face-api), en coordenadas del
// cuadro de video SIN espejo. Es la misma cuenta que hace el navegador en src/rostro/giro.js: los dos lados
// se prueban con los mismos casos (LetrerosHerreraSYNCTIMEapi.UnitTests/CasosGiro.json).
//
//   r = (x[30] - x[0]) / (x[16] - x[0])   la punta de la nariz entre los extremos de la mandibula
//   g = (r - 0.5) * 2, limitado a [-1, 1]
//
// g > 0 es un giro hacia la IZQUIERDA de la persona (en la imagen sin espejo su lado izquierdo queda a la
// derecha); g < 0, hacia su derecha. Las operaciones van en este orden exacto para que el navegador y el
// servidor den el mismo numero (doble precision IEEE 754 en los dos).
public static class Giro
{
    public const int CantidadPuntos = 68;
    public const int MandibulaInicio = 0;
    public const int MandibulaFin = 16;
    public const int PuntaNariz = 30;

    // La mandibula debe ocupar al menos esta fraccion del ancho del cuadro: con menos, el rostro esta muy
    // lejos o los puntos no son de un rostro, y la cuenta no significa nada.
    public const double FraccionMinimaMandibula = 0.10;

    // Cuadros mas grandes que esto no los entrega ninguna camara web: se rechazan sin calcular.
    public const double LadoMaximoCuadro = 10_000;

    // El indice g, o null si los datos no sirven: no hay 68 puntos de dos numeros, alguno no es finito o
    // queda fuera del cuadro, el cuadro no tiene medidas validas o la mandibula es demasiado angosta.
    public static double? Indice(IReadOnlyList<double[]?>? puntos, double ancho, double alto)
    {
        if (!double.IsFinite(ancho) || !double.IsFinite(alto) || ancho <= 0 || alto <= 0 || ancho > LadoMaximoCuadro || alto > LadoMaximoCuadro)
            return null;
        if (puntos is not { Count: CantidadPuntos })
            return null;
        foreach (var punto in puntos)
        {
            if (punto is not { Length: 2 } || !double.IsFinite(punto[0]) || !double.IsFinite(punto[1])
                || punto[0] < 0 || punto[0] > ancho || punto[1] < 0 || punto[1] > alto)
                return null;
        }

        var x0 = puntos[MandibulaInicio]![0];
        var x16 = puntos[MandibulaFin]![0];
        var x30 = puntos[PuntaNariz]![0];
        var mandibula = x16 - x0;
        if (mandibula < FraccionMinimaMandibula * ancho)
            return null;

        var r = (x30 - x0) / mandibula;
        var g = (r - 0.5) * 2;
        return Math.Clamp(g, -1, 1);
    }
}

// El lado hacia el que gira la persona (su izquierda o su derecha, no la de la imagen).
public enum LadoGiro
{
    Izquierda,
    Derecha
}

// Que le falta a una captura para valer (Ninguno si vale). El registro lo usa para explicar como repetirla.
public enum ProblemaGiro
{
    Ninguno,
    NoEstaDeFrente,
    LadoContrario,
    GiroInsuficiente,
    GiroExcesivo
}

// Rangos configurables (RostroFacial:FrenteMaximo, :GiroMinimo y :GiroMaximo). Todos los extremos valen.
public sealed record RangosGiro(double FrenteMaximo, double GiroMinimo, double GiroMaximo)
{
    public static readonly RangosGiro PorDefecto = new(0.15, 0.30, 0.75);

    // La frente debe quedar por debajo del giro minimo, y el giro maximo no puede pasar de 1 (el tope de g).
    public bool SonValidos =>
        double.IsFinite(FrenteMaximo) && double.IsFinite(GiroMinimo) && double.IsFinite(GiroMaximo)
        && FrenteMaximo > 0 && FrenteMaximo < GiroMinimo && GiroMinimo < GiroMaximo && GiroMaximo <= 1;

    public bool EsFrente(double g) => Revisar(null, g) == ProblemaGiro.Ninguno;

    public bool EsGiro(LadoGiro lado, double g) => Revisar(lado, g) == ProblemaGiro.Ninguno;

    // Los tres giros de un intento (frente, primer giro y segundo giro) cumplen el orden pedido.
    public bool Cumple(IReadOnlyList<double> giros, LadoGiro primero, LadoGiro segundo) =>
        giros.Count == 3 && EsFrente(giros[0]) && EsGiro(primero, giros[1]) && EsGiro(segundo, giros[2]);

    // lado null: la captura de frente.
    public ProblemaGiro Revisar(LadoGiro? lado, double g)
    {
        if (lado is null)
            return Math.Abs(g) <= FrenteMaximo ? ProblemaGiro.Ninguno : ProblemaGiro.NoEstaDeFrente;

        // Hacia la izquierda g es positivo; hacia la derecha, negativo. Se compara el valor con su signo.
        var hacia = lado == LadoGiro.Izquierda ? g : -g;
        if (hacia <= 0)
            return ProblemaGiro.LadoContrario;
        if (hacia < GiroMinimo)
            return ProblemaGiro.GiroInsuficiente;
        return hacia > GiroMaximo ? ProblemaGiro.GiroExcesivo : ProblemaGiro.Ninguno;
    }
}

// El orden de los dos giros que pide el desafio. Lo elige el servidor; el navegador no puede elegirlo.
public static class OrdenGiros
{
    public const string IzquierdaDerecha = "izquierda_derecha";
    public const string DerechaIzquierda = "derecha_izquierda";

    public static readonly string[] Validos = [IzquierdaDerecha, DerechaIzquierda];

    // Los dos lados, en el orden pedido. null si el codigo no es uno de los dos.
    public static (LadoGiro Primero, LadoGiro Segundo)? Lados(string? orden) => orden switch
    {
        IzquierdaDerecha => (LadoGiro.Izquierda, LadoGiro.Derecha),
        DerechaIzquierda => (LadoGiro.Derecha, LadoGiro.Izquierda),
        _ => null
    };

    // Como lo recibe el navegador: ["izquierda","derecha"] o ["derecha","izquierda"].
    public static string[] ParaElNavegador(string orden) =>
        orden == IzquierdaDerecha ? ["izquierda", "derecha"] : ["derecha", "izquierda"];
}
