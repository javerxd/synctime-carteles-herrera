namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fila de la vista vw_metricas_conversion_online: solo lectura, sin clave.
public class MetricaConversionOnline
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int TotalSolicitudesOnline { get; set; }
    public int SolicitudesConcretadasEnPedido { get; set; }
    public decimal PorcentajeConversion { get; set; }
}
