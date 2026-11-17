using LetrerosHerreraSYNCTIMEapi.Datos.Indicadores;
using LetrerosHerreraSYNCTIMEapi.Negocio.Indicadores;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 12B: reglas puras de los indicadores (RFS09, RFU27, E21, S10 a S14, S24), sin base de datos. Las cifras esperadas
// son las mismas que el frontend de la 12A calculaba con su adaptador de demostracion (hoy: lunes 16-11-2026).
public class IndicadoresTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 16);

    private static FiltroIndicadores Filtro(params (string, string)[] parametros)
    {
        var (filtro, errores) = ReglasIndicadores.Validar(parametros.ToDictionary(p => p.Item1, p => (string?)p.Item2), conPagina: true);
        Assert.Empty(errores);
        return filtro!;
    }

    // ── Períodos y comparación ──

    [Fact]
    public void ElMesEnCurso_SeCompara_ConElMesAnteriorCompleto()
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(Filtro(), Hoy);

        Assert.Equal(("mes", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "noviembre de 2026"), (periodo.Tipo, periodo.Desde, periodo.Hasta, periodo.Etiqueta));
        var anterior = ReglasIndicadores.PeriodoComparacion(periodo, "anterior")!;
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), "octubre de 2026"), (anterior.Desde, anterior.Hasta, anterior.Etiqueta));
    }

    [Fact]
    public void EnEnero_ElMesAnterior_EsDiciembreDelAnioPasado()
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(Filtro(("anio", "2026"), ("mes", "1")), Hoy);

        var anterior = ReglasIndicadores.PeriodoComparacion(periodo, "anterior")!;

        Assert.Equal((new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 31)), (anterior.Desde, anterior.Hasta));
    }

    [Fact]
    public void LaSemana_VaDeLunesADomingo_YSeCompara_ConLaSemanaAnterior()
    {
        var semana = ReglasIndicadores.ResolverPeriodo(Filtro(("periodo", "semana")), Hoy);
        var anterior = ReglasIndicadores.PeriodoComparacion(semana, "anterior")!;

        Assert.Equal((new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22)), (semana.Desde, semana.Hasta));
        Assert.Equal((new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)), (anterior.Desde, anterior.Hasta));
        // Un domingo pertenece a la semana que empezo el lunes anterior.
        var domingo = ReglasIndicadores.ResolverPeriodo(Filtro(("periodo", "semana")), new DateOnly(2026, 11, 22));
        Assert.Equal(new DateOnly(2026, 11, 16), domingo.Desde);
    }

    [Fact]
    public void ElAnio_SeCompara_ConElAnioAnterior_YConElMismoPeriodoDelAnioPasado()
    {
        var anio = ReglasIndicadores.ResolverPeriodo(Filtro(("periodo", "anio")), Hoy);
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), (anio.Desde, anio.Hasta));
        Assert.Equal(new DateOnly(2025, 1, 1), ReglasIndicadores.PeriodoComparacion(anio, "anterior")!.Desde);

        var mes = ReglasIndicadores.ResolverPeriodo(Filtro(), Hoy);
        var delAnioPasado = ReglasIndicadores.PeriodoComparacion(mes, "anio_anterior")!;
        Assert.Equal((new DateOnly(2025, 11, 1), new DateOnly(2025, 11, 30)), (delAnioPasado.Desde, delAnioPasado.Hasta));
        Assert.Null(ReglasIndicadores.PeriodoComparacion(mes, "ninguno"));
    }

    [Fact]
    public void ElRangoPropio_GanaATodo_YSeComparaConLosMismosDiasAnteriores()
    {
        var rango = ReglasIndicadores.ResolverPeriodo(Filtro(("anio", "2026"), ("desde", "2026-11-01"), ("hasta", "2026-11-10")), Hoy);

        Assert.Equal(("rango", "del 01-11-2026 al 10-11-2026"), (rango.Tipo, rango.Etiqueta));
        var anterior = ReglasIndicadores.PeriodoComparacion(rango, "anterior")!;
        Assert.Equal((new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 31)), (anterior.Desde, anterior.Hasta));
    }

    [Fact]
    public void ElAcceso_PorAnioYMes_GanaAlPeriodoRelativo()
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(Filtro(("periodo", "semana"), ("anio", "2026"), ("mes", "1")), Hoy);

        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)), (periodo.Desde, periodo.Hasta));
    }

    [Theory]
    [InlineData("semana", "dia", 7)]
    [InlineData("mes", "semana", 5)]
    [InlineData("anio", "mes", 12)]
    public void LaEvolucion_SeAgrupaSegunElPeriodo(string tipo, string granularidad, int tramos)
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(Filtro(("periodo", tipo)), Hoy);

        Assert.Equal(granularidad, ReglasIndicadores.Granularidad(periodo));
        Assert.Equal(tramos, ReglasIndicadores.Tramos(periodo, granularidad).Count);
    }

    [Fact]
    public void ElRangoPropio_SeAgrupaPorDiaHasta14DiasPorSemanaHasta92_YDespuesPorMes()
    {
        string Para(string hasta) => ReglasIndicadores.Granularidad(ReglasIndicadores.ResolverPeriodo(Filtro(("desde", "2026-01-01"), ("hasta", hasta)), Hoy));

        Assert.Equal("dia", Para("2026-01-14"));
        Assert.Equal("semana", Para("2026-01-15"));
        Assert.Equal("semana", Para("2026-04-02"));   // 92 dias
        Assert.Equal("mes", Para("2026-04-03"));
    }

    [Fact]
    public void LosTramosDelMes_SonSemanasDesdeElDia1_ConElUltimoMasCorto()
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(Filtro(), Hoy);

        var tramos = ReglasIndicadores.Tramos(periodo, "semana");

        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), "01-11"), (tramos[0].Desde, tramos[0].Hasta, tramos[0].Etiqueta));
        Assert.Equal((new DateOnly(2026, 11, 29), new DateOnly(2026, 11, 30)), (tramos[4].Desde, tramos[4].Hasta));
    }

    [Fact]
    public void ElIndiceDeUnaFecha_EnSusTramos_CoincideConElTramoQueLaContiene()
    {
        var periodo = ReglasIndicadores.ResolverPeriodo(Filtro(("periodo", "anio")), Hoy);

        Assert.Equal(0, ReglasIndicadores.IndiceDeTramo(periodo, "mes", new DateOnly(2026, 1, 31)));
        Assert.Equal(10, ReglasIndicadores.IndiceDeTramo(periodo, "mes", new DateOnly(2026, 11, 16)));
        var mes = ReglasIndicadores.ResolverPeriodo(Filtro(), Hoy);
        Assert.Equal(1, ReglasIndicadores.IndiceDeTramo(mes, "semana", new DateOnly(2026, 11, 8)));
        Assert.Equal(4, ReglasIndicadores.IndiceDeTramo(mes, "semana", new DateOnly(2026, 11, 30)));
    }

    // ── Filtros ──

    [Theory]
    [InlineData("0001-01-01", "2026-01-01")]
    [InlineData("2026-01-01", "9999-12-31")]
    [InlineData("2019-12-31", "2020-01-31")]
    [InlineData("2020-01-01", "2030-12-31")]   // 11 años: pasa de los 3.660 dias
    public void UnRangoPropioDesmesurado_SeRechaza_ConUn400_NoUnError(string desde, string hasta)
    {
        var (filtro, errores) = ReglasIndicadores.Validar(new Dictionary<string, string?> { ["desde"] = desde, ["hasta"] = hasta });

        Assert.Null(filtro);
        Assert.Contains("hasta", errores.Keys);
    }

    [Fact]
    public void UnRangoPropioLargoPeroRazonable_SeAcepta_YSuComparacionSeCalcula()
    {
        var filtro = Filtro(("desde", "2020-01-01"), ("hasta", "2029-12-31"));

        var periodo = ReglasIndicadores.ResolverPeriodo(filtro, Hoy);

        Assert.NotNull(ReglasIndicadores.PeriodoComparacion(periodo, "anterior"));
        Assert.True(ReglasIndicadores.Tramos(periodo, ReglasIndicadores.Granularidad(periodo)).Count <= 120);
    }

    [Fact]
    public void ElReparto_ConservaLaSuma_DeLosIngresos()
    {
        // 100 entre tres categorias iguales: 33,33 cada una. Redondeadas por separado sumarian 99.
        var reparto = ConsultasIndicadores.Repartir([33.3333m, 33.3333m, 33.3334m]);

        Assert.Equal(100m, reparto.Sum());
        Assert.Equal([34m, 33m, 33m], reparto.OrderByDescending(x => x).ToList());
        Assert.Equal([120000m, 60000m], ConsultasIndicadores.Repartir([120000m, 60000m]));
        Assert.Empty(ConsultasIndicadores.Repartir([]));
    }

    [Fact]
    public void LosFiltros_TienenSusValoresPorOmision()
    {
        var f = Filtro();

        Assert.Equal(("mes", "anterior", "periodo"), (f.Periodo, f.Comparar, f.AlcanceSaldo));
        Assert.Empty(f.EstadosPedido);
        Assert.Equal((1, 10), (f.Pagina, f.Tamano));
    }

    [Fact]
    public void PorCobrar_SeExpande_A_NoPagadoYAbonado()
    {
        Assert.Equal(["no_pagado", "abonado"], Filtro(("estadoPago", "por_cobrar")).EstadosPago);
        Assert.Equal(["abonado", "pagado_total"], Filtro(("estadoPago", "abonado,pagado_total")).EstadosPago);
        Assert.Equal(["terminado", "entregado"], Filtro(("estadoPedido", "terminado,entregado")).EstadosPedido);
    }

    [Theory]
    [InlineData("periodo", "decada")]
    [InlineData("comparar", "siempre")]
    [InlineData("estadoPedido", "anulado")]           // los anulados nunca cuentan
    [InlineData("estadoPedido", "terminado,terminado")]
    [InlineData("estadoPago", "por_cobrar,abonado")]
    [InlineData("estadoPago", "debe")]
    [InlineData("origen", "web")]
    [InlineData("categoria", "0")]
    [InlineData("categoria", "x")]
    [InlineData("cliente", "-3")]
    [InlineData("alcanceSaldo", "todo")]
    [InlineData("anio", "1999")]
    [InlineData("anio", "2101")]
    [InlineData("mes", "13")]
    [InlineData("agrupar", "marca")]
    [InlineData("pagina", "0")]
    [InlineData("tamano", "101")]
    public void UnFiltroInvalido_SeRechaza_PorCampo(string campo, string valor)
    {
        var consulta = new Dictionary<string, string?> { [campo] = valor };
        if (campo == "mes")
            consulta["anio"] = "2026";   // el mes exige año: aqui falla por su valor, no por eso
        var (filtro, errores) = ReglasIndicadores.Validar(consulta, conPagina: true);

        Assert.Null(filtro);
        Assert.Contains(campo, errores.Keys);
    }

    [Fact]
    public void ElMes_ExigeElAnio_Y_ElRango_ExigeLasDosFechasEnOrden()
    {
        Assert.Contains("mes", ReglasIndicadores.Validar(new Dictionary<string, string?> { ["mes"] = "3" }, true).Errores.Keys);
        Assert.Contains("hasta", ReglasIndicadores.Validar(new Dictionary<string, string?> { ["desde"] = "2026-11-10" }, true).Errores.Keys);
        Assert.Contains("hasta", ReglasIndicadores.Validar(new Dictionary<string, string?> { ["desde"] = "2026-11-10", ["hasta"] = "2026-11-01" }, true).Errores.Keys);
        Assert.Contains("desde", ReglasIndicadores.Validar(new Dictionary<string, string?> { ["desde"] = "10-11-2026", ["hasta"] = "2026-11-12" }, true).Errores.Keys);
    }

    [Fact]
    public void VariosErrores_SeDevuelvenJuntos_PorCampo()
    {
        var (_, errores) = ReglasIndicadores.Validar(new Dictionary<string, string?> { ["periodo"] = "x", ["origen"] = "y", ["tamano"] = "0" }, true);

        Assert.Equal(["origen", "periodo", "tamano"], errores.Keys.Order());
    }

    // ── Variaciones y porcentajes (division por cero incluida) ──

    [Theory]
    [InlineData(180000, 120000, 50)]
    [InlineData(3, 1, 200)]
    [InlineData(80000, 120000, -33.3)]
    [InlineData(10, 10, 0)]
    public void LaVariacionPorcentual_SeRedondeaAUnDecimal(double valor, double anterior, double esperada)
    {
        Assert.Equal((decimal)esperada, ReglasIndicadores.VariacionPorcentual((decimal)valor, (decimal)anterior));
    }

    [Fact]
    public void SinBaseDeComparacion_LaVariacion_EsNula_NoInfinita()
    {
        Assert.Null(ReglasIndicadores.VariacionPorcentual(5, 0));
        Assert.Null(ReglasIndicadores.VariacionPorcentual(5, null));
        Assert.Null(ReglasIndicadores.VariacionPorcentual(null, 5));
        Assert.Null(ReglasIndicadores.VariacionPuntos(null, 50));
        Assert.Null(ReglasIndicadores.VariacionPuntos(50, null));
    }

    [Fact]
    public void LosPorcentajes_VarianEnPuntos()
    {
        Assert.Equal(-25m, ReglasIndicadores.VariacionPuntos(25m, 50m));
        Assert.Equal(27.8m, ReglasIndicadores.VariacionPuntos(50m, 22.2m));
    }

    [Theory]
    [InlineData(1, 4, 25)]
    [InlineData(2, 3, 66.7)]
    [InlineData(1, 3, 33.3)]
    public void UnPorcentaje_SeRedondeaAUnDecimal(int parte, int total, double esperado)
    {
        Assert.Equal((decimal)esperado, ReglasIndicadores.Porcentaje(parte, total));
    }

    [Fact]
    public void ElPorcentaje_ConDivisorCero_EsNulo()
    {
        Assert.Null(ReglasIndicadores.Porcentaje(0, 0));
        Assert.Null(ReglasIndicadores.Porcentaje(3, 0));
    }

    [Fact]
    public void ElTicketPromedio_EsIngresosSobreEntregados_RedondeadoAlPeso_YNuloSinEntregas()
    {
        Assert.Equal(60000, ReglasIndicadores.TicketPromedio(180000, 3));
        Assert.Equal(33333, ReglasIndicadores.TicketPromedio(100000, 3));
        Assert.Null(ReglasIndicadores.TicketPromedio(0, 0));
    }
}
