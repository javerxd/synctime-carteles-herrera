using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

// Un pedido tiene dos estados independientes (revision de la profesora guia, 12-11-2026):
// - EstadoPedido, operativo: aceptado -> en_proceso -> terminado -> entregado, o anulado.
//   Se dice "anulado" y no "cancelado", porque en Chile "cancelado" se entiende como "pagado".
// - EstadoPago, financiero: no_pagado, abonado o pagado_total. Lo calcula la base a partir de
//   MontoAbonado y del monto final, asi nunca contradice a los montos.
// El ciclo de un pedido se cierra solo cuando esta entregado Y pagado_total.
public class Pedido
{
    public int IdPedido { get; set; }

    // Todo pedido tiene solicitud y cotizacion. Un pedido presencial directo (el cliente acuerda el
    // trabajo en el taller, sin solicitud previa) las genera internamente en la misma transaccion.
    public int IdSolicitud { get; set; }
    public int? IdCotizacion { get; set; }

    // "cotizacion" si nace de una cotizacion aprobada; "directo" si se registro en el taller.
    public string Origen { get; set; } = "cotizacion";
    public int IdEmpleadoResponsable { get; set; }
    public DateTime FechaPedido { get; set; }
    public DateTime FechaEntregaEstimada { get; set; }
    public DateTime? FechaEntregaReal { get; set; }
    public string ModalidadEntrega { get; set; } = "retiro_local";
    public bool RequiereInstalacion { get; set; } = false;
    public string? DireccionInstalacion { get; set; }
    public decimal CostoInstalacionAdicional { get; set; }
    public decimal MontoBaseTrabajo { get; set; }
    public decimal MontoTotalFinal { get; set; }
    public string EstadoPedido { get; set; } = "aceptado";

    // Pesos chilenos, sin decimales. Es la suma de los Pagos del pedido: la mantiene el trigger de la
    // tabla pagos, y EF nunca la escribe (se registra un Pago, no se edita este total).
    public int MontoAbonado { get; set; }
    public int SaldoPendiente { get; set; }
    public string EstadoPago { get; set; } = "no_pagado";

    // Opcional: un cliente habitual puede pagar varios pedidos con una sola factura a fin de mes.
    public string? NumeroFactura { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    // Fase 8: un pedido anulado guarda su motivo y su fecha (ck_pedidos_anulacion). El borrado es logico.
    public string? MotivoAnulacion { get; set; }
    public DateTimeOffset? FechaAnulacion { get; set; }
    public bool EliminadoLogico { get; set; }
    public DateTimeOffset? FechaEliminacionLogica { get; set; }

    public virtual Solicitud? Solicitud { get; set; }
    public virtual Cotizacion? Cotizacion { get; set; }
    public virtual Empleado? EmpleadoResponsable { get; set; }

    // Historial de abonos y pagos, con fecha, medio y quien lo registro.
    [JsonIgnore]
    public virtual ICollection<Pago> Pagos { get; set; } = new List<Pago>();

    // Fase 8: cada cambio de estado del pedido, tambien su creacion.
    [JsonIgnore]
    public virtual ICollection<PedidoHistorial> Historial { get; set; } = new List<PedidoHistorial>();
}
