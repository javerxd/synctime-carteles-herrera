using System.Net;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Almacen;
using Microsoft.AspNetCore.HttpOverrides;

namespace LetrerosHerreraSYNCTIMEapi.Api;

// Fase 16A: lo que cambia entre el equipo de desarrollo y un servidor de verdad (Render, Docker). Todo se declara por
// variables de entorno y se revisa al arrancar: un valor mal escrito impide arrancar, que es preferible a una API que
// parece funcionar y falla cuando el navegador rechaza una peticion o un correo sale con un enlace roto.
public static class ConfiguracionDespliegue
{
    public const string SeccionReenvio = "ForwardedHeaders";
    private const int MaximoDeSaltos = 10;

    // Detras de un proxy que termina el HTTPS (Render, el nginx de compose), la API ve la IP y el protocolo del proxy, no los
    // del visitante: todos compartirian el mismo cupo de peticiones y UseHttpsRedirection entraria en bucle.
    // Las cabeceras X-Forwarded-* las escribe cualquiera, asi que solo se leen si vienen de un proxy que se DECLARA:
    //   ForwardedHeaders:Habilitado           true para leerlas (por omision, no se leen)
    //   ForwardedHeaders:ProxiesDeConfianza   direcciones IP de los proxies
    //   ForwardedHeaders:RedesDeConfianza     redes en notacion CIDR (10.0.0.0/8)
    //   ForwardedHeaders:LimiteDeSaltos       cuantos proxies hay en la cadena (por omision, 1)
    // Devuelve null si no esta habilitado.
    public static ForwardedHeadersOptions? OpcionesDeReenvio(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(SeccionReenvio);
        if (!seccion.GetValue("Habilitado", false))
            return null;

        var proxies = (seccion.GetSection("ProxiesDeConfianza").Get<string[]>() ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        var redes = (seccion.GetSection("RedesDeConfianza").Get<string[]>() ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();
        if (proxies.Length + redes.Length == 0)
            throw new InvalidOperationException(
                $"{SeccionReenvio}:Habilitado es true pero no hay proxies de confianza: declare {SeccionReenvio}:ProxiesDeConfianza o {SeccionReenvio}:RedesDeConfianza. " +
                "Sin esa lista, cualquier visitante podria escribir X-Forwarded-For y saltarse los limites de tasa.");

        var limite = seccion.GetValue("LimiteDeSaltos", 1);
        if (limite is < 1 or > MaximoDeSaltos)
            throw new InvalidOperationException($"{SeccionReenvio}:LimiteDeSaltos debe estar entre 1 y {MaximoDeSaltos} y es {limite}.");

        var opciones = new ForwardedHeadersOptions
        {
            // Solo la IP y el protocolo. X-Forwarded-Host no se lee: la API no genera enlaces con el host de la peticion.
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = limite,
        };
        // Por omision el middleware confia en el loopback; aqui solo se confia en lo declarado.
        opciones.KnownProxies.Clear();
        opciones.KnownIPNetworks.Clear();

        foreach (var texto in proxies)
        {
            if (!IPAddress.TryParse(texto.Trim(), out var direccion))
                throw new InvalidOperationException($"{SeccionReenvio}:ProxiesDeConfianza tiene un valor que no es una direccion IP: «{texto}».");
            opciones.KnownProxies.Add(direccion);
        }
        foreach (var texto in redes)
        {
            if (!System.Net.IPNetwork.TryParse(texto.Trim(), out var red))
                throw new InvalidOperationException($"{SeccionReenvio}:RedesDeConfianza tiene un valor que no es una red CIDR valida (por ejemplo 10.0.0.0/8): «{texto}».");
            // Confiar en 0.0.0.0/0 equivale a confiar en todo internet, o sea, a no tener lista. Una red privada (la de los proxies de
            // Render o de compose) puede llegar a /8; una red PUBLICA, solo si es chica (un proxy propio), nunca un bloque entero.
            var minimo = EsRedPrivada(red) ? 8 : (red.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 64 : 24);
            if (red.PrefixLength < minimo)
                throw new InvalidOperationException(
                    $"{SeccionReenvio}:RedesDeConfianza abarca demasiadas direcciones (/{red.PrefixLength}): «{texto}». Declare solo la red de los proxies (minimo /{minimo} para esta red).");
            opciones.KnownIPNetworks.Add(red);
        }
        return opciones;
    }

    // Redes privadas, de loopback o de enlace local: donde viven los proxies de una plataforma, no internet.
    private static bool EsRedPrivada(System.Net.IPNetwork red)
    {
        var d = red.BaseAddress;
        if (d.IsIPv4MappedToIPv6)
            d = d.MapToIPv4();
        if (System.Net.IPAddress.IsLoopback(d) || d.IsIPv6LinkLocal || d.IsIPv6SiteLocal)
            return true;
        var b = d.GetAddressBytes();
        if (b.Length == 16) // IPv6: direcciones locales unicas (fc00::/7)
            return (b[0] & 0xFE) == 0xFC;
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127) || (b[0] == 169 && b[1] == 254);
    }

    // Fase 16C-1 (H12): donde viven los archivos de los clientes. Render gratuito no tiene disco persistente: sin Blob, cada reinicio los borraria.
    // Por eso, fuera de desarrollo, Almacen:Modo se DECLARA (Disco o Blob): una variable olvidada no puede dejar la API guardando adjuntos en un disco
    // que se pierde. Con Blob, la cadena de conexion y el nombre del contenedor se revisan al arrancar (el mensaje nunca repite la cadena).
    public static OpcionesAlmacen OpcionesDeAlmacen(IConfiguration configuracion, IHostEnvironment entorno)
    {
        var seccion = configuracion.GetSection(OpcionesAlmacen.Seccion);
        if (!entorno.IsDevelopment() && string.IsNullOrWhiteSpace(seccion["Modo"]))
            throw new InvalidOperationException(
                "Falta Almacen:Modo: declare Disco (carpetas del servidor, para compose) o Blob (Azure Blob Storage, para la nube). " +
                "Render gratuito no tiene disco persistente: ahi se usa Blob.");
        var opciones = seccion.Get<OpcionesAlmacen>() ?? new OpcionesAlmacen();
        if (opciones.Problema() is { } problema)
            throw new InvalidOperationException(problema);
        if (opciones.EsBlob)
        {
            try
            {
                _ = new Azure.Storage.Blobs.BlobServiceClient(opciones.Blob.CadenaDeConexion);
            }
            catch (Exception)
            {
                // Sin la excepcion original: su texto podria repetir parte de la cadena, que lleva la clave de la cuenta. Se arma el cliente
                // (no se conecta a nada) para que una cadena mal pegada impida arrancar y no falle recien en la primera subida de un cliente.
                throw new InvalidOperationException("Almacen:Blob:CadenaDeConexion no es una cadena de conexion valida de Azure Storage.");
            }
        }
        return opciones;
    }

    // Los origenes que el navegador puede usar para llamar a la API con credenciales (RNFP29).
    // En desarrollo, sin configuracion, queda el servidor de Vite. Fuera de desarrollo es obligatorio declararlos: antes, la falta
    // de configuracion dejaba en silencio «https://localhost:3000» y el sitio publicado fallaba solo en el navegador.
    public static string[] OrigenesCors(IConfiguration configuracion, IHostEnvironment entorno)
    {
        var origenes = (configuracion.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
        if (origenes.Length == 0)
        {
            if (entorno.IsDevelopment())
                return ["https://localhost:3000"];
            throw new InvalidOperationException(
                "Falta Cors:AllowedOrigins: declare el origen del sitio, por ejemplo Cors__AllowedOrigins__0=https://synctime.vercel.app (sin barra final).");
        }
        foreach (var origen in origenes)
        {
            var problema = ProblemaDeOrigen(origen);
            if (problema is not null)
                throw new InvalidOperationException($"Cors:AllowedOrigins tiene un valor invalido («{origen}»): {problema}");
        }
        return origenes;
    }

    // La direccion publica del sitio: de aqui salen los enlaces de los correos (confirmar, baja) y del codigo QR.
    // En desarrollo puede faltar (la API avisa cuando la necesita); fuera de desarrollo un enlace roto llega a clientes reales.
    public static void ValidarUrlPublica(IConfiguration configuracion, IHostEnvironment entorno)
    {
        if (entorno.IsDevelopment())
            return;
        var url = configuracion["Sitio:UrlPublica"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Falta Sitio:UrlPublica: es la direccion del sitio que llevan los enlaces de los correos, por ejemplo https://synctime.vercel.app.");
        var problema = ProblemaDeOrigen(url);
        if (problema is not null)
            throw new InvalidOperationException($"Sitio:UrlPublica es invalida («{url}»): {problema}");
    }

    // Un origen es esquema, servidor y puerto: sin ruta, sin barra final, sin comodines y en minusculas (asi lo envia el navegador).
    private static string? ProblemaDeOrigen(string origen)
    {
        if (origen.Contains('*'))
            return "no admite comodines.";
        if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return "debe ser una direccion completa que empiece con https:// (por ejemplo https://synctime.vercel.app).";
        if (!string.Equals(uri.GetLeftPart(UriPartial.Authority), origen, StringComparison.Ordinal) || !string.IsNullOrEmpty(uri.UserInfo))
            return "debe ser solo el origen: sin barra final, sin ruta, sin usuario y en minusculas.";
        if (uri.Scheme == "http" && !uri.IsLoopback)
            return "solo https; http se admite unicamente para localhost.";
        return null;
    }
}
