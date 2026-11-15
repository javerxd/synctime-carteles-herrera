using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 10 (decision 3): calibracion del umbral con rostros reales. La enciende RostroFacial:Calibracion:Activa
// (apagada por defecto) y solo la usa gestion. Cada medicion es un intento guiado como el del ingreso, marcado
// «propio» u «otra_persona», y guarda solo numeros. No cambia la configuracion: el umbral sugerido se copia a
// mano a RostroFacial:UmbralDistancia.
public sealed class ServicioCalibracionRostro(
    LetrerosHerreradbContext db,
    RostroFacialService rostros,
    ServicioRostros servicioRostros,
    IConfiguration configuracion,
    TimeProvider reloj,
    ILogger<ServicioCalibracionRostro> logger)
{
    private const int MaxMediciones = 500;
    // Tope de mediciones contra el rostro de un mismo empleado por hora.
    public const int MaxPorEmpleadoYHora = 30;
    // Las distancias se guardan con 2 decimales: bastan para elegir un umbral y no permiten reconstruir una muestra
    // con muchas mediciones exactas (revision de seguridad de la fase 10).
    private const int Decimales = 2;

    public bool Activa => configuracion.GetValue("RostroFacial:Calibracion:Activa", false);

    public async Task<ResultadoRostro<MedicionCalibracion>> MedirAsync(int idRegistra, PedidoMedicion pedido, CancellationToken ct)
    {
        if (!rostros.Configurado)
            return new ResultadoRostro<MedicionCalibracion>.NoDisponible();

        var errores = new Dictionary<string, string[]>();
        if (pedido.Etiqueta is not (Calibracion.Propio or Calibracion.OtraPersona))
            errores["etiqueta"] = ["Indica si el intento es propio (\"propio\") o de otra persona (\"otra_persona\")."];
        var capturas = ServicioRostros.LeerCapturas(pedido.Capturas);
        if (capturas is null)
            errores["capturas"] = ["Faltan las tres capturas (frente y los dos giros) con su descriptor y sus puntos."];
        var empleado = pedido.IdEmpleado is { } id
            ? await db.RostrosEmpleado.AsNoTracking().Include(r => r.Empleado)
                .FirstOrDefaultAsync(r => r.IdEmpleado == id && r.FechaRevocacion == null && r.Empleado!.Activo, ct)
            : null;
        var muestras = empleado is not null && RostroFacialService.EsGestion(empleado.Empleado!.Rol) ? MuestrasCifradas.De(empleado) : null;
        if (muestras is null)
            errores["idEmpleado"] = ["Elige a un empleado de gestión con su rostro registrado con las tres muestras."];
        if (errores.Count > 0)
            return new ResultadoRostro<MedicionCalibracion>.Invalido(errores, "Revisa los datos de la medición.");

        // El desafio se pide con la misma ruta del ingreso, con el email del empleado que se compara.
        var orden = await servicioRostros.ConsumirDesafioAsync(ServicioRostros.NormalizarCorreo(empleado!.Empleado!.Email), pedido.Desafio, ct);
        if (OrdenGiros.Lados(orden) is not { } lados)
            return new ResultadoRostro<MedicionCalibracion>.Invalido(
                new Dictionary<string, string[]> { ["desafio"] = ["El desafío venció o ya se usó. Pide uno nuevo y repite el intento."] },
                "El desafío venció o ya se usó.");

        var haceUnaHora = reloj.GetUtcNow().AddHours(-1);
        if (await db.CalibracionRostro.CountAsync(m => m.IdEmpleado == empleado.IdEmpleado && m.Fecha > haceUnaHora, ct) >= MaxPorEmpleadoYHora)
            return new ResultadoRostro<MedicionCalibracion>.DemasiadosIntentos();

        var medida = rostros.Medir(capturas!.Descriptores, lados.Primero, muestras, empleado.IdEmpleado);
        if (medida is null)
        {
            logger.LogError("No se pudo descifrar una muestra del rostro del empleado {IdEmpleado}: debe registrar su rostro de nuevo.", empleado.IdEmpleado);
            return new ResultadoRostro<MedicionCalibracion>.Invalido(new Dictionary<string, string[]>(),
                "No se pudieron leer las muestras guardadas de ese empleado: debe registrar su rostro de nuevo.");
        }

        var giros = capturas.Giros;
        var girosCorrectos = rostros.Rangos.Cumple(giros, lados.Primero, lados.Segundo);
        var fila = new MedicionCalibracionRostro
        {
            IdEmpleado = empleado.IdEmpleado,
            Etiqueta = pedido.Etiqueta!,
            DistanciaFrente = Redondear(medida.Frente),
            DistanciaPrimerGiro = Redondear(medida.PrimerGiro),
            DistanciaSegundoGiro = Redondear(medida.SegundoGiro),
            DistanciaMaxEntreCapturas = Redondear(medida.MaxEntreCapturas),
            GiroFrente = Redondear(giros[0]),
            GiroPrimero = Redondear(giros[1]),
            GiroSegundo = Redondear(giros[2]),
            UmbralVigente = rostros.Umbral,
            IdEmpleadoRegistra = idRegistra,
            Fecha = reloj.GetUtcNow()
        };
        db.CalibracionRostro.Add(fila);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Empleado {IdRegistra} guardo una medicion de calibracion del rostro", idRegistra);
        return new ResultadoRostro<MedicionCalibracion>.Correcto(Ficha(fila, girosCorrectos && rostros.Coincide(medida)));
    }

    public async Task<ConsultaCalibracion> ConsultarAsync(CancellationToken ct)
    {
        var filas = await db.CalibracionRostro.AsNoTracking()
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.IdMedicion)
            .Take(MaxMediciones)
            .ToListAsync(ct);
        var mediciones = filas.Select(f => Ficha(f, null)).ToList();
        var resumen = Calibracion.Resumir(
            mediciones.Where(m => m.Etiqueta == Calibracion.Propio).Select(m => m.PeorDistancia).ToList(),
            mediciones.Where(m => m.Etiqueta == Calibracion.OtraPersona).Select(m => m.PeorDistancia).ToList());
        var rangos = rostros.Rangos;
        return new ConsultaCalibracion(Activa, rostros.Umbral,
            new RangosVigentes(rangos.FrenteMaximo, rangos.GiroMinimo, rangos.GiroMaximo, rostros.UmbralEntreCapturas),
            mediciones, resumen);
    }

    public async Task BorrarAsync(int idQuien, CancellationToken ct)
    {
        var borradas = await db.CalibracionRostro.ExecuteDeleteAsync(ct);
        logger.LogInformation("Empleado {IdQuien} borro {Cantidad} medicion(es) de calibracion del rostro", idQuien, borradas);
    }

    private static double Redondear(double valor) => Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);

    // habriaEntrado solo se informa al medir: depende del umbral vigente en ese momento.
    private static MedicionCalibracion Ficha(MedicionCalibracionRostro m, bool? habriaEntrado) => new(
        m.IdMedicion, m.IdEmpleado, m.Etiqueta, m.DistanciaFrente, m.DistanciaPrimerGiro, m.DistanciaSegundoGiro, m.DistanciaMaxEntreCapturas,
        Calibracion.PeorDistancia(m.DistanciaFrente, m.DistanciaPrimerGiro, m.DistanciaSegundoGiro),
        m.GiroFrente, m.GiroPrimero, m.GiroSegundo, m.UmbralVigente, m.IdEmpleadoRegistra, m.Fecha, habriaEntrado);
}

public sealed record PedidoMedicion(int? IdEmpleado, string? Etiqueta, string? Desafio, IReadOnlyList<CapturaRostro?>? Capturas);

public sealed record MedicionCalibracion(int IdMedicion, int IdEmpleado, string Etiqueta, double DistanciaFrente, double DistanciaPrimerGiro,
    double DistanciaSegundoGiro, double DistanciaMaxEntreCapturas, double PeorDistancia, double GiroFrente, double GiroPrimero, double GiroSegundo,
    double UmbralVigente, int IdEmpleadoRegistra, DateTimeOffset Fecha, bool? HabriaEntrado);

public sealed record RangosVigentes(double FrenteMaximo, double GiroMinimo, double GiroMaximo, double UmbralEntreCapturas);

public sealed record ConsultaCalibracion(bool Activa, double UmbralVigente, RangosVigentes Rangos, IReadOnlyList<MedicionCalibracion> Mediciones,
    ResumenCalibracion Resumen);
