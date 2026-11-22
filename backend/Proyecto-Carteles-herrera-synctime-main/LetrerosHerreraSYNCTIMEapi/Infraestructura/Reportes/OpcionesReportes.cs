namespace LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;

// Fase 13A (RNFP18): la configuracion de las exportaciones de reportes (seccion «Reportes» de appsettings). Valores por omision
// = los del plan aprobado: archivos que vencen a las 24 horas, tope de 50.000 filas y dos trabajos a la vez.
public sealed class OpcionesReportes
{
    public const string Seccion = "Reportes";

    // Carpeta PRIVADA (ignorada por git: **/almacen/). Ninguna ruta publica la sirve: solo /api/v1/reportes/exportaciones/{id}/archivo,
    // que exige sesion y que el trabajo sea de quien lo pide.
    public string Carpeta { get; set; } = "almacen/exportaciones";

    // Cuanto dura el archivo despues de terminar; pasado ese plazo se borra y la descarga responde 410.
    public int HorasDeVigencia { get; set; } = 24;

    // Un reporte con mas filas que esto se rechaza («acota el rango»); nunca se genera a medias.
    public int MaximoDeFilas { get; set; } = 50_000;

    // El PDF tiene un tope propio, mas bajo: generarlo con 50.000 filas pasa de los 120 segundos y de 1 GB de memoria.
    public int MaximoDeFilasPdf { get; set; } = 10_000;

    // Trabajos que se generan a la vez, y el tiempo maximo de cada uno.
    public int MaximoSimultaneos { get; set; } = 2;
    public int SegundosLimitePorTrabajo { get; set; } = 120;

    // Archivos listos (sin vencer) que una persona puede tener a la vez: sin este tope, pedir uno tras otro llenaria el disco.
    public int MaximoArchivosVivosPorPersona { get; set; } = 5;

    // Cuantas veces se retoma un trabajo que el proceso dejo a medias antes de darlo por fallido.
    public int MaximoIntentos { get; set; } = 3;

    // Las pruebas apagan el proceso en segundo plano y lo ejecutan a mano.
    public bool ColaActiva { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 5;
}
