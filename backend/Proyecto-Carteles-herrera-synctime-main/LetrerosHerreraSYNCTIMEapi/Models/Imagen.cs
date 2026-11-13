namespace LetrerosHerreraSYNCTIMEapi.Models;

// Imagen subida por el taller (RNFP22). Los archivos viven en la carpeta Imagenes:Carpeta como
// "{clave}-grande.webp" y "{clave}-miniatura.webp"; aqui queda quien la subio, cuando y sus medidas.
// La clave es aleatoria (128 bits): una imagen aun no publicada, como un logotipo sin la autorizacion
// del cliente, no se puede encontrar probando numeros.
public class Imagen
{
    public int IdImagen { get; set; }
    public string Clave { get; set; } = null!;

    // Medidas de la imagen original, ya enderezada.
    public int Ancho { get; set; }
    public int Alto { get; set; }
    public int BytesGrande { get; set; }
    public int BytesMiniatura { get; set; }

    public int IdEmpleadoSube { get; set; }
    public DateTimeOffset FechaSubida { get; set; }

    public virtual Empleado? EmpleadoSube { get; set; }
}
