namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 10 (decision 3): una medicion de la calibracion del rostro. Solo numeros: las distancias contra las
// muestras del empleado y entre las capturas, y los giros medidos. Ningun descriptor, punto ni imagen.
public class MedicionCalibracionRostro
{
    public int IdMedicion { get; set; }
    // El rostro contra el que se comparo.
    public int IdEmpleado { get; set; }
    // propio u otra_persona (Negocio.Rostro.Calibracion).
    public string Etiqueta { get; set; } = null!;
    public double DistanciaFrente { get; set; }
    public double DistanciaPrimerGiro { get; set; }
    public double DistanciaSegundoGiro { get; set; }
    public double DistanciaMaxEntreCapturas { get; set; }
    public double GiroFrente { get; set; }
    public double GiroPrimero { get; set; }
    public double GiroSegundo { get; set; }
    public double UmbralVigente { get; set; }
    public int IdEmpleadoRegistra { get; set; }
    public DateTimeOffset Fecha { get; set; }

    public virtual Empleado? Empleado { get; set; }
    public virtual Empleado? EmpleadoRegistra { get; set; }
}
