using System.Globalization;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Local;

// ── Fase 9: horario semanal del taller (E10, RFS02) y disponibilidad del personal (RFU20) ──
// Sin HTTP ni base de datos: se prueban solas en LetrerosHerreraSYNCTIMEapi.UnitTests. Las horas viajan
// como texto «HH:mm» (como las escribe un <input type="time">) y se guardan sin segundos.

public sealed record EntradaDiaHorario(int DiaSemana, bool Abierto, string? HoraInicio, string? HoraFin);
public sealed record DiaHorario(byte DiaSemana, string NombreDia, bool Abierto, TimeOnly? HoraInicio, TimeOnly? HoraFin);

public sealed record EntradaFranja(int DiaSemana, string? HoraInicio, string? HoraFin);
public sealed record Franja(byte DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFin);

// Lo que ve la portada: por dia, las franjas en que hay alguien atendiendo (sin nombres).
public sealed record FranjaAtencion(string Desde, string Hasta);
public sealed record AtencionDelDia(int DiaSemana, string NombreDia, IReadOnlyList<FranjaAtencion> Franjas);

public static class ReglasHorario
{
    public const int MaximoFranjas = 28;

    private static readonly string[] Nombres = ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];

    public static string NombreDia(int dia) => dia is >= 1 and <= 7 ? Nombres[dia - 1] : "";

    public static string Texto(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    // «9:00», «09:00» o «09:00:00». Los segundos se descartan: el horario se maneja por minutos.
    public static bool TryLeerHora(string? texto, out TimeOnly hora)
    {
        hora = default;
        if (string.IsNullOrWhiteSpace(texto))
            return false;
        string[] formatos = ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss"];
        if (!TimeOnly.TryParseExact(texto.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var leida))
            return false;
        hora = new TimeOnly(leida.Hour, leida.Minute);
        return true;
    }

    // Los 7 dias, una vez cada uno. Si abre, apertura antes del cierre; si no abre, sin horas. Los errores
    // van por «{dia}.horaInicio» o «{dia}.horaFin», como el formulario.
    public static (IReadOnlyList<DiaHorario> Dias, Dictionary<string, string[]> Errores) Validar(IReadOnlyList<EntradaDiaHorario?>? lista)
    {
        var errores = new Dictionary<string, string[]>();
        var resultado = new List<DiaHorario>();
        // Un elemento null en la lista ([..., null]) es un error de ese indice, no un 500.
        for (var i = 0; i < (lista?.Count ?? 0); i++)
            if (lista![i] is null)
                errores[$"dias[{i}]"] = ["Falta este día."];
        if (errores.Count > 0)
            return (resultado, errores);
        var dias = lista?.Select(d => d!).ToList();
        if (dias is null || dias.Count != 7 || dias.Select(d => d.DiaSemana).Distinct().Count() != 7 || dias.Any(d => d.DiaSemana is < 1 or > 7))
        {
            errores["dias"] = ["Envía los 7 días de la semana, de lunes (1) a domingo (7), una vez cada uno."];
            return (resultado, errores);
        }

        foreach (var dia in dias.OrderBy(d => d.DiaSemana))
        {
            if (!dia.Abierto)
            {
                resultado.Add(new DiaHorario((byte)dia.DiaSemana, NombreDia(dia.DiaSemana), false, null, null));
                continue;
            }
            var hayInicio = TryLeerHora(dia.HoraInicio, out var inicio);
            var hayFin = TryLeerHora(dia.HoraFin, out var fin);
            if (!hayInicio)
                errores[$"{dia.DiaSemana}.horaInicio"] = [$"Escribe la hora de apertura del {NombreDia(dia.DiaSemana).ToLowerInvariant()}."];
            if (!hayFin)
                errores[$"{dia.DiaSemana}.horaFin"] = [$"Escribe la hora de cierre del {NombreDia(dia.DiaSemana).ToLowerInvariant()}."];
            else if (hayInicio && fin <= inicio)
                errores[$"{dia.DiaSemana}.horaFin"] = ["El cierre debe ser posterior a la apertura."];
            resultado.Add(new DiaHorario((byte)dia.DiaSemana, NombreDia(dia.DiaSemana), true, inicio, fin));
        }
        return (resultado, errores);
    }

    // Las franjas propias reemplazan a todas las anteriores. Cada una: un dia valido, inicio antes que fin,
    // dentro del horario de ese dia (un dia cerrado no admite franjas) y sin cruzarse con otra del mismo dia
    // (pueden tocarse: 9:00-13:00 y 13:00-18:00). Errores por «franjas[i].campo».
    public static (IReadOnlyList<Franja> Franjas, Dictionary<string, string[]> Errores) ValidarFranjas(
        IReadOnlyList<EntradaFranja?>? franjas, IReadOnlyList<DiaHorario> horario)
    {
        var errores = new Dictionary<string, string[]>();
        var validas = new List<(int Indice, Franja Franja)>();
        if (franjas is null)
        {
            errores["franjas"] = ["Envía la lista de franjas (puede estar vacía)."];
            return ([], errores);
        }
        if (franjas.Count > MaximoFranjas)
        {
            errores["franjas"] = [$"Puedes registrar hasta {MaximoFranjas} franjas."];
            return ([], errores);
        }

        for (var i = 0; i < franjas.Count; i++)
        {
            var clave = $"franjas[{i}]";
            if (franjas[i] is not { } f)
            {
                errores[clave] = ["Falta esta franja."];
                continue;
            }
            if (f.DiaSemana is < 1 or > 7)
            {
                errores[$"{clave}.diaSemana"] = ["El día debe ir de lunes (1) a domingo (7)."];
                continue;
            }
            var hayInicio = TryLeerHora(f.HoraInicio, out var inicio);
            var hayFin = TryLeerHora(f.HoraFin, out var fin);
            if (!hayInicio)
                errores[$"{clave}.horaInicio"] = ["Escribe la hora de inicio."];
            if (!hayFin)
                errores[$"{clave}.horaFin"] = ["Escribe la hora de término."];
            if (!hayInicio || !hayFin)
                continue;
            if (fin <= inicio)
            {
                errores[$"{clave}.horaFin"] = ["El término debe ser posterior al inicio."];
                continue;
            }
            var dia = horario.FirstOrDefault(d => d.DiaSemana == f.DiaSemana);
            var nombre = NombreDia(f.DiaSemana).ToLowerInvariant();
            // Un dia abierto sin horas se trata como cerrado.
            if (dia is not { Abierto: true, HoraInicio: not null, HoraFin: not null })
            {
                errores[$"{clave}.diaSemana"] = [$"El taller no abre el {nombre}: ese día no admite franjas."];
                continue;
            }
            if (inicio < dia.HoraInicio || fin > dia.HoraFin)
            {
                errores[$"{clave}.horaInicio"] = [$"La franja debe quedar dentro del horario del {nombre} ({Texto(dia.HoraInicio!.Value)} a {Texto(dia.HoraFin!.Value)})."];
                continue;
            }
            validas.Add((i, new Franja((byte)f.DiaSemana, inicio, fin)));
        }

        foreach (var delDia in validas.GroupBy(v => v.Franja.DiaSemana))
        {
            var ordenadas = delDia.OrderBy(v => v.Franja.HoraInicio).ToList();
            for (var j = 1; j < ordenadas.Count; j++)
            {
                if (ordenadas[j].Franja.HoraInicio < ordenadas[j - 1].Franja.HoraFin)
                    errores[$"franjas[{ordenadas[j].Indice}].horaInicio"] = ["Se cruza con otra franja del mismo día."];
            }
        }
        return (validas.OrderBy(v => v.Franja.DiaSemana).ThenBy(v => v.Franja.HoraInicio).Select(v => v.Franja).ToList(), errores);
    }

    // Une las franjas de todo el personal: por dia, los tramos que se cruzan o se tocan quedan en uno. Siempre
    // devuelve los 7 dias (un dia sin nadie, con la lista vacia).
    public static IReadOnlyList<AtencionDelDia> Unir(IEnumerable<Franja> franjas)
    {
        var porDia = franjas.GroupBy(f => f.DiaSemana).ToDictionary(g => (int)g.Key, g => g.OrderBy(f => f.HoraInicio).ToList());
        var semana = new List<AtencionDelDia>();
        for (var dia = 1; dia <= 7; dia++)
        {
            var unidas = new List<(TimeOnly Desde, TimeOnly Hasta)>();
            foreach (var f in porDia.GetValueOrDefault(dia) ?? [])
            {
                if (unidas.Count > 0 && f.HoraInicio <= unidas[^1].Hasta)
                    unidas[^1] = (unidas[^1].Desde, f.HoraFin > unidas[^1].Hasta ? f.HoraFin : unidas[^1].Hasta);
                else
                    unidas.Add((f.HoraInicio, f.HoraFin));
            }
            semana.Add(new AtencionDelDia(dia, NombreDia(dia), unidas.Select(u => new FranjaAtencion(Texto(u.Desde), Texto(u.Hasta))).ToList()));
        }
        return semana;
    }
}
