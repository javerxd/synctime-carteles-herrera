using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using LetrerosHerreraSYNCTIMEapi.Negocio.Cotizaciones;
using LetrerosHerreraSYNCTIMEapi.Negocio.Pedidos;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 8: reglas de cotizaciones, pedidos y abonos, sin base de datos (RFS05, RFS06, RFU12-RFU15, E06-E09, E25).
public class CotizacionesYPedidosTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 14);

    // ── Cotizacion: lineas, montos y total ──

    private static EntradaMaterial Linea(decimal cantidad = 2, decimal costo = 500, int idMaterial = 1, string? medida = "2 x 1") =>
        new(idMaterial, medida, cantidad, costo);

    private static EntradaCotizacion Cotizacion(decimal manoObra = 1000, decimal otros = 0, string? observaciones = null, params EntradaMaterial[] lineas) =>
        new(lineas, manoObra, otros, observaciones);

    [Fact]
    public void UnaCotizacionValida_NoTieneErrores()
    {
        Assert.Empty(ValidacionCotizacion.Validar(Cotizacion(1000, 500, "Incluye diseño", Linea(), Linea(1.25m, 7200))));
        // Sin materiales tambien vale, si el total es mayor que cero.
        Assert.Empty(ValidacionCotizacion.Validar(Cotizacion(1000)));
    }

    [Fact]
    public void LosErrores_VanPorCampoConElIndiceDeLaLinea()
    {
        var errores = ValidacionCotizacion.Validar(Cotizacion(10.5m, -1, new string('x', 2001),
            Linea(cantidad: 0), Linea(cantidad: 1.234m), Linea(costo: 99.5m), Linea(idMaterial: 0), Linea(medida: new string('m', 101)), Linea(cantidad: 100_001), Linea(costo: 100_000_001)));

        Assert.Equal(
            ["costoManoObra", "materiales[0].cantidad", "materiales[1].cantidad", "materiales[2].costoUnitario", "materiales[3].idMaterial",
             "materiales[4].medidaAplicada", "materiales[5].cantidad", "materiales[6].costoUnitario", "observaciones", "otrosCostos"],
            errores.Keys.Order());
    }

    [Fact]
    public void MasDe30Lineas_EsUnError()
    {
        var lineas = Enumerable.Range(0, 31).Select(_ => Linea()).ToArray();
        Assert.True(ValidacionCotizacion.Validar(Cotizacion(1000, 0, null, lineas)).ContainsKey("materiales"));
        Assert.Empty(ValidacionCotizacion.Validar(Cotizacion(1000, 0, null, lineas[..30])));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100_000_000, 100_000_000)]
    public void ElTotal_DebeSerMayorQueCeroYHastaDosMilMillones(decimal manoObra, decimal otros)
    {
        // 100 millones de mano de obra + 100 millones de otros + 19 lineas de 100 millones = 2.100 millones.
        var lineas = manoObra == 0 ? [] : Enumerable.Range(0, 19).Select(_ => Linea(1, 100_000_000)).ToArray();
        var errores = ValidacionCotizacion.Validar(Cotizacion(manoObra, otros, null, lineas));
        Assert.True(errores.ContainsKey("total"));
    }

    [Fact]
    public void ElTotal_SumaSubtotalesManoDeObraYOtrosCostos()
    {
        var entrada = Cotizacion(1000, 250, null, Linea(2, 500), Linea(1.25m, 7200));

        Assert.Equal(1000m, ValidacionCotizacion.Subtotal(Linea(2, 500)));
        Assert.Equal(9000m, ValidacionCotizacion.Subtotal(Linea(1.25m, 7200)));
        Assert.Equal(11_250m, ValidacionCotizacion.Total(entrada));
    }

    [Theory]
    [InlineData("1000.49", 1000)]
    [InlineData("1000.50", 1001)]
    [InlineData("1000.51", 1001)]
    [InlineData("2500", 2500)]
    [InlineData("0.5", 1)]
    public void ElTotalRedondeado_EsAlPesoConLaMitadHaciaArriba(string total, int esperado) =>
        Assert.Equal(esperado, ValidacionCotizacion.Redondear(decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture)));

    // ── Cotizacion: respuesta ──

    [Theory]
    [InlineData("aprobada", "telefono", null)]
    [InlineData("rechazada", "whatsapp", "Muy caro")]
    [InlineData("rechazada", "presencial", null)]
    public void UnaRespuestaDelPersonal_Valida(string decision, string via, string? motivo) =>
        Assert.Empty(ValidacionCotizacion.ValidarRespuesta(decision, via, motivo, delPersonal: true));

    [Fact]
    public void UnaRespuestaInvalida_DiceQueCampoFalla()
    {
        Assert.Equal(["decision", "motivo", "via"],
            ValidacionCotizacion.ValidarRespuesta("quizas", "correo", new string('x', 501), delPersonal: true).Keys.Order());
        // El personal no puede decir que respondio el cliente desde la web.
        Assert.True(ValidacionCotizacion.ValidarRespuesta("aprobada", "cliente_web", null, delPersonal: true).ContainsKey("via"));
        // El cliente no indica via: siempre es la web.
        Assert.Empty(ValidacionCotizacion.ValidarRespuesta("rechazada", null, "Lo pensaré", delPersonal: false));
    }

    [Theory]
    [InlineData("pendiente", true)]
    [InlineData("rechazada", true)]
    [InlineData("aprobada", false)]
    public void SoloSeEliminaUnaPendienteOUnaRechazada(string estado, bool eliminable) =>
        Assert.Equal(eliminable, EstadosCotizacion.PuedeEliminarse(estado));

    [Theory]
    [InlineData("Marta Fuentes", "Marta")]
    [InlineData("  Pablo  Rojas ", "Pablo")]
    [InlineData("Daniela", "Daniela")]
    public void ElClienteVe_SoloElNombreDePilaDeQuienCotizo(string nombre, string esperado) =>
        Assert.Equal(esperado, FichaCotizacion.NombreDePila(nombre));

    [Fact]
    public void LaFichaDelCliente_NoTraeElIdDelEmpleado()
    {
        var ficha = Ficha() with
        {
            Respuesta = new RespuestaCotizacion(new DateTime(2026, 11, 14, 11, 0, 0), "telefono", null, new PersonaDelTaller(null, "Pablo Rojas"))
        };

        var delCliente = ficha.ParaElCliente();

        Assert.Null(delCliente.ElaboradaPor.IdEmpleado);
        Assert.Equal("Marta", delCliente.ElaboradaPor.Nombre);
        Assert.Equal("Pablo", delCliente.Respuesta!.RegistradaPor!.Nombre);
        Assert.Equal(7, ficha.ElaboradaPor.IdEmpleado);
    }

    private static FichaCotizacion Ficha() => new(
        45, "#45", 123, "#123", new DateTime(2026, 11, 14, 10, 32, 0), "pendiente",
        new DatosCliente(9, "Ana", "Rojas", "912345678", "ana@correo.cl"),
        [new ItemCotizacion("Letrero luminoso", "Con logo", 1000, 2000, 2, 4m)],
        [new MaterialCotizacion(1, "Madera", "m2", "2 x 1", 1.5m, 12_500, 18_750m)],
        18_750m, 100_000, 5_000, 123_750.5m, 123_751, "Incluye instalación <b>gratis</b>",
        new PersonaDelTaller(7, "Marta Fuentes"), null, null, false, [1, 2]);

    // ── Correo «Tu cotizacion #N esta lista» ──

    [Fact]
    public void ElCorreoDeCotizacionLista_LlevaElTotalLosItemsYElBoton()
    {
        var mensaje = CotizacionLista.Componer(Ficha(), "ana@correo.cl", "contacto@taller.cl", "https://synctime.cl/");

        Assert.Equal(TiposCorreo.CotizacionLista, mensaje.Tipo);
        Assert.Equal("Tu cotización #45 está lista", mensaje.Asunto);
        Assert.Equal(["ana@correo.cl"], mensaje.Para);
        Assert.Equal("contacto@taller.cl", mensaje.ResponderA);
        Assert.Equal("cotizacion:45", mensaje.Referencia);
        foreach (var parte in new[] { mensaje.Texto, mensaje.Html })
        {
            Assert.Contains("#123", parte);
            Assert.Contains("$123.751", parte);
            Assert.Contains("2 × Letrero luminoso", parte);
            Assert.Contains("Puedes aprobarla o rechazarla desde tu cuenta", parte);
            Assert.Contains("Ver cotización", parte);
        }
        Assert.Contains("https://synctime.cl/cuenta/solicitudes/123", mensaje.Html);
        Assert.Contains("https://synctime.cl/cuenta/solicitudes/123", mensaje.Texto);
        Assert.Contains("no respondas", mensaje.Texto);
        // Las observaciones las escribe el taller, pero igual se escapan: nunca llega HTML crudo.
        Assert.DoesNotContain("<b>gratis</b>", mensaje.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;gratis&lt;/b&gt;", mensaje.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    public void SinUnaDireccionDelSitioValida_ElCorreoSaleSinBoton(string? url)
    {
        var mensaje = CotizacionLista.Componer(Ficha(), "ana@correo.cl", "contacto@taller.cl", url);
        Assert.DoesNotContain("Ver cotización", mensaje.Html, StringComparison.Ordinal);
    }

    // ── Pedido: estados y transiciones ──

    [Theory]
    [InlineData("aceptado", "en_proceso")]
    [InlineData("en_proceso", "terminado")]
    [InlineData("terminado", "entregado")]
    [InlineData("en_proceso", "aceptado")]
    [InlineData("terminado", "en_proceso")]
    [InlineData("aceptado", "anulado")]
    [InlineData("en_proceso", "anulado")]
    [InlineData("terminado", "anulado")]
    public void LasTransicionesDelDiagrama_SonValidas(string desde, string hacia)
    {
        Assert.True(TransicionesPedido.EsValida(desde, hacia));
        Assert.Null(TransicionesPedido.Explicar(desde, hacia));
    }

    [Theory]
    [InlineData("aceptado", "terminado")]
    [InlineData("aceptado", "entregado")]
    [InlineData("en_proceso", "entregado")]
    [InlineData("terminado", "aceptado")]
    [InlineData("entregado", "terminado")]
    [InlineData("entregado", "anulado")]
    [InlineData("anulado", "aceptado")]
    [InlineData("en_proceso", "en_proceso")]
    public void LasDemasTransiciones_SeRechazanConUnaExplicacion(string desde, string hacia)
    {
        Assert.False(TransicionesPedido.EsValida(desde, hacia));
        Assert.False(string.IsNullOrWhiteSpace(TransicionesPedido.Explicar(desde, hacia)));
    }

    [Fact]
    public void LasTransicionesPosibles_DependenDelEstado()
    {
        Assert.Equal(["en_proceso", "anulado"], TransicionesPedido.Posibles("aceptado"));
        Assert.Equal(["terminado", "aceptado", "anulado"], TransicionesPedido.Posibles("en_proceso"));
        Assert.Equal(["entregado", "en_proceso", "anulado"], TransicionesPedido.Posibles("terminado"));
        Assert.Empty(TransicionesPedido.Posibles("entregado"));
        Assert.Empty(TransicionesPedido.Posibles("anulado"));
    }

    [Theory]
    [InlineData("entregado", "pagado_total", true)]
    [InlineData("entregado", "abonado", false)]
    [InlineData("terminado", "pagado_total", false)]
    [InlineData("anulado", "pagado_total", false)]
    public void ElCicloSeCierra_SoloEntregadoYPagado(string estadoPedido, string estadoPago, bool cerrado) =>
        Assert.Equal(cerrado, ReglasPedido.CicloCerrado(estadoPedido, estadoPago));

    // ── Pedido: datos (E08) ──

    private static EntradaDatosPedido Datos(string modalidad = "retiro_local", bool? instalacion = false, string? direccion = null,
        decimal costo = 0, DateOnly? fecha = null, int responsable = 1, string? factura = null) =>
        new(responsable, fecha ?? Hoy.AddDays(7), modalidad, instalacion, direccion, costo, factura);

    [Fact]
    public void UnosDatosValidos_NoTienenErrores()
    {
        Assert.Empty(ReglasPedido.ValidarDatos(Datos(), Hoy));
        Assert.Empty(ReglasPedido.ValidarDatos(Datos("entrega_domicilio", false, "Av. Alemania 100"), Hoy));
        Assert.Empty(ReglasPedido.ValidarDatos(Datos("instalacion_terreno", true, "Av. Alemania 100", 35_000, Hoy), Hoy));
        Assert.Empty(ReglasPedido.ValidarDatos(Datos(fecha: Hoy.AddYears(2)), Hoy));
    }

    [Fact]
    public void LosDatosInvalidos_DicenQueCampoFalla()
    {
        Assert.Equal(["fechaEntregaEstimada"], ReglasPedido.ValidarDatos(Datos(fecha: Hoy.AddDays(-1)), Hoy).Keys);
        Assert.Equal(["fechaEntregaEstimada"], ReglasPedido.ValidarDatos(Datos(fecha: Hoy.AddYears(2).AddDays(1)), Hoy).Keys);
        Assert.Equal(["modalidadEntrega"], ReglasPedido.ValidarDatos(Datos("despacho"), Hoy).Keys);
        Assert.Equal(["direccionInstalacion"], ReglasPedido.ValidarDatos(Datos("entrega_domicilio"), Hoy).Keys);
        Assert.Equal(["direccionInstalacion"], ReglasPedido.ValidarDatos(Datos("entrega_domicilio", direccion: new string('d', 301)), Hoy).Keys);
        Assert.Equal(["requiereInstalacion"], ReglasPedido.ValidarDatos(Datos("instalacion_terreno", false, "Calle 1"), Hoy).Keys);
        Assert.Equal(["costoInstalacionAdicional"], ReglasPedido.ValidarDatos(Datos(costo: 1000), Hoy).Keys);
        Assert.Equal(["costoInstalacionAdicional"], ReglasPedido.ValidarDatos(Datos("instalacion_terreno", true, "Calle 1", 9_990.5m), Hoy).Keys);
        Assert.Equal(["idEmpleadoResponsable"], ReglasPedido.ValidarDatos(Datos(responsable: 0), Hoy).Keys);
        Assert.Equal(["numeroFactura"], ReglasPedido.ValidarDatos(Datos(factura: new string('f', 31)), Hoy).Keys);
        Assert.Equal(["fechaEntregaEstimada"], ReglasPedido.ValidarDatos(new EntradaDatosPedido(1, null, "retiro_local", false, null, 0, null), Hoy).Keys);
    }

    [Fact]
    public void AlEditar_SeAceptaLaFechaEstimadaQueYaTenia()
    {
        var pasada = Hoy.AddDays(-10);
        Assert.Empty(ReglasPedido.ValidarDatos(Datos(fecha: pasada), Hoy, fechaEstimadaActual: pasada));
        Assert.NotEmpty(ReglasPedido.ValidarDatos(Datos(fecha: pasada.AddDays(1)), Hoy, fechaEstimadaActual: pasada));
    }

    [Fact]
    public void LaDireccion_NoSeGuardaSiSeRetiraEnElLocal()
    {
        Assert.Null(ReglasPedido.DireccionAGuardar("retiro_local", "Av. Alemania 100"));
        Assert.Equal("Av. Alemania 100", ReglasPedido.DireccionAGuardar("entrega_domicilio", "  Av. Alemania 100 "));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(1500.5, false)]
    [InlineData(2_000_000_001, false)]
    public void ElMontoBase_EsEnteroYNoNegativo(double monto, bool valido) =>
        Assert.Equal(valido, ReglasPedido.ValidarMontoBase((decimal)monto, 0) is null);

    [Fact]
    public void LaFechaDeEntregaReal_NoEsFuturaNiAnteriorAlPedido()
    {
        var fechaPedido = Hoy.AddDays(-5);
        Assert.Null(ReglasPedido.ValidarFechaEntregaReal(Hoy, Hoy, fechaPedido));
        Assert.Null(ReglasPedido.ValidarFechaEntregaReal(fechaPedido, Hoy, fechaPedido));
        Assert.NotNull(ReglasPedido.ValidarFechaEntregaReal(Hoy.AddDays(1), Hoy, fechaPedido));
        Assert.NotNull(ReglasPedido.ValidarFechaEntregaReal(fechaPedido.AddDays(-1), Hoy, fechaPedido));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("corto", false)]
    [InlineData("El cliente desistió", true)]
    public void AnularUnPedido_PideUnMotivoDe10A500(string? motivo, bool valido) =>
        Assert.Equal(valido, ReglasPedido.ValidarMotivoAnulacion(motivo) is null);

    // Revision de ECC: cuenta cualquier abono, vigente o anulado.
    [Fact]
    public void AnularUnPedidoConAbonos_EsSoloDeGestion()
    {
        Assert.True(ReglasPedido.AnularExigeGestion(tieneAbonos: true));
        Assert.False(ReglasPedido.AnularExigeGestion(tieneAbonos: false));
    }

    // Revision de ECC: un monto que no vino es un error del campo, nunca un 0.
    [Fact]
    public void LosMontosQueNoVienen_SonErroresDelCampo()
    {
        var errores = ValidacionCotizacion.Validar(new EntradaCotizacion([new EntradaMaterial(1, null, null, null)], null, null, null));
        Assert.Equal(["costoManoObra", "materiales[0].cantidad", "materiales[0].costoUnitario", "otrosCostos"], errores.Keys.Order());

        Assert.Equal(["costoInstalacionAdicional"],
            ReglasPedido.ValidarDatos(new EntradaDatosPedido(1, Hoy.AddDays(1), "retiro_local", false, null, null, null), Hoy).Keys);
        Assert.NotNull(ReglasPedido.ValidarMontoBase(null, 0));
    }

    [Fact]
    public void ElRangoDelCalendario_SeValida()
    {
        Assert.Null(ReglasPedido.ValidarRango(Hoy, Hoy.AddDays(61)));
        Assert.NotNull(ReglasPedido.ValidarRango(Hoy, Hoy.AddDays(62)));
        Assert.NotNull(ReglasPedido.ValidarRango(Hoy, Hoy.AddDays(-1)));
        Assert.NotNull(ReglasPedido.ValidarRango(null, Hoy));
        // El ultimo dia posible no tiene dia siguiente: 400 y no un error del servidor.
        Assert.NotNull(ReglasPedido.ValidarRango(DateOnly.MaxValue.AddDays(-3), DateOnly.MaxValue));
    }

    // Revision de ECC: el trigger de pagos no deja reactivar un abono anulado (error 50080 de SQL Server).
    [Fact]
    public void ReactivarUnAbonoAnulado_EsUn409()
    {
        var problema = LetrerosHerreraSYNCTIMEapi.Middleware.ApiExceptionHandler.TraducirSql(50080, "Un abono anulado no se puede reactivar.");

        Assert.NotNull(problema);
        Assert.Equal(409, problema!.Status);
        Assert.Equal("abono_anulado", problema.Extensions["codigo"]);
        Assert.Equal("base_de_datos", problema.Extensions["nivel"]);
    }

    // ── Abonos (E25) ──

    private static EntradaAbono Abono(decimal monto = 10_000, DateOnly? fecha = null, string? medio = "efectivo", string? observacion = null, string? factura = null) =>
        new(monto, fecha, medio, observacion, factura);

    [Fact]
    public void UnAbonoValido_NoTieneErrores()
    {
        Assert.Empty(ReglasPedido.ValidarAbono(Abono(), Hoy));
        Assert.Empty(ReglasPedido.ValidarAbono(Abono(fecha: Hoy.AddDays(-365), medio: "transferencia", observacion: "Transferencia 4471", factura: "F-1"), Hoy));
    }

    [Fact]
    public void UnAbonoInvalido_DiceQueCampoFalla()
    {
        Assert.Equal(["monto"], ReglasPedido.ValidarAbono(Abono(0), Hoy).Keys);
        Assert.Equal(["monto"], ReglasPedido.ValidarAbono(Abono(-5), Hoy).Keys);
        Assert.Equal(["monto"], ReglasPedido.ValidarAbono(Abono(100.5m), Hoy).Keys);
        Assert.Equal(["fechaPago"], ReglasPedido.ValidarAbono(Abono(fecha: Hoy.AddDays(1)), Hoy).Keys);
        Assert.Equal(["fechaPago"], ReglasPedido.ValidarAbono(Abono(fecha: Hoy.AddDays(-366)), Hoy).Keys);
        Assert.Equal(["medioPago"], ReglasPedido.ValidarAbono(Abono(medio: "bitcoin"), Hoy).Keys);
        Assert.Equal(["observacion"], ReglasPedido.ValidarAbono(Abono(observacion: new string('o', 301)), Hoy).Keys);
        Assert.Equal(["numeroFactura"], ReglasPedido.ValidarAbono(Abono(factura: new string('f', 31)), Hoy).Keys);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("Error", false)]
    [InlineData("Se ingresó dos veces", true)]
    public void AnularUnAbono_PideUnMotivoDe10A300(string? motivo, bool valido) =>
        Assert.Equal(valido, ReglasPedido.ValidarMotivoAnulacionAbono(motivo) is null);

    [Fact]
    public void QuienPuedeAnularUnAbono()
    {
        var registro = new DateTime(2026, 11, 14, 10, 0, 0);
        // Gestion: cualquiera, aunque sea de otro y antiguo.
        Assert.True(ReglasPedido.PuedeAnularAbono(esGestion: true, idEmpleado: 1, idRegistra: 2, registro, registro.AddDays(30)));
        // El resto: solo los propios de las ultimas 24 horas.
        Assert.True(ReglasPedido.PuedeAnularAbono(false, 2, 2, registro, registro.AddHours(24)));
        Assert.False(ReglasPedido.PuedeAnularAbono(false, 2, 2, registro, registro.AddHours(24).AddMinutes(1)));
        Assert.False(ReglasPedido.PuedeAnularAbono(false, 1, 2, registro, registro.AddMinutes(5)));
    }

    // ── Formato de pesos ──

    [Theory]
    [InlineData(0, "$0")]
    [InlineData(1500, "$1.500")]
    [InlineData(2_000_000_000, "$2.000.000.000")]
    public void LosPesos_SeEscribenComoEnChile(long monto, string esperado) =>
        Assert.Equal(esperado, FormatoChile.Pesos(monto));
}
