using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Security;

// Limite de intentos fallidos por cuenta (RNFP07): complementa el limite por IP de Program.cs.
// Sin el, quien reparte sus intentos entre muchas IP puede probar contraseñas de una misma cuenta sin tope.
//
// Ventana fija de un minuto por cuenta, en memoria de este proceso.
//  - Solo cuentan los fallos, y un acierto borra el contador: quien entra bien no gasta intentos.
//  - Cuenta igual exista o no la cuenta, asi que el limite no revela si existe.
//  - La clave es un hash de la cuenta normalizada: ocupa siempre lo mismo y no guarda correos en memoria.
//
// Riesgo aceptado, propio de todo limite por cuenta: quien conoce el correo de otra persona puede
// agotar sus intentos y dejarla sin entrar con contraseña mientras insista. El acceso con rostro usa
// un contador aparte y no se ve afectado.
public sealed class LimitadorPorCuenta(int limite, TimeProvider? reloj = null, int cuentasAntesDeLimpiar = 10_000)
{
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(1);

    private readonly TimeProvider reloj = reloj ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, Contador> contadores = new(StringComparer.Ordinal);
    private long ultimaLimpieza;

    public int CuentasEnMemoria => contadores.Count;

    // true si la cuenta ya agoto sus intentos fallidos de este minuto.
    public bool EstaBloqueada(string? cuenta)
    {
        if (Clave(cuenta) is not { } clave || !contadores.TryGetValue(clave, out var contador))
            return false;
        return reloj.GetUtcNow() - contador.Inicio < Ventana && contador.Fallos >= limite;
    }

    public void RegistrarFallo(string? cuenta)
    {
        if (Clave(cuenta) is not { } clave)
            return;

        var ahora = reloj.GetUtcNow();
        if (contadores.Count >= cuentasAntesDeLimpiar)
        {
            QuitarVencidos(ahora);
            // Tope duro: si aun asi hay demasiadas cuentas vivas, no se agregan mas. El limite por IP sigue activo.
            if (contadores.Count >= cuentasAntesDeLimpiar * 5 && !contadores.ContainsKey(clave))
                return;
        }

        contadores.AddOrUpdate(
            clave,
            _ => new Contador(ahora, 1),
            (_, actual) => ahora - actual.Inicio >= Ventana ? new Contador(ahora, 1) : actual with { Fallos = actual.Fallos + 1 });
    }

    // Un acierto borra los fallos anteriores de la cuenta.
    public void Olvidar(string? cuenta)
    {
        if (Clave(cuenta) is { } clave)
            contadores.TryRemove(clave, out _);
    }

    // Recorre el diccionario como mucho una vez por ventana, aunque lleguen muchas peticiones a la vez.
    private void QuitarVencidos(DateTimeOffset ahora)
    {
        var anterior = Interlocked.Read(ref ultimaLimpieza);
        if (anterior != 0 && ahora.UtcTicks - anterior < Ventana.Ticks)
            return;
        if (Interlocked.CompareExchange(ref ultimaLimpieza, ahora.UtcTicks, anterior) != anterior)
            return;

        foreach (var (clave, contador) in contadores)
        {
            if (ahora - contador.Inicio >= Ventana)
                contadores.TryRemove(new KeyValuePair<string, Contador>(clave, contador));
        }
    }

    // La misma cuenta escrita de formas equivalentes (mayusculas, espacios, letras de ancho completo)
    // debe dar la misma clave: SQL Server las compara como iguales.
    private static string? Clave(string? cuenta)
    {
        if (string.IsNullOrWhiteSpace(cuenta))
            return null;
        var normalizada = cuenta.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizada)));
    }

    private sealed record Contador(DateTimeOffset Inicio, int Fallos);
}
