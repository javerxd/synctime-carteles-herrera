using System.Text.Json.Serialization;
using LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;

// La cotizacion (S06, fase 8): la misma para el personal, para el cliente y para el correo «cotizacion
// lista». Las fechas, en hora de Chile. Los montos enteros van como int; el subtotal de un material y el
// total pueden tener centavos (la cantidad admite 2 decimales), por eso tambien va el total redondeado.
public sealed record FichaCotizacion(
    int IdCotizacion,
    string Numero,
    int IdSolicitud,
    string NumeroSolicitud,
    DateTime FechaChile,
    string Estado,
    DatosCliente Cliente,
    IReadOnlyList<ItemCotizacion> Items,
    IReadOnlyList<MaterialCotizacion> Materiales,
    decimal CostoMaterialesTotal,
    int CostoManoObra,
    int OtrosCostos,
    decimal Total,
    int TotalRedondeado,
    string? Observaciones,
    PersonaDelTaller ElaboradaPor,
    RespuestaCotizacion? Respuesta,
    int? IdPedido,
    bool PedidoEliminado,
    byte[] RowVersion)
{
    // El cliente ve la misma cotizacion, sin el id de quien la elaboro y solo con su nombre de pila.
    public FichaCotizacion ParaElCliente() => this with
    {
        ElaboradaPor = new PersonaDelTaller(null, NombreDePila(ElaboradaPor.Nombre)),
        Respuesta = Respuesta is null ? null : Respuesta with
        {
            RegistradaPor = Respuesta.RegistradaPor is null ? null : new PersonaDelTaller(null, NombreDePila(Respuesta.RegistradaPor.Nombre))
        }
    };

    public static string NombreDePila(string nombre) =>
        nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
}

public sealed record DatosCliente(int IdCliente, string Nombre, string Apellido, string Telefono, string? Correo);

// Un letrero de la solicitud, tal como lo pidio el cliente.
public sealed record ItemCotizacion(string Producto, string? Descripcion, int? AnchoMm, int? AltoMm, int Cantidad, decimal? AreaM2);

public sealed record MaterialCotizacion(int IdMaterial, string Material, string Unidad, string? MedidaAplicada, decimal Cantidad, int CostoUnitario, decimal Subtotal);

// Alguien del taller. IdEmpleado no se envia cuando es null (lo que ve el cliente).
public sealed record PersonaDelTaller(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IdEmpleado,
    string Nombre);

// RegistradaPor: quien del personal marco la respuesta (telefono, WhatsApp o en el local); null si
// respondio el cliente desde la web. Solo trae el nombre.
public sealed record RespuestaCotizacion(DateTime FechaChile, string Via, string? Motivo, PersonaDelTaller? RegistradaPor);

// Una fila del listado del personal.
public sealed record FilaCotizacion(
    int IdCotizacion,
    string Numero,
    int IdSolicitud,
    DateTime FechaChile,
    ClienteFila Cliente,
    decimal Total,
    string Estado,
    PersonaDelTaller ElaboradaPor);

// Las cotizaciones de una solicitud en su ficha del personal, de la mas nueva a la mas antigua (sin las
// eliminadas). IdPedido y PedidoEliminado: una cotizacion da a lo mas un pedido, aunque despues se elimine,
// asi que con IdPedido la pantalla nunca vuelve a ofrecer «Crear pedido».
public sealed record CotizacionDeLaSolicitud(int IdCotizacion, string Numero, DateTime FechaChile, decimal Total, string Estado,
    int? IdPedido, bool PedidoEliminado);

// La cotizacion vigente de una solicitud, en el seguimiento del cliente (S04).
public sealed record SeguimientoCotizacion(int IdCotizacion, string Numero, decimal Total, string Estado);
