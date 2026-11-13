using System.Globalization;

namespace LetrerosHerreraSYNCTIMEapi.Negocio.Correo;

// Una lista de direcciones escrita a mano ("a@b.cl; c@d.cl"), como la de los avisos del taller.
public static class ListaDeCorreos
{
    private static readonly char[] Separadores = [';', ','];

    // Devuelve las direcciones validas, sin repetir (sin distinguir mayusculas), en el orden escrito.
    // Las invalidas se cuentan para avisar, pero no se devuelven: un error de tipeo no debe impedir
    // que el aviso llegue a las demas.
    public static IReadOnlyList<string> Separar(string? texto, out int invalidos)
    {
        invalidos = 0;
        var validas = new List<string>();
        if (string.IsNullOrWhiteSpace(texto))
            return validas;

        foreach (var parte in texto.Split(Separadores, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Reglas.EsCorreo(parte))
                invalidos++;
            else if (!validas.Contains(parte, StringComparer.OrdinalIgnoreCase))
                validas.Add(parte);
        }
        return validas;
    }
}

// Cuanto esperar antes de volver a intentar un envio fallido. Crece con cada intento, para no insistir
// contra un servidor caido ni hacer que la cuenta parezca sospechosa por demasiados intentos seguidos.
public static class PoliticaReintentos
{
    private static readonly TimeSpan[] Esperas =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(180)
    ];

    // null: no queda otro intento. Si el problema es del servidor o de la cuenta (caido, sin conexion,
    // contraseña cambiada), el correo no tiene la culpa: se sigue intentando cada 3 horas sin agotar
    // los intentos, para que el aviso salga en cuanto se arregle. Solo un fallo propio del mensaje los agota.
    public static TimeSpan? Espera(int intentosRealizados, int maximoIntentos, bool problemaDelServidor = false)
    {
        if (intentosRealizados >= maximoIntentos && !problemaDelServidor)
            return null;
        return Esperas[Math.Clamp(intentosRealizados - 1, 0, Esperas.Length - 1)];
    }
}

// Telefono chileno de 9 digitos, para mostrarlo legible y ofrecer llamar o escribir por WhatsApp.
public sealed record TelefonoChile(string Legible, string Internacional, bool EsCelular)
{
    public static TelefonoChile? Interpretar(string? telefono)
    {
        if (!Reglas.EsTelefono(telefono))
            return null;

        var digitos = new string(telefono!.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length == 11 && digitos.StartsWith("56", StringComparison.Ordinal))
            digitos = digitos[2..];

        // Los celulares empiezan con 9 (+56 9 1234 5678); los fijos, con el codigo de area (+56 2 2234 5678).
        var legible = $"+56 {digitos[0]} {digitos[1..5]} {digitos[5..]}";
        return new TelefonoChile(legible, $"+56{digitos}", digitos[0] == '9');
    }

    public string EnlaceWhatsApp => $"https://wa.me/{Internacional[1..]}";
    public string EnlaceLlamada => $"tel:{Internacional}";
}

// Fechas y horas como se escriben en Chile (RNFO05).
public static class FormatoChile
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CL");

    public static string Fecha(DateTime fecha) => fecha.ToString("dd-MM-yyyy", Cultura);
    public static string Hora(DateTime fecha) => fecha.ToString("HH:mm", Cultura);

    // Medidas guardadas en milimetros, mostradas en centimetros como en la portada.
    public static string Centimetros(int milimetros) => (milimetros / 10m).ToString("0.#", Cultura);
}
