namespace LetrerosHerreraSYNCTIMEapi.Models;

// Fase 8: cada cambio de estado de un pedido, tambien su creacion (EstadoAnterior null). Siempre lo hace
// un empleado; la nota es la del avance o el motivo de la anulacion.
public class PedidoHistorial
{
    public int IdHistorial { get; set; }
    public int IdPedido { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = null!;
    public string? Nota { get; set; }
    public int IdEmpleado { get; set; }
    public DateTimeOffset Fecha { get; set; }

    public virtual Pedido? Pedido { get; set; }
    public virtual Empleado? Empleado { get; set; }
}
