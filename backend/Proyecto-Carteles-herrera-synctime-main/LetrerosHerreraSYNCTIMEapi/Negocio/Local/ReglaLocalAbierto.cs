namespace LetrerosHerreraSYNCTIMEapi.Negocio.Local;

// Un dia del horario semanal (1 = lunes ... 7 = domingo).
public sealed record FranjaHorario(int DiaSemana, string NombreDia, bool Abierto, TimeOnly? Inicio, TimeOnly? Fin)
{
    public bool Atiende => Abierto && Inicio is not null && Fin is not null && Fin > Inicio;
}

// Lo que dice el cartel electronico, que el personal cambia a mano.
public sealed record EstadoCartelLocal(bool AbiertoEnLocal, string MensajeEstado, string? TiempoEstimadoRegreso, DateTime FechaUltimaModificacion);

public sealed record ProximaApertura(int DiaSemana, string NombreDia, TimeOnly Hora, bool EsHoy);

public sealed record EstadoLocalAhora(
    bool AbiertoAhora,
    string Motivo,
    bool DentroDeHorario,
    DateTime HoraChile,
    FranjaHorario? Hoy,
    ProximaApertura? ProximaApertura,
    EstadoCartelLocal? Cartel,
    IReadOnlyList<FranjaHorario> Semana);

// Regla de «abierto ahora» (RFS02). El horario dice cuando se atiende; el cartel dice si alguien esta.
//   - Abierto solo si es horario de atencion (hora de Chile) Y el cartel dice abierto.
//   - En horario, con el cartel cerrado: «ausente» (por ejemplo, salio a instalar): se muestra su
//     mensaje y el regreso estimado.
//   - Fuera de horario: cerrado aunque el cartel haya quedado en abierto; se informa la proxima apertura.
//   - La hora de cierre ya es cerrado (de 9:00 a 18:30 significa hasta las 18:29).
public static class ReglaLocalAbierto
{
    public const string Abierto = "abierto";
    public const string Ausente = "ausente";
    public const string FueraDeHorario = "fuera_de_horario";
    public const string CerradoHoy = "cerrado_hoy";

    public static int DiaSemana(DateTime fecha) => fecha.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)fecha.DayOfWeek;

    public static EstadoLocalAhora Calcular(IReadOnlyList<FranjaHorario> semana, EstadoCartelLocal? cartel, DateTime ahoraChile)
    {
        var dia = DiaSemana(ahoraChile);
        var hora = TimeOnly.FromDateTime(ahoraChile);
        var hoy = semana.FirstOrDefault(f => f.DiaSemana == dia);
        var dentro = hoy is { Atiende: true } && hora >= hoy.Inicio && hora < hoy.Fin;
        var cartelAbierto = cartel?.AbiertoEnLocal ?? true;

        var motivo = !dentro
            ? (hoy is { Atiende: true } ? FueraDeHorario : CerradoHoy)
            : cartelAbierto ? Abierto : Ausente;

        return new EstadoLocalAhora(
            AbiertoAhora: motivo == Abierto,
            Motivo: motivo,
            DentroDeHorario: dentro,
            HoraChile: ahoraChile,
            Hoy: hoy,
            ProximaApertura: motivo == Abierto ? null : Proxima(semana, dia, hora),
            Cartel: cartel,
            Semana: semana.OrderBy(f => f.DiaSemana).ToList());
    }

    // La proxima vez que empieza una franja de atencion, mirando hasta una semana adelante.
    private static ProximaApertura? Proxima(IReadOnlyList<FranjaHorario> semana, int dia, TimeOnly hora)
    {
        for (var desplazamiento = 0; desplazamiento <= 7; desplazamiento++)
        {
            var numero = (dia - 1 + desplazamiento) % 7 + 1;
            var franja = semana.FirstOrDefault(f => f.DiaSemana == numero);
            if (franja is not { Atiende: true })
                continue;
            if (desplazamiento == 0 && hora >= franja.Inicio)
                continue;
            return new ProximaApertura(numero, franja.NombreDia, franja.Inicio!.Value, desplazamiento == 0);
        }
        return null;
    }
}

// La hora oficial de Chile continental, con su horario de verano, sin depender de la zona del servidor
// (en un contenedor suele ser UTC).
// Necesita los datos de zonas horarias del sistema (en Linux, el paquete tzdata). Program.cs la consulta al
// arrancar: si faltan, la API no levanta con un mensaje claro, en vez de fallar en cada peticion.
public static class HoraChile
{
    // Nombre IANA (Linux, Windows con ICU) y nombre de Windows de la misma zona.
    private static readonly string[] Nombres = ["America/Santiago", "Pacific SA Standard Time"];

    private static readonly Lazy<TimeZoneInfo> Zona = new(Buscar);

    public static TimeZoneInfo ZonaHoraria => Zona.Value;

    public static DateTime Convertir(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Zona.Value).DateTime;

    private static TimeZoneInfo Buscar()
    {
        foreach (var nombre in Nombres)
            if (TimeZoneInfo.TryFindSystemTimeZoneById(nombre, out var zona))
                return zona;
        throw new InvalidOperationException(
            "El servidor no tiene la zona horaria de Chile (America/Santiago). En Linux instala el paquete tzdata y no actives InvariantTimezone.");
    }
}
