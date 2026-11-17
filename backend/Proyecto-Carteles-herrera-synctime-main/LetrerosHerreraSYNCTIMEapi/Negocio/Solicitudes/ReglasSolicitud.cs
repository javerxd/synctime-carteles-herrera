using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Solicitudes;

// ── Fase 7: reglas de las solicitudes (RFS04, RFS09, RNFP32, RNFP33) ──
// Sin HTTP ni base de datos: se prueban solas en LetrerosHerreraSYNCTIMEapi.UnitTests.

public static class EstadosSolicitud
{
    public const string Solicitado = "solicitado";
    public const string Cotizado = "cotizado";
    public const string Aceptado = "aceptado";
    public const string Rechazado = "rechazado";

    public static readonly string[] Todos = [Solicitado, Cotizado, Aceptado, Rechazado];

    public static bool Existe(string? estado) => estado is not null && Todos.Contains(estado);

    // «En revision»: todavia puede terminar en pedido.
    public static bool EsPendiente(string estado) => estado is Solicitado or Cotizado;

    // Los datos e items solo se editan mientras no hay una decision tomada.
    public static bool EsEditable(string estado) => estado is Solicitado or Cotizado;

    // El borrado (logico) solo para lo que nunca llego a cotizarse o ya se descarto.
    public static bool PuedeEliminarse(string estado) => estado is Solicitado or Rechazado;
}

// Quien pide el cambio de estado. El sistema cotiza y acepta (fase 8, al emitir o aprobar una cotizacion);
// el cliente puede rechazar una cotizacion; el personal de gestion rechaza o devuelve a «solicitado».
public enum ActorEstado { Sistema, Cliente, Personal }

public static class TransicionesSolicitud
{
    private static readonly (string Desde, string Hacia, ActorEstado[] Quien)[] Permitidas =
    [
        (EstadosSolicitud.Solicitado, EstadosSolicitud.Cotizado, [ActorEstado.Sistema]),
        (EstadosSolicitud.Cotizado, EstadosSolicitud.Solicitado, [ActorEstado.Cliente, ActorEstado.Personal, ActorEstado.Sistema]),
        (EstadosSolicitud.Cotizado, EstadosSolicitud.Aceptado, [ActorEstado.Sistema]),
        (EstadosSolicitud.Solicitado, EstadosSolicitud.Rechazado, [ActorEstado.Personal]),
        (EstadosSolicitud.Cotizado, EstadosSolicitud.Rechazado, [ActorEstado.Personal])
    ];

    public static bool EsValida(string desde, string hacia, ActorEstado quien) =>
        Permitidas.Any(t => t.Desde == desde && t.Hacia == hacia && t.Quien.Contains(quien));

    // null si la transicion es valida; si no, la explicacion para el 409.
    public static string? Explicar(string desde, string hacia, ActorEstado quien)
    {
        if (EsValida(desde, hacia, quien))
            return null;
        if (desde == hacia)
            return $"La solicitud ya está «{desde}».";
        if (desde is EstadosSolicitud.Aceptado or EstadosSolicitud.Rechazado)
            return $"Una solicitud «{desde}» ya no cambia de estado.";
        if (hacia == EstadosSolicitud.Cotizado)
            return "Una solicitud pasa a «cotizado» solo al emitir una cotización.";
        if (hacia == EstadosSolicitud.Aceptado)
            return "Una solicitud pasa a «aceptado» solo al aprobar su cotización.";
        if (hacia == EstadosSolicitud.Solicitado)
            return "Solo una solicitud «cotizado» puede volver a «solicitado».";
        return $"No se puede pasar de «{desde}» a «{hacia}».";
    }
}

// Lo que llega para crear o editar una solicitud, ya separado del formato de la peticion.
public sealed record EntradaDetalle(int IdProducto, string? Descripcion, int? AnchoMm, int? AltoMm, int Cantidad, IReadOnlyDictionary<string, string>? Opciones = null);

public static class ValidacionSolicitud
{
    public const int MinDetalles = 1;
    public const int MaxDetalles = 10;
    public const int MinDescripcion = 10;
    public const int MaxDescripcion = 2000;
    public const int MaxDescripcionDetalle = 500;
    public const int MaxMedidaMm = 50_000;
    public const int MaxCantidad = 999;
    public const int MinMotivo = 10;
    public const int MaxMotivo = 500;
    public const int MaxAdjuntos = 5;

    public const string MensajeProducto = "Uno de los productos no existe o no está disponible.";

    // Errores por campo, con las claves que usa el formulario: descripcionGeneral, detalles,
    // detalles[0].anchoMm, adjuntos. Vacio si todo esta bien (los productos se revisan contra la base).
    public static Dictionary<string, string[]> Validar(string? descripcionGeneral, IReadOnlyList<EntradaDetalle>? detalles, IReadOnlyList<int>? adjuntos)
    {
        var errores = new Dictionary<string, string[]>();
        ValidarDescripcion(descripcionGeneral, errores);
        ValidarDetalles(detalles, errores);
        if (adjuntos is { } ids && ids.Distinct().Count() > MaxAdjuntos)
            errores["adjuntos"] = [$"Puedes adjuntar hasta {MaxAdjuntos} archivos por solicitud."];
        return errores;
    }

    public static void ValidarDescripcion(string? descripcion, Dictionary<string, string[]> errores)
    {
        var largo = descripcion?.Trim().Length ?? 0;
        if (largo < MinDescripcion || largo > MaxDescripcion)
            errores["descripcionGeneral"] = [$"Cuéntanos qué necesitas en {MinDescripcion} a {MaxDescripcion} caracteres."];
    }

    public static void ValidarDetalles(IReadOnlyList<EntradaDetalle>? detalles, Dictionary<string, string[]> errores)
    {
        if (detalles is null || detalles.Count < MinDetalles)
        {
            errores["detalles"] = ["Agrega al menos un letrero a la solicitud."];
            return;
        }
        if (detalles.Count > MaxDetalles)
        {
            errores["detalles"] = [$"Una solicitud admite hasta {MaxDetalles} letreros."];
            return;
        }

        for (var i = 0; i < detalles.Count; i++)
        {
            var d = detalles[i];
            var campo = $"detalles[{i}]";
            if (d.IdProducto <= 0)
                errores[$"{campo}.idProducto"] = ["Elige el tipo de letrero."];
            if (d.Descripcion?.Trim().Length > MaxDescripcionDetalle)
                errores[$"{campo}.descripcion"] = [$"La descripción del letrero admite hasta {MaxDescripcionDetalle} caracteres."];
            if (d.AnchoMm is null != d.AltoMm is null)
            {
                errores[$"{campo}.{(d.AnchoMm is null ? "anchoMm" : "altoMm")}"] = ["Indica el ancho y el alto, o deja los dos en blanco."];
            }
            else
            {
                if (d.AnchoMm is int ancho && (ancho < 1 || ancho > MaxMedidaMm))
                    errores[$"{campo}.anchoMm"] = [$"El ancho debe estar entre 1 y {MaxMedidaMm} mm."];
                if (d.AltoMm is int alto && (alto < 1 || alto > MaxMedidaMm))
                    errores[$"{campo}.altoMm"] = [$"El alto debe estar entre 1 y {MaxMedidaMm} mm."];
            }
            if (d.Cantidad < 1 || d.Cantidad > MaxCantidad)
                errores[$"{campo}.cantidad"] = [$"La cantidad debe estar entre 1 y {MaxCantidad}."];
        }
    }

    public static string? ValidarMotivo(string? motivo)
    {
        var largo = motivo?.Trim().Length ?? 0;
        return largo < MinMotivo || largo > MaxMotivo
            ? $"Explica el motivo del rechazo en {MinMotivo} a {MaxMotivo} caracteres."
            : null;
    }

    // Filtros de la bandeja: un valor desconocido es un error (400), no se ignora en silencio.
    public static Dictionary<string, string[]> ValidarFiltro(string? estado, string? origen, string? orden)
    {
        var errores = new Dictionary<string, string[]>();
        if (!string.IsNullOrEmpty(estado) && !EstadosSolicitud.Existe(estado))
            errores["estado"] = [$"Estado inválido. Valores: {string.Join(", ", EstadosSolicitud.Todos)}."];
        if (!string.IsNullOrEmpty(origen) && origen is not ("online" or "presencial"))
            errores["origen"] = ["Origen inválido. Valores: online, presencial."];
        if (!string.IsNullOrEmpty(orden) && orden is not ("fecha_desc" or "fecha_asc"))
            errores["orden"] = ["Orden inválido. Valores: fecha_desc, fecha_asc."];
        return errores;
    }

    // Mensaje principal del 400: el error si hay uno solo; si no, uno general (el detalle va por campo).
    public static string Resumen(IReadOnlyDictionary<string, string[]> errores) =>
        errores.Count == 1 ? errores.First().Value[0] : "Revisa los datos de la solicitud.";
}

public static class MedidasSolicitud
{
    // Area de un item en m², calculada siempre en el servidor: ancho × alto / 1.000.000 × cantidad,
    // redondeada a 2 decimales. null si el item no trae medidas.
    public static decimal? AreaM2(int? anchoMm, int? altoMm, int cantidad)
    {
        if (anchoMm is not int ancho || altoMm is not int alto)
            return null;
        return Math.Round((decimal)ancho * alto / 1_000_000m * cantidad, 2, MidpointRounding.AwayFromZero);
    }

    // Suma de las areas conocidas; null si ningun item trae medidas.
    public static decimal? AreaTotal(IEnumerable<decimal?> areas)
    {
        var conocidas = areas.Where(a => a is not null).Select(a => a!.Value).ToList();
        return conocidas.Count == 0 ? null : Math.Round(conocidas.Sum(), 2, MidpointRounding.AwayFromZero);
    }

    public static string Numero(int idSolicitud) => $"#{idSolicitud.ToString(CultureInfo.InvariantCulture)}";

    // «2,5 m²» con coma decimal, como se escribe en Chile.
    public static string Area(decimal areaM2) => $"{areaM2.ToString("0.##", CultureInfo.GetCultureInfo("es-CL"))} m²";
}

// RFS04, RNFP33: una persona no puede tener mas de una solicitud en linea en revision, ni enviar en
// rafaga desde la misma conexion. Cuando la regla frena un envio no se guarda nada, y el aviso es el
// mismo para todos los motivos: asi no revela si la otra solicitud es de esta cuenta o de otra persona.
public static class ReglaContraSolicitudesFalsas
{
    // RNFP33: 3 envios por hora por IP. Son los valores propuestos; se cambian en
    // Solicitudes:MaxEnLineaPorIp y Solicitudes:VentanaIpMinutos.
    public const int MaxEnLineaPorIpPredeterminado = 3;
    public const int VentanaIpMinutosPredeterminada = 60;

    public const string Codigo = "contacto_directo";
    // S25: el mismo aviso para todos los motivos.
    public const string Mensaje = "Para esta solicitud es preferible contactarse directamente con el local";

    // EnviosRecientesDeLaIp: los envios en linea de la IP dentro de la ventana configurada.
    public sealed record Hechos(bool CuentaConPendienteEnLinea, bool RutConPendiente, bool IpConPendienteEnLinea, int EnviosRecientesDeLaIp);

    public static bool Frena(Hechos hechos, int maxEnviosPorIp = MaxEnLineaPorIpPredeterminado) =>
        hechos.CuentaConPendienteEnLinea
        || hechos.RutConPendiente
        || hechos.IpConPendienteEnLinea
        || hechos.EnviosRecientesDeLaIp >= Math.Max(1, maxEnviosPorIp);
}

// La IP nunca se guarda en claro: solo su HMAC-SHA256 con una clave propia del servidor. Sin la clave
// no se puede volver a la IP probando las 4 mil millones de direcciones posibles.
public static class HuellaIp
{
    public const string Etiqueta = "synctime:huella-ip";

    // Clave de 256 bits derivada con HKDF de un secreto del servidor (Jwt:Key), con su propia etiqueta:
    // la clave que firma los tokens no se usa tal cual para otra cosa.
    public static byte[] DerivarClave(string secreto) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secreto), 32, info: Encoding.UTF8.GetBytes(Etiqueta));

    // Una IPv6 se reduce a su prefijo /64: un mismo hogar u oficina recibe millones de direcciones de
    // ese prefijo y podria cambiar de una en cada envio. Una IPv4 escrita como IPv6 cuenta como IPv4.
    public static string? Normalizar(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;
        if (!IPAddress.TryParse(ip.Trim(), out var direccion))
            return ip.Trim();
        if (direccion.IsIPv4MappedToIPv6)
            direccion = direccion.MapToIPv4();
        if (direccion.AddressFamily != AddressFamily.InterNetworkV6)
            return direccion.ToString();
        var bytes = direccion.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    public static string? Calcular(string? ip, byte[] clave)
    {
        if (Normalizar(ip) is not { } normalizada)
            return null;
        if (clave is not { Length: >= 16 })
            throw new ArgumentException("Falta la clave para calcular la huella de la IP.", nameof(clave));
        return Convert.ToHexStringLower(HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes(normalizada)));
    }
}
