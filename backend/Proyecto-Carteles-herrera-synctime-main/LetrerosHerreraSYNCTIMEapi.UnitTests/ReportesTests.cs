using System.Text;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Reportes;
using LetrerosHerreraSYNCTIMEapi.Negocio.Reportes;
using LetrerosHerreraSYNCTIMEapi.Security;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Fase 13A (RFS10, RFU24 a RFU26, RFU36, RNFP18, E16, E20, S18, S24): reglas puras de los reportes y de sus exportaciones. Sin
// base de datos ni HTTP. Los archivos reales (Excel, PDF) se abren en ArchivosDeReportesTests.
public class ReportesTests
{
    private static Dictionary<string, string?> Consulta(params (string Clave, string? Valor)[] pares) =>
        pares.ToDictionary(p => p.Clave, p => p.Valor, StringComparer.Ordinal);

    // ── Filtros ──

    [Fact]
    public void SinFiltros_QuedanLosValoresPorOmision()
    {
        var (filtro, errores) = ReglasReportes.Validar(TiposReporte.Operativo, Consulta());

        Assert.Empty(errores);
        Assert.NotNull(filtro);
        Assert.Equal("mes", filtro.Indicadores.Periodo);
        Assert.Equal("anterior", filtro.Indicadores.Comparar);
        Assert.Empty(filtro.Canonico);
    }

    [Theory]
    [InlineData("desde", "2026-12-01", "hasta", "2026-11-01", "hasta")]          // rango invertido
    [InlineData("mes", "5", null, null, "mes")]                                   // mes sin año
    [InlineData("anio", "2019", null, null, "anio")]                              // año por debajo del rango
    [InlineData("anio", "2101", null, null, "anio")]                              // año por encima del rango
    [InlineData("mes", "13", "anio", "2026", "mes")]                              // mes fuera de 1 a 12
    [InlineData("estadoPedido", "enviado", null, null, "estadoPedido")]           // estado desconocido
    [InlineData("estadoPedido", "anulado", null, null, "estadoPedido")]           // «anulado» no se ofrece como filtro
    [InlineData("estadoPago", "regalado", null, null, "estadoPago")]              // pago desconocido
    [InlineData("cliente", "abc", null, null, "cliente")]                         // cliente no numerico
    [InlineData("desde", "01-11-2026", "hasta", "2026-11-30", "desde")]           // fecha con otro formato
    [InlineData("periodo", "decada", null, null, "periodo")]
    [InlineData("comparar", "ayer", null, null, "comparar")]
    public void UnFiltroInvalido_SeRechazaConElErrorPorCampo(string campo, string valor, string? campo2, string? valor2, string campoConError)
    {
        var pares = new List<(string, string?)> { (campo, valor) };
        if (campo2 is not null)
            pares.Add((campo2, valor2));

        var (filtro, errores) = ReglasReportes.Validar(TiposReporte.Gerencial, Consulta([.. pares]));

        Assert.Null(filtro);
        Assert.True(errores.ContainsKey(campoConError), $"Se esperaba un error en «{campoConError}»; hubo: {string.Join(", ", errores.Keys)}");
        Assert.All(errores.Values, mensajes => Assert.All(mensajes, m => Assert.False(string.IsNullOrWhiteSpace(m))));
    }

    [Fact]
    public void UnFiltroQueNoExiste_NoSeIgnoraEnSilencio()
    {
        var (filtro, errores) = ReglasReportes.Validar(TiposReporte.Operativo, Consulta(("estadoPedidoo", "entregado")));

        Assert.Null(filtro);
        Assert.Contains("estadoPedidoo", errores.Keys);
    }

    [Theory]
    [InlineData("operativo", "alcanceSaldo")]        // solo la cobranza del parametrizable lo usa
    [InlineData("operativo", "comparar")]            // solo el gerencial compara
    [InlineData("parametrizable", "comparar")]
    [InlineData("gerencial", "alcanceSaldo")]
    [InlineData("operativo", "categoria")]           // existe en el dashboard, no en los reportes
    [InlineData("operativo", "origen")]
    public void UnFiltroDeOtroReporte_SeRechaza(string reporte, string campo)
    {
        var valor = campo == "categoria" ? "1" : campo == "origen" ? "online" : campo == "comparar" ? "ninguno" : "vigente";
        var (filtro, errores) = ReglasReportes.Validar(reporte, Consulta((campo, valor)));

        Assert.Null(filtro);
        Assert.Contains(campo, errores.Keys);
    }

    [Fact]
    public void LosFiltrosPropios_DeCadaReporte_SeAceptan()
    {
        Assert.NotNull(ReglasReportes.Validar(TiposReporte.Gerencial, Consulta(("comparar", "anio_anterior"))).Filtro);
        Assert.NotNull(ReglasReportes.Validar(TiposReporte.Parametrizable, Consulta(("alcanceSaldo", "vigente"), ("estadoPago", "por_cobrar"))).Filtro);
        Assert.NotNull(ReglasReportes.Validar(TiposReporte.Operativo, Consulta(("desde", "2026-11-01"), ("hasta", "2026-11-20"), ("cliente", "7"), ("estadoPedido", "entregado,en_proceso"))).Filtro);
    }

    [Fact]
    public void PorCobrar_EsLoMismoQueNoPagadoYAbonado()
    {
        var (filtro, errores) = ReglasReportes.Validar(TiposReporte.Parametrizable, Consulta(("estadoPago", "por_cobrar")));

        Assert.Empty(errores);
        Assert.Equal(["no_pagado", "abonado"], filtro!.Indicadores.EstadosPago);
    }

    [Fact]
    public void ElFiltroGuardado_ConservaSoloLoPedido_SinEspaciosYEnOrdenFijo()
    {
        var (filtro, _) = ReglasReportes.Validar(TiposReporte.Parametrizable,
            Consulta(("cliente", " 12 "), ("anio", "2026"), ("mes", "11"), ("estadoPago", ""), ("estadoPedido", "entregado")));

        // Los vacios cuentan como ausentes; el orden no depende del orden en que llegaron.
        Assert.Equal(["anio", "mes", "estadoPedido", "cliente"], filtro!.Canonico.Keys);
        Assert.Equal("12", filtro.Canonico["cliente"]);
    }

    [Fact]
    public void ElFiltroGuardado_EsElValorNormalizado_NoElTextoCrudo()
    {
        var (filtro, errores) = ReglasReportes.Validar(TiposReporte.Operativo,
            Consulta(("anio", "002026"), ("mes", "0011"), ("cliente", "000012"), ("estadoPedido", "aceptado" + new string(' ', 2000) + ",entregado")));

        Assert.Empty(errores);
        Assert.Equal("2026", filtro!.Canonico["anio"]);
        Assert.Equal("11", filtro.Canonico["mes"]);
        Assert.Equal("12", filtro.Canonico["cliente"]);
        Assert.Equal("aceptado,entregado", filtro.Canonico["estadoPedido"]);
    }

    [Fact]
    public void ElFiltroGuardado_NoLlevaDatosPersonales_ElClienteVaPorSuNumero()
    {
        var (filtro, _) = ReglasReportes.Validar(TiposReporte.Operativo, Consulta(("cliente", "12")));

        Assert.All(filtro!.Canonico.Values, v => Assert.Matches(@"^[0-9a-z_,\-]+$", v));
    }

    [Fact]
    public void ElFiltroGuardado_AlValidarseDeNuevo_DaElMismoFiltro()
    {
        var (primero, _) = ReglasReportes.Validar(TiposReporte.Parametrizable,
            Consulta(("desde", "2026-11-01"), ("hasta", "2026-11-20"), ("estadoPago", "por_cobrar"), ("alcanceSaldo", "vigente")));
        var (segundo, errores) = ReglasReportes.Validar(TiposReporte.Parametrizable, primero!.Canonico.ToDictionary(p => p.Key, p => (string?)p.Value));

        Assert.Empty(errores);
        Assert.Equal(primero.Canonico, segundo!.Canonico);
        Assert.Equal(primero.Indicadores.Desde, segundo.Indicadores.Desde);
        Assert.Equal(primero.Indicadores.Hasta, segundo.Indicadores.Hasta);
        Assert.Equal(primero.Indicadores.EstadosPago, segundo.Indicadores.EstadosPago);
        Assert.Equal(primero.Indicadores.AlcanceSaldo, segundo.Indicadores.AlcanceSaldo);
    }

    [Fact]
    public void ElReporteQueNoExiste_NoTieneFiltros()
    {
        var (filtro, errores) = ReglasReportes.Validar("secreto", Consulta());

        Assert.Null(filtro);
        Assert.Contains("reporte", errores.Keys);
    }

    // ── Quien ve que ──

    [Theory]
    [InlineData("operativo", Roles.Dueno, true)]
    [InlineData("operativo", Roles.Administrador, true)]
    [InlineData("operativo", Roles.Disenador, true)]
    [InlineData("operativo", Roles.Operario, true)]
    [InlineData("operativo", Roles.Cliente, false)]
    [InlineData("gerencial", Roles.Dueno, true)]
    [InlineData("gerencial", Roles.Administrador, true)]
    [InlineData("gerencial", Roles.Disenador, false)]
    [InlineData("gerencial", Roles.Operario, false)]
    [InlineData("gerencial", Roles.Cliente, false)]
    [InlineData("parametrizable", Roles.Dueno, true)]
    [InlineData("parametrizable", Roles.Administrador, true)]
    [InlineData("parametrizable", Roles.Disenador, false)]
    [InlineData("parametrizable", Roles.Operario, false)]
    [InlineData("parametrizable", Roles.Cliente, false)]
    [InlineData("secreto", Roles.Dueno, false)]
    [InlineData("operativo", null, false)]
    public void CadaReporte_LoVeSoloQuienDebe(string reporte, string? rol, bool esperado) =>
        Assert.Equal(esperado, TiposReporte.PuedeVer(reporte, rol));

    // ── Formatos y nombres ──

    [Theory]
    [InlineData("operativo", "xlsx", "reporte-operativo-2026-11-20.xlsx")]
    [InlineData("gerencial", "pdf", "reporte-gerencial-2026-11-20.pdf")]
    [InlineData("parametrizable", "csv", "reporte-parametrizable-2026-11-20.csv")]
    public void ElNombreDelArchivo_EsElTipoYLaFecha_SinDatosPersonales(string reporte, string formato, string esperado) =>
        Assert.Equal(esperado, ReglasReportes.NombreArchivo(reporte, formato, new DateOnly(2026, 11, 20)));

    [Fact]
    public void LosFormatos_TienenSuTipoYSuExtension()
    {
        Assert.Equal(["xlsx", "csv", "pdf"], FormatosReporte.Todos);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FormatosReporte.TipoMime("xlsx"));
        Assert.Equal("text/csv; charset=utf-8", FormatosReporte.TipoMime("csv"));
        Assert.Equal("application/pdf", FormatosReporte.TipoMime("pdf"));
        Assert.False(FormatosReporte.Existe("docx"));
        Assert.False(FormatosReporte.Existe("../xlsx"));
        Assert.False(FormatosReporte.Existe(null));
    }

    // ── Tope de filas ──

    [Theory]
    [InlineData(0, false)]
    [InlineData(50_000, false)]
    [InlineData(50_001, true)]
    public void MasDeCincuentaMilFilas_SeRechazan(int filas, bool excede) =>
        Assert.Equal(excede, ReglasReportes.ExcedeElTope(filas, ReglasReportes.MaximoDeFilasPorOmision));

    [Fact]
    public void ElMensajeDelTope_PideAcotarElRango()
    {
        var mensaje = ReglasReportes.MensajeDelTope(50_000);
        Assert.Contains("acota", mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("50.000", mensaje);
    }

    // ── Costos y margen (gerencial) ──

    [Fact]
    public void UnPedidoConCotizacion_TieneCosto_ElDeMaterialesManoDeObraYOtros()
    {
        var costo = ReglasGerencial.CostoDe(tieneCotizacion: true, materiales: 20_000, manoObra: 30_000, otros: 5_000);

        Assert.NotNull(costo);
        Assert.Equal(55_000, costo.Total);
    }

    [Fact]
    public void UnPedidoSinCotizacion_NoTieneCosto_NoSeInventa()
    {
        Assert.Null(ReglasGerencial.CostoDe(tieneCotizacion: false, materiales: 0, manoObra: 0, otros: 0));
    }

    [Fact]
    public void ElMargen_SoloSeCalculaSobreLosPedidosConCosto()
    {
        // Ingresos de 300.000, de los cuales 100.000 no tienen costo registrado: el margen solo mira los 200.000 con costo,
        // y descuenta solo los materiales (ganancia bruta), no la mano de obra ni los otros costos.
        var margen = ReglasGerencial.Margen(ingresosConCosto: 200_000, materiales: 150_000);

        Assert.Equal(50_000, margen);
        Assert.Equal(25m, ReglasGerencial.MargenPorcentaje(200_000, 150_000));
    }

    [Fact]
    public void LosOtrosCostos_SonOpcionales_SinEllosElMargenNoCambia_ConEllosSeDescuentan()
    {
        Assert.Equal(50_000, ReglasGerencial.Margen(200_000, 150_000));
        Assert.Equal(50_000, ReglasGerencial.Margen(200_000, 150_000, otros: 0));
        Assert.Equal(40_000, ReglasGerencial.Margen(200_000, 150_000, otros: 10_000));
        Assert.Equal(20m, ReglasGerencial.MargenPorcentaje(200_000, 150_000, 10_000));
        Assert.Equal(25m, ReglasGerencial.MargenPorcentaje(200_000, 150_000));
    }

    [Fact]
    public void SinPedidosConCosto_NoHayMargenPorcentual_NuncaUnaDivisionPorCero()
    {
        Assert.Null(ReglasGerencial.MargenPorcentaje(0, 0));
        Assert.Equal(0, ReglasGerencial.Margen(0, 0));
    }

    [Fact]
    public void ElMargen_PuedeSerNegativo()
    {
        Assert.Equal(-20_000, ReglasGerencial.Margen(100_000, 120_000));
        Assert.Equal(-20m, ReglasGerencial.MargenPorcentaje(100_000, 120_000));
    }

    // ── Totales de una tabla ──

    private static TablaReporte TablaDePedidos(params object?[][] filas) => new("pedidos", "Pedidos",
        [
            new ColumnaReporte("estado", "Estado", TiposColumna.Texto),
            new ColumnaReporte("monto", "Monto", TiposColumna.Pesos, Suma: true),
            new ColumnaReporte("saldo", "Saldo", TiposColumna.Pesos, Suma: true)
        ], filas, Principal: true, Exclusion: new ExclusionDeTotales(0, "Anulado"));

    [Fact]
    public void LosTotales_SumanLasColumnasMarcadas_YDejanFueraLosAnulados()
    {
        var tabla = TablaDePedidos(["Entregado", 100_000, 0], ["Anulado", 50_000, 50_000], ["En proceso", 60_000, 40_000]);

        Assert.Equal([null, 160_000m, 40_000m], tabla.Totales);
    }

    [Fact]
    public void UnaTablaSinFilas_TieneTotalesEnCero_NoNulos()
    {
        Assert.Equal([null, 0m, 0m], TablaDePedidos().Totales);
    }

    [Fact]
    public void UnaCeldaVacia_NoSuma()
    {
        var tabla = TablaDePedidos(["Entregado", 100_000, null], ["En proceso", null, 40_000]);

        Assert.Equal([null, 100_000m, 40_000m], tabla.Totales);
    }

    // ── Formato de lo que se muestra ──

    [Fact]
    public void Fechas_DiaMesAnio()
    {
        Assert.Equal("20-11-2026", FormatoReporte.Fecha(new DateOnly(2026, 11, 20)));
        Assert.Equal("20-11-2026 14:35", FormatoReporte.FechaHora(new DateTime(2026, 11, 20, 14, 35, 59)));
    }

    [Theory]
    [InlineData(0, "$0")]
    [InlineData(999, "$999")]
    [InlineData(1_000, "$1.000")]
    [InlineData(1_234_567, "$1.234.567")]
    [InlineData(-5_000, "-$5.000")]
    public void Pesos_ConPuntoComoSeparadorDeMiles(int monto, string esperado) =>
        Assert.Equal(esperado, FormatoReporte.Pesos(monto));

    [Fact]
    public void Porcentaje_ConUnDecimalYComa() =>
        Assert.Equal("25,5 %", FormatoReporte.Porcentaje(25.5m));

    // ── CSV ──

    private static DocumentoReporte DocumentoConUnaTabla(params object?[][] filas) => new("parametrizable", "Reporte de prueba",
        new DateTimeOffset(2026, 11, 20, 14, 35, 0, TimeSpan.FromHours(-3)), [], [new TablaReporte("pedidos", "Pedidos",
        [
            new ColumnaReporte("cliente", "Cliente", TiposColumna.Texto),
            new ColumnaReporte("fecha", "Fecha", TiposColumna.Fecha),
            new ColumnaReporte("monto", "Monto", TiposColumna.Pesos, Suma: true, TextoNulo: "Sin costo")
        ], filas, Principal: true)], [], []);

    private static string[] LineasDelCsv(byte[] csv) => Encoding.UTF8.GetString(csv).TrimStart('﻿').Split("\r\n", StringSplitOptions.None);

    [Fact]
    public void ElCsv_EmpiezaConLaMarcaUtf8_ParaQueExcelRespeteLasTildes()
    {
        var csv = GeneradorCsv.Generar(DocumentoConUnaTabla(["Ñandú Peña", new DateOnly(2026, 11, 20), 1000]));

        Assert.Equal([0xEF, 0xBB, 0xBF], csv.Take(3).ToArray());
        Assert.Contains("Ñandú Peña", Encoding.UTF8.GetString(csv));
    }

    [Fact]
    public void ElCsv_UsaPuntoYComa_FechasAnioMesDia_YPesosEnterosSinMiles()
    {
        var lineas = LineasDelCsv(GeneradorCsv.Generar(DocumentoConUnaTabla(["Ana Pérez", new DateOnly(2026, 11, 5), 1_234_567])));

        Assert.Equal("Cliente;Fecha;Monto", lineas[0]);
        Assert.Equal("Ana Pérez;2026-11-05;1234567", lineas[1]);
    }

    [Fact]
    public void ElCsv_NoLlevaFilaDeTotales_SoloLosDatos()
    {
        var lineas = LineasDelCsv(GeneradorCsv.Generar(DocumentoConUnaTabla(["Ana", new DateOnly(2026, 11, 5), 1000], ["Luis", new DateOnly(2026, 11, 6), 2000])));

        // Encabezado, dos filas y el salto final: nada mas.
        Assert.Equal(["Cliente;Fecha;Monto", "Ana;2026-11-05;1000", "Luis;2026-11-06;2000", ""], lineas);
    }

    [Fact]
    public void ElCsv_TerminaCadaLineaConCrLf_SegunLaRfc4180()
    {
        var texto = Encoding.UTF8.GetString(GeneradorCsv.Generar(DocumentoConUnaTabla(["Ana", new DateOnly(2026, 11, 5), 1])));

        Assert.EndsWith("\r\n", texto);
        Assert.DoesNotContain("\n", texto.Replace("\r\n", ""));
    }

    [Theory]
    [InlineData("Ana; Luis", "\"Ana; Luis\"")]                      // el separador obliga a las comillas
    [InlineData("Dijo \"hola\"", "\"Dijo \"\"hola\"\"\"")]          // las comillas se duplican
    [InlineData("linea1\nlinea2", "\"linea1\nlinea2\"")]            // un salto de linea dentro del campo
    [InlineData("linea1\r\nlinea2", "\"linea1\r\nlinea2\"")]
    [InlineData("Ñandú, S.A.", "Ñandú, S.A.")]                      // la coma no es el separador: no necesita comillas
    [InlineData("Peña", "Peña")]
    public void ElCsv_EscapaSegunLaRfc4180(string texto, string esperado)
    {
        var csv = Encoding.UTF8.GetString(GeneradorCsv.Generar(DocumentoConUnaTabla([texto, new DateOnly(2026, 11, 5), 1]))).TrimStart('﻿');

        Assert.Contains($"\r\n{esperado};2026-11-05;1\r\n", csv);
    }

    [Theory]
    [InlineData("=SUMA(A1:A9)", "'=SUMA(A1:A9)")]
    [InlineData("+56912345678", "'+56912345678")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@usuario", "'@usuario")]
    [InlineData("\tpestaña", "'\tpestaña")]                         // la tabulacion tambien abre una formula en Excel
    [InlineData("=HYPERLINK(\"http://x\";\"a\")", "\"'=HYPERLINK(\"\"http://x\"\";\"\"a\"\")\"")]  // la defensa y el escape se suman
    public void ElCsv_UnTextoQueEmpiezaComoFormula_SeEscribeConApostrofo(string texto, string esperado)
    {
        var csv = Encoding.UTF8.GetString(GeneradorCsv.Generar(DocumentoConUnaTabla([texto, new DateOnly(2026, 11, 5), 1]))).TrimStart('﻿');

        Assert.Contains($"\r\n{esperado};2026-11-05;1\r\n", csv);
    }

    [Fact]
    public void ElCsv_UnNumeroNegativo_NoSeTrataComoTexto_NoLlevaApostrofo()
    {
        var csv = Encoding.UTF8.GetString(GeneradorCsv.Generar(DocumentoConUnaTabla(["Ana", new DateOnly(2026, 11, 5), -5000])));

        Assert.Contains("Ana;2026-11-05;-5000\r\n", csv);
    }

    [Fact]
    public void ElCsv_UnTextoQueNoEsFormula_NoSeAltera()
    {
        var csv = Encoding.UTF8.GetString(GeneradorCsv.Generar(DocumentoConUnaTabla(["Mario - Pérez", new DateOnly(2026, 11, 5), 1])));

        Assert.Contains("\r\nMario - Pérez;2026-11-05;1\r\n", csv);
    }

    [Fact]
    public void ElCsv_UnaCeldaVacia_LlevaSuTextoDeVacio_OQuedaVacia()
    {
        var lineas = LineasDelCsv(GeneradorCsv.Generar(DocumentoConUnaTabla(["Ana", new DateOnly(2026, 11, 5), null], ["Luis", null, 1])));

        Assert.Equal("Ana;2026-11-05;Sin costo", lineas[1]);
        Assert.Equal("Luis;;1", lineas[2]);
    }

    [Fact]
    public void ElCsv_UnTitulo_ConFormula_TambienSeDefiende()
    {
        var documento = DocumentoConUnaTabla(["Ana", new DateOnly(2026, 11, 5), 1]);
        var tabla = documento.Tablas[0] with { Columnas = [new ColumnaReporte("c", "=cmd|' /C calc'!A0", TiposColumna.Texto)], Filas = [["x"]] };

        var csv = Encoding.UTF8.GetString(GeneradorCsv.Generar(documento with { Tablas = [tabla] })).TrimStart('﻿');

        Assert.StartsWith("'=cmd", csv);
    }

    [Fact]
    public void ElCsv_ExportaLaTablaPrincipal_NoLaPrimera()
    {
        var principal = new TablaReporte("b", "B", [new ColumnaReporte("x", "ColumnaB", TiposColumna.Texto)], [["fila-b"]], Principal: true);
        var otra = new TablaReporte("a", "A", [new ColumnaReporte("x", "ColumnaA", TiposColumna.Texto)], [["fila-a"]]);
        var documento = new DocumentoReporte("operativo", "T", DateTimeOffset.UnixEpoch, [], [otra, principal], [], []);

        var texto = Encoding.UTF8.GetString(GeneradorCsv.Generar(documento));

        Assert.Contains("ColumnaB", texto);
        Assert.DoesNotContain("ColumnaA", texto);
    }
}
