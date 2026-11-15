using System.Text.Json.Serialization;

namespace LetrerosHerreraSYNCTIMEapi.Models;

// Un abono o pago de un pedido (historial de abonos, aprobado el 12-11-2026). Un pedido puede tener
// varios: un abono inicial, otros parciales y el pago final. El trigger trg_pagos_recalcular_monto_abonado
// mantiene pedidos.monto_abonado igual a la suma de sus pagos, y la restriccion del pedido impide que
// esa suma supere el monto final.
public class Pago
{
    public int IdPago { get; set; }
    public int IdPedido { get; set; }

    // Pesos chilenos, sin decimales, mayor que cero.
    public int Monto { get; set; }

    // Dia en que el cliente pago (puede ser anterior al registro). Si no se indica, hoy.
    public DateOnly FechaPago { get; set; }

    // efectivo, transferencia, tarjeta_debito, tarjeta_credito, cheque u otro.
    public string MedioPago { get; set; } = "efectivo";
    public string? Observacion { get; set; }

    // Quien lo registro y cuando (hora de Chile).
    public int IdEmpleadoRegistra { get; set; }
    public DateTime FechaRegistro { get; set; }

    // Fase 8 (decision 2 del 14-11-2026): un abono mal ingresado no se edita ni se borra, se anula con
    // motivo. Queda en el historial con quien y cuando, y el trigger deja de sumarlo.
    public bool Anulado { get; set; }
    public string? MotivoAnulacion { get; set; }
    public int? IdEmpleadoAnula { get; set; }
    public DateTimeOffset? FechaAnulacion { get; set; }

    [JsonIgnore]
    public virtual Pedido? Pedido { get; set; }

    [JsonIgnore]
    public virtual Empleado? EmpleadoRegistra { get; set; }

    [JsonIgnore]
    public virtual Empleado? EmpleadoAnula { get; set; }
}
