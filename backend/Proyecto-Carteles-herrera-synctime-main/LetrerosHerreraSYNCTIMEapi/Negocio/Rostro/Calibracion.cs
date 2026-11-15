namespace LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;

// Fase 10 (decision 3 del usuario): resumen de las mediciones de calibracion y umbral sugerido. Cada intento
// se resume en su PEOR distancia (la mayor de las tres contra las muestras), que es la que decide si entra:
// el ingreso acepta cuando todas son <= umbral.
//
//  - Si las series se separan (todo «propio» queda por debajo de todo «otra persona»), el umbral sugerido es
//    el punto medio entre el maximo de «propio» y el minimo de «otra», y el margen es la mitad de ese hueco.
//  - Si se solapan, se sugiere el umbral que comete menos errores (propios rechazados mas ajenos aceptados);
//    entre varios con los mismos errores, el menor, que acepta menos ajenos. El margen sale negativo: mide
//    cuanto se solapan.
//
// No cambia la configuracion: el umbral se copia a mano a RostroFacial:UmbralDistancia.
public static class Calibracion
{
    public const string Propio = "propio";
    public const string OtraPersona = "otra_persona";

    private const int Decimales = 4;

    public static double PeorDistancia(double frente, double primerGiro, double segundoGiro) =>
        Math.Max(frente, Math.Max(primerGiro, segundoGiro));

    public static ResumenCalibracion Resumir(IReadOnlyCollection<double> propias, IReadOnlyCollection<double> ajenas)
    {
        var propio = Serie(propias);
        var otra = Serie(ajenas);
        if (propio is null || otra is null)
            return new ResumenCalibracion(propio ?? SerieCalibracion.Vacia, otra ?? SerieCalibracion.Vacia, null, null);

        var maxPropio = propias.Max();
        var minOtra = ajenas.Min();
        var margen = Redondear((minOtra - maxPropio) / 2);
        if (maxPropio < minOtra)
            return new ResumenCalibracion(propio, otra, Redondear((maxPropio + minOtra) / 2), margen);

        return new ResumenCalibracion(propio, otra, Redondear(MenosErrores(propias, ajenas)), margen);
    }

    // Errores de un umbral t: propios con distancia > t (rechazados) mas ajenos con distancia <= t (aceptados).
    public static int Errores(IEnumerable<double> propias, IEnumerable<double> ajenas, double umbral) =>
        propias.Count(d => d > umbral) + ajenas.Count(d => d <= umbral);

    private static double MenosErrores(IReadOnlyCollection<double> propias, IReadOnlyCollection<double> ajenas)
    {
        // Basta probar los valores medidos: entre dos seguidos los errores no cambian.
        var candidatos = propias.Concat(ajenas).Distinct().Order().ToList();
        var mejor = candidatos[0];
        var menos = int.MaxValue;
        foreach (var t in candidatos)
        {
            var errores = Errores(propias, ajenas, t);
            if (errores < menos)
            {
                menos = errores;
                mejor = t;
            }
        }
        return mejor;
    }

    private static SerieCalibracion? Serie(IReadOnlyCollection<double> valores) =>
        valores.Count == 0
            ? null
            : new SerieCalibracion(valores.Count, Redondear(valores.Min()), Redondear(valores.Max()), Redondear(valores.Average()));

    private static double Redondear(double valor) => Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);
}

public sealed record SerieCalibracion(int N, double? Min, double? Max, double? Promedio)
{
    public static readonly SerieCalibracion Vacia = new(0, null, null, null);
}

public sealed record ResumenCalibracion(SerieCalibracion Propio, SerieCalibracion Otra, double? UmbralSugerido, double? Margen);
