using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using LetrerosHerreraSYNCTIMEapi.Models;

namespace LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;

// Como se audita una entidad: su nombre en la bitacora, su texto para el visor, los campos cuyo valor se puede
// mostrar (estados, montos, fechas, activo y visible), los que nunca se registran (version, contadores tecnicos)
// y los que acompañan siempre al registro (por ejemplo el monto de un abono que se anula).
public sealed record EntidadAuditada(
    string Nombre,
    string Texto,
    IReadOnlySet<string> Valores,
    IReadOnlySet<string>? Ignorados = null,
    IReadOnlySet<string>? Siempre = null)
{
    public bool Ignora(string propiedad) => propiedad == "RowVersion" || Ignorados?.Contains(propiedad) == true;

    // Un valor sensible nunca se muestra, aunque alguien lo agregue por error a la lista permitida.
    public bool MuestraValor(string propiedad) => Valores.Contains(propiedad) && !ReglasAuditoria.Sensibles.Contains(propiedad);
}

public sealed record CambioDeCampo(string Propiedad, object? Antes, object? Despues);

// Fase 11 (RFS12, S20): que entidades registra el interceptor y con que reglas. Una prueba de arquitectura exige
// que cada entidad del modelo este aqui o en NoAuditadas, con su motivo.
public static class ReglasAuditoria
{
    private static HashSet<string> C(params string[] campos) => new(campos, StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> Sensibles = C(
        "PasswordHash", "TokenHash", "TokenEquipoHash", "Hash", "DescriptorCifrado", "DescriptorFrente", "DescriptorIzquierda", "DescriptorDerecha",
        "Email", "Correo", "Destinatarios", "ResponderA", "Asunto", "CuerpoHtml", "CuerpoTexto", "EnlaceBaja", "HuellaIp", "Telefono", "Rut",
        "Direccion", "DireccionInstalacion");

    public static readonly IReadOnlyDictionary<Type, EntidadAuditada> Entidades = new Dictionary<Type, EntidadAuditada>
    {
        [typeof(Solicitud)] = new("solicitud", "Solicitud", C("Estado", "EliminadoLogico", "FechaEliminacionLogica", "IdCliente", "IdEmpleadoRegistra")),
        [typeof(SolicitudDetalle)] = new("solicitud_detalle", "Ítem de solicitud", C("IdSolicitud", "IdProducto", "AnchoMm", "AltoMm", "Cantidad")),
        [typeof(Cotizacion)] = new("cotizacion", "Cotización", C("IdSolicitud", "Estado", "CostoManoObra", "OtrosCostos", "TotalCotizacion",
            "CostoMaterialesTotal", "ViaRespuesta", "FechaRespuesta", "EliminadoLogico", "IdEmpleadoRespuesta")),
        [typeof(Pedido)] = new("pedido", "Pedido", C("IdSolicitud", "EstadoPedido", "EstadoPago", "MontoBaseTrabajo", "CostoInstalacionAdicional",
            "MontoTotalFinal", "MontoAbonado", "SaldoPendiente", "FechaEntregaEstimada", "FechaEntregaReal", "ModalidadEntrega", "IdEmpleadoResponsable",
            "EliminadoLogico", "FechaAnulacion")),
        // E25: cada abono y cada anulacion con quien, cuando y cuanto (el monto va siempre).
        [typeof(Pago)] = new("pago", "Abono", C("IdPedido", "Monto", "FechaPago", "MedioPago", "Anulado", "FechaAnulacion", "IdEmpleadoAnula"),
            Siempre: C("Monto", "IdPedido")),
        [typeof(Catalogo)] = new("catalogo", "Publicación de la vitrina", C("IdProducto", "Visible", "IncluyeLogoCliente", "PrecioReferencial", "FechaAutorizacionLogo")),
        [typeof(Producto)] = new("producto", "Tipo de letrero", C("IdCategoria", "Activo", "PrecioBaseRef", "AnchoMinMm", "AnchoMaxMm", "AltoMinMm", "AltoMaxMm")),
        // Fase 12D: el taller edita las medidas fijas y las opciones de cada producto desde el mantenedor.
        [typeof(ProductoMedidaFija)] = new("producto_medida", "Medida fija de un tipo de letrero", C("IdProducto", "AnchoMm", "AltoMm")),
        [typeof(ProductoOpcion)] = new("producto_opcion", "Opción de un tipo de letrero", C("IdProducto", "Grupo", "Valor", "Orden")),
        [typeof(Categoria)] = new("categoria", "Categoría", C("Activo")),
        [typeof(Material)] = new("material", "Material", C("Activo", "CostoUnitarioAprox", "UnidadMedida")),
        [typeof(Cliente)] = new("cliente", "Cliente", C("Activo", "RecibeAvisosEstado", "InhabilitadoEn", "FechaVerificacionCorreo")),
        [typeof(Empleado)] = new("empleado", "Empleado", C("Activo", "Rol")),
        [typeof(DestinatarioAviso)] = new("destinatario_aviso", "Destino de avisos", C("Estado", "RecibeEnLinea", "RecibePresenciales", "FechaConfirmacion", "FechaRetiro")),
        // Los contadores del ingreso con rostro cambian en cada intento: no son un cambio del negocio.
        [typeof(RostroEmpleado)] = new("rostro_empleado", "Rostro", C("IdEmpleado", "FechaRevocacion", "VersionConsentimiento", "FormatoMuestras", "FechaAutorizacionEquipo"),
            Ignorados: C("IntentosFallidos", "BloqueadoHasta")),
        [typeof(HorarioAtencion)] = new("horario_atencion", "Horario", C("DiaSemana", "Abierto", "HoraInicio", "HoraFin")),
        [typeof(CartelDigital)] = new("cartel_digital", "Cartel del local", C("AbiertoEnLocal"))
    };

    // Las demas entidades del modelo, con el motivo por el que el interceptor no las registra.
    public static readonly IReadOnlyDictionary<Type, string> NoAuditadas = new Dictionary<Type, string>
    {
        [typeof(CotizacionMaterial)] = "Líneas de la cotización: su total queda en la cotización, que sí se audita.",
        [typeof(AdjuntoSolicitud)] = "Archivo de una solicitud: quitarlo queda en solicitudes_historial y renueva la versión de la solicitud.",
        [typeof(SolicitudHistorial)] = "Es en sí un historial de estados (fase 7).",
        [typeof(PedidoHistorial)] = "Es en sí un historial de estados (fase 8).",
        [typeof(DestinatarioAvisoHistorial)] = "Es en sí el historial de los destinos de avisos (fase 9).",
        [typeof(Consentimiento)] = "Es en sí el registro de consentimientos; el visor lo consulta aparte.",
        [typeof(CorreoSaliente)] = "La cola de correo: el trabajador registra a mano los envíos y fallos (sin cuerpo ni direcciones).",
        [typeof(RegistroAuditoria)] = "La propia bitácora.",
        [typeof(ClaveIdempotencia)] = "Dato técnico de 24 horas para no repetir una creación.",
        [typeof(TokenRenovacion)] = "Sesiones: datos técnicos con hash, de vida corta.",
        [typeof(CodigoCuenta)] = "Códigos de un solo uso: datos técnicos con hash, de vida corta.",
        [typeof(DesafioRostro)] = "Desafíos de la prueba de vida: segundos de vida.",
        [typeof(MedicionCalibracionRostro)] = "Mediciones técnicas de la calibración del rostro.",
        [typeof(Imagen)] = "Archivo de imagen: su uso queda en la publicación de la vitrina, que sí se audita.",
        [typeof(ConsultaContacto)] = "Mensaje de un visitante: su aviso al taller queda en la bitácora.",
        [typeof(DisponibilidadEmpleado)] = "Franjas que cada persona marca para sí misma, sin efecto en datos del negocio.",
        [typeof(TrabajoExportacion)] = "Cola de exportaciones de reportes: quién lo pidió, con qué filtros y cuántas filas salieron quedan en la bitácora (exportar_reporte), sin el contenido."
    };

    public static readonly IReadOnlyDictionary<string, string> TextosEntidad =
        Entidades.Values.ToDictionary(e => e.Nombre, e => e.Texto)
            .Concat(new Dictionary<string, string> { ["correo"] = "Correo", ["cuenta"] = "Cuenta del cliente", ["reporte"] = "Reporte" })
            .ToDictionary(p => p.Key, p => p.Value);

    public static readonly IReadOnlyDictionary<string, string> TextosAccion = new Dictionary<string, string>
    {
        [AccionesAuditoria.Crear] = "Creó",
        [AccionesAuditoria.Modificar] = "Modificó",
        [AccionesAuditoria.Eliminar] = "Eliminó",
        [AccionesAuditoria.AvisoEnviado] = "Aviso enviado",
        [AccionesAuditoria.CorreoFallido] = "Correo no enviado",
        [AccionesAuditoria.CorreoReenviado] = "Reenvió un correo",
        [AccionesAuditoria.CorreoRevisado] = "Marcó un correo como revisado",
        [AccionesAuditoria.BajaCorreo] = "Se dio de baja de correos",
        [AccionesAuditoria.BajaCuenta] = "Dio de baja su cuenta",
        [AccionesAuditoria.DescargaDatos] = "Descargó sus datos",
        [AccionesAuditoria.ExportarReporte] = "Exportó un reporte"
    };

    public static string Camello(string propiedad) => propiedad.Length == 0 ? propiedad : char.ToLowerInvariant(propiedad[0]) + propiedad[1..];
}

// El detalle (JSON, hasta 2000 caracteres) de un registro de la bitacora.
public static class DetalleAuditoria
{
    public const int Maximo = 2000;
    private const int MaximoTexto = 200;

    private static readonly JsonSerializerOptions Opciones = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Los nombres de los campos cambiados y, solo para los permitidos, su valor antes y despues.
    public static string? DeCambios(EntidadAuditada reglas, IReadOnlyList<CambioDeCampo> cambios, IReadOnlyList<CambioDeCampo>? contexto = null)
    {
        var campos = new JsonArray();
        var valores = new JsonObject();
        foreach (var cambio in cambios.Where(c => !reglas.Ignora(c.Propiedad)))
        {
            campos.Add(ReglasAuditoria.Camello(cambio.Propiedad));
            if (reglas.MuestraValor(cambio.Propiedad))
                valores[ReglasAuditoria.Camello(cambio.Propiedad)] = Par(cambio);
        }
        foreach (var dato in contexto ?? [])
            if (reglas.MuestraValor(dato.Propiedad) && !valores.ContainsKey(ReglasAuditoria.Camello(dato.Propiedad)))
                valores[ReglasAuditoria.Camello(dato.Propiedad)] = Par(dato);
        if (campos.Count == 0 && valores.Count == 0)
            return null;

        var detalle = new JsonObject { ["campos"] = campos };
        if (valores.Count > 0)
            detalle["valores"] = valores;
        var texto = detalle.ToJsonString(Opciones);
        if (texto.Length <= Maximo)
            return texto;

        // Demasiado largo: primero sin valores, despues con los campos que quepan.
        detalle.Remove("valores");
        texto = detalle.ToJsonString(Opciones);
        while (texto.Length > Maximo - 20 && campos.Count > 0)
        {
            campos.RemoveAt(campos.Count - 1);
            detalle["truncado"] = true;
            texto = detalle.ToJsonString(Opciones);
        }
        return texto;
    }

    // Un detalle propio de la accion (por ejemplo, el de un correo). Los textos se recortan.
    public static string? Libre(IReadOnlyDictionary<string, object?> datos)
    {
        var objeto = new JsonObject();
        foreach (var (clave, valor) in datos)
            objeto[clave] = Nodo(valor);
        var texto = objeto.ToJsonString(Opciones);
        if (texto.Length <= Maximo)
            return texto;
        return new JsonObject { ["truncado"] = true, ["claves"] = new JsonArray(datos.Keys.Select(k => (JsonNode?)JsonValue.Create(k)).ToArray()) }
            .ToJsonString(Opciones) is { Length: <= Maximo } corto ? corto : "{\"truncado\":true}";
    }

    private static JsonObject Par(CambioDeCampo cambio) => new() { ["antes"] = Nodo(cambio.Antes), ["despues"] = Nodo(cambio.Despues) };

    private static JsonNode? Nodo(object? valor) => valor switch
    {
        null => null,
        string s => JsonValue.Create(s.Length > MaximoTexto ? s[..MaximoTexto] + "…" : s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        byte by => JsonValue.Create(by),
        decimal d => JsonValue.Create(d),
        double db => JsonValue.Create(db),
        DateTime f => JsonValue.Create(f.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)),
        DateTimeOffset fo => JsonValue.Create(fo.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)),
        DateOnly d1 => JsonValue.Create(d1.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
        TimeOnly t => JsonValue.Create(t.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)),
        IEnumerable<string> lista => new JsonArray(lista.Select(x => Nodo(x)).ToArray()),
        // Fase 13A: los filtros de un reporte (nombre y valor, ya validados y sin datos personales).
        IReadOnlyDictionary<string, string> mapa => new JsonObject(mapa.Select(p => KeyValuePair.Create(p.Key, Nodo(p.Value)))),
        _ => JsonValue.Create(valor.ToString())
    };
}
