using System.Security.Cryptography;
using System.Text;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Correo;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Consentimientos;
using LetrerosHerreraSYNCTIMEapi.Negocio.Correo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 6: cuentas y sesion (RFS03, RNFP06-08, RNFP13) ──

// Quien tiene la sesion: un cliente o un empleado, nunca los dos.
public sealed record TitularSesion(int? IdCliente, int? IdEmpleado)
{
    public static TitularSesion DeCliente(int id) => new(id, null);
    public static TitularSesion DeEmpleado(int id) => new(null, id);
    public bool EsCliente => IdCliente is not null;
}

public sealed record SesionEmitida(string TokenRenovacion, DateTimeOffset Expira);

public abstract record ResultadoRenovacion
{
    public sealed record Renovada(TitularSesion Titular, SesionEmitida Sesion) : ResultadoRenovacion;
    // Otra pestaña acaba de renovar con este mismo token: no es un robo, se reintenta con la cookie nueva.
    public sealed record EnCurso : ResultadoRenovacion;
    public sealed record Rechazada : ResultadoRenovacion;
}

internal static class Secretos
{
    // 256 bits aleatorios en base64url: el valor de la cookie o del enlace.
    public static string Nuevo() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    public static bool IgualesEnTiempoConstante(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}

// Tokens de renovacion: rotan en cada uso y una reutilizacion revoca la sesion entera.
public sealed class ServicioSesiones(LetrerosHerreradbContext db, IConfiguration configuracion, TimeProvider reloj, ILogger<ServicioSesiones> logger)
{
    // Dos pestañas que renuevan a la vez presentan el mismo token: la segunda llega a lo mas unos segundos despues.
    private static readonly TimeSpan GraciaRenovacionSimultanea = TimeSpan.FromSeconds(10);

    public TimeSpan DuracionMaxima(TitularSesion titular) => titular.EsCliente
        ? TimeSpan.FromDays(Math.Max(1, configuracion.GetValue("Sesion:DiasCliente", 7)))
        : TimeSpan.FromHours(Math.Max(1, configuracion.GetValue("Sesion:HorasPersonal", 12)));

    public Task<SesionEmitida> IniciarAsync(TitularSesion titular, CancellationToken ct) =>
        EmitirAsync(titular, Guid.NewGuid(), reloj.GetUtcNow().Add(DuracionMaxima(titular)), ct);

    public async Task<ResultadoRenovacion> RenovarAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return new ResultadoRenovacion.Rechazada();

        var hash = Secretos.Hash(token);
        var ahora = reloj.GetUtcNow();

        // Marcarlo como usado es atomico: de dos peticiones con el mismo token, solo una lo consigue.
        var tomados = await db.TokensRenovacion
            .Where(t => t.Hash == hash && t.FechaUso == null && t.FechaRevocacion == null && t.FechaExpiracion > ahora)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaUso, ahora), ct);

        var registro = await db.TokensRenovacion.AsNoTracking().FirstOrDefaultAsync(t => t.Hash == hash, ct);
        if (registro is null)
            return new ResultadoRenovacion.Rechazada();

        if (tomados == 0)
        {
            if (registro.FechaRevocacion is null && registro.FechaUso is { } usado && ahora - usado <= GraciaRenovacionSimultanea)
                return new ResultadoRenovacion.EnCurso();
            if (registro.FechaRevocacion is null && registro.FechaUso is not null)
            {
                // Un token ya usado volvio a aparecer: alguien tiene una copia. Se cierra esa sesion completa.
                logger.LogWarning("Se reutilizo un token de renovacion ya usado (sesion {Familia}): se cierra esa sesion por seguridad.", registro.Familia);
                await RevocarFamiliaAsync(registro.Familia, ct);
            }
            return new ResultadoRenovacion.Rechazada();
        }

        var titular = new TitularSesion(registro.IdCliente, registro.IdEmpleado);
        if (!await CuentaActivaAsync(titular, ct))
        {
            await RevocarFamiliaAsync(registro.Familia, ct);
            return new ResultadoRenovacion.Rechazada();
        }
        return new ResultadoRenovacion.Renovada(titular, await EmitirAsync(titular, registro.Familia, registro.FechaExpiracion, ct));
    }

    // Cierre de sesion: revoca la sesion de esta cookie (las de otros equipos siguen abiertas).
    public async Task CerrarAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return;
        var hash = Secretos.Hash(token);
        var familia = await db.TokensRenovacion.Where(t => t.Hash == hash).Select(t => (Guid?)t.Familia).FirstOrDefaultAsync(ct);
        if (familia is Guid f)
            await RevocarFamiliaAsync(f, ct);
    }

    // Al cambiar o restablecer la contraseña, o al desactivar la cuenta: se cierran todas sus sesiones.
    public Task<int> RevocarTodasAsync(TitularSesion titular, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var consulta = titular.EsCliente
            ? db.TokensRenovacion.Where(t => t.IdCliente == titular.IdCliente)
            : db.TokensRenovacion.Where(t => t.IdEmpleado == titular.IdEmpleado);
        return consulta.Where(t => t.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaRevocacion, ahora), ct);
    }

    private Task<int> RevocarFamiliaAsync(Guid familia, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        return db.TokensRenovacion.Where(t => t.Familia == familia && t.FechaRevocacion == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaRevocacion, ahora), ct);
    }

    private Task<bool> CuentaActivaAsync(TitularSesion titular, CancellationToken ct) => titular.EsCliente
        ? db.Clientes.AnyAsync(c => c.IdCliente == titular.IdCliente && c.Activo && c.PasswordHash != null, ct)
        : db.Empleados.AnyAsync(e => e.IdEmpleado == titular.IdEmpleado && e.Activo, ct);

    private async Task<SesionEmitida> EmitirAsync(TitularSesion titular, Guid familia, DateTimeOffset expira, CancellationToken ct)
    {
        var token = Secretos.Nuevo();
        db.TokensRenovacion.Add(new TokenRenovacion
        {
            IdCliente = titular.IdCliente,
            IdEmpleado = titular.IdEmpleado,
            Hash = Secretos.Hash(token),
            Familia = familia,
            FechaCreacion = reloj.GetUtcNow(),
            FechaExpiracion = expira
        });
        await db.SaveChangesAsync(ct);
        return new SesionEmitida(token, expira);
    }
}

// ── Correo verificado y recuperacion de la contraseña ──

public abstract record ResultadoRestablecer
{
    public sealed record Restablecida(TitularSesion Titular) : ResultadoRestablecer;
    public sealed record Invalido : ResultadoRestablecer;
    public sealed record PasswordInvalida : ResultadoRestablecer;
}

public sealed class ServicioCodigosCuenta(
    LetrerosHerreradbContext db,
    IServicioCorreo correo,
    IOptions<OpcionesCorreo> opcionesCorreo,
    IConfiguration configuracion,
    AuthService auth,
    ServicioSesiones sesiones,
    TimeProvider reloj,
    ILogger<ServicioCodigosCuenta> logger)
{
    public const string TipoCliente = "cliente";
    public const string TipoPersonal = "personal";
    public const int MinutosRecuperacion = 15;
    public const int MaxIntentosRecuperacion = 5;
    public static readonly TimeSpan VigenciaVerificacion = TimeSpan.FromHours(24);
    // Contra quien pide codigos en bucle para llenar el correo de otro: uno por minuto y 5 por hora como maximo.
    private static readonly TimeSpan EsperaEntreCodigos = TimeSpan.FromMinutes(1);
    public const int MaxCodigosPorHora = 5;

    public const string MensajeCodigoInvalido = "El código no es válido o ya venció. Pide uno nuevo.";

    public static string NormalizarCorreo(string? correo) => (correo ?? "").Trim().ToLowerInvariant();

    // Envia el enlace para confirmar el correo actual del cliente. Reemplaza los enlaces anteriores. Nunca
    // lanza: la cuenta ya quedo guardada y el cliente puede pedir el enlace de nuevo desde su cuenta.
    public async Task EnviarVerificacionAsync(Cliente cliente, CancellationToken ct)
    {
        try
        {
            await EnviarVerificacionSinCapturarAsync(cliente, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "No se pudo preparar el enlace para confirmar el correo del cliente {IdCliente}.", cliente.IdCliente);
        }
    }

    private async Task EnviarVerificacionSinCapturarAsync(Cliente cliente, CancellationToken ct)
    {
        if (cliente.Email is null)
            return;
        var ahora = reloj.GetUtcNow();
        await db.CodigosCuenta
            .Where(c => c.IdCliente == cliente.IdCliente && c.Proposito == CodigoCuenta.VerificacionCorreo && c.FechaUso == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaUso, ahora), ct);

        var token = Secretos.Nuevo();
        db.CodigosCuenta.Add(new CodigoCuenta
        {
            IdCliente = cliente.IdCliente,
            Proposito = CodigoCuenta.VerificacionCorreo,
            Hash = Secretos.Hash(token),
            Correo = cliente.Email,
            FechaCreacion = ahora,
            FechaExpiracion = ahora.Add(VigenciaVerificacion)
        });
        await db.SaveChangesAsync(ct);

        if (UrlSitio() is not { } sitio)
        {
            logger.LogError("Falta Sitio:UrlPublica: no se pudo enviar el enlace para confirmar el correo. El cliente puede pedirlo de nuevo desde su cuenta.");
            return;
        }
        var enlace = $"{sitio}/cuenta/verificar?token={Uri.EscapeDataString(token)}";
        await EncolarAsync(CorreosDeCuenta.Verificacion(cliente.Email, cliente.Nombre, enlace, opcionesCorreo.Value.ContactoTaller), ct);
    }

    // true si el enlace era valido y el correo quedo confirmado.
    public async Task<bool> VerificarCorreoAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return false;
        var hash = Secretos.Hash(token);
        var ahora = reloj.GetUtcNow();
        var codigo = await db.CodigosCuenta.Include(c => c.Cliente)
            .FirstOrDefaultAsync(c => c.Hash == hash && c.Proposito == CodigoCuenta.VerificacionCorreo, ct);
        // El enlace vale para el correo al que se envio: si el cliente cambio su correo despues, ya no sirve.
        if (codigo?.Cliente is not { } cliente || codigo.FechaUso is not null || codigo.FechaExpiracion <= ahora
            || !string.Equals(cliente.Email, codigo.Correo, StringComparison.OrdinalIgnoreCase))
            return false;

        codigo.FechaUso = ahora;
        cliente.FechaVerificacionCorreo ??= ahora;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // Siempre termina igual, exista o no la cuenta: la respuesta no revela quien tiene cuenta.
    public async Task SolicitarRecuperacionAsync(string tipo, string? correoEscrito, CancellationToken ct)
    {
        var correoNormalizado = NormalizarCorreo(correoEscrito);
        if (!Reglas.EsCorreo(correoNormalizado))
            return;
        var cuenta = await BuscarCuentaAsync(tipo, correoNormalizado, ct);
        if (cuenta is null)
            return;

        var ahora = reloj.GetUtcNow();
        var haceUnaHora = ahora.AddHours(-1);
        var recientes = await DelTitular(cuenta.Titular).Where(c => c.Proposito == CodigoCuenta.Recuperacion && c.FechaCreacion > haceUnaHora)
            .Select(c => c.FechaCreacion).ToListAsync(ct);
        if (recientes.Count >= MaxCodigosPorHora || recientes.Any(creado => ahora - creado < EsperaEntreCodigos))
            return;

        await Pendientes(cuenta.Titular, CodigoCuenta.Recuperacion).ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaUso, ahora), ct);
        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.CodigosCuenta.Add(new CodigoCuenta
        {
            IdCliente = cuenta.Titular.IdCliente,
            IdEmpleado = cuenta.Titular.IdEmpleado,
            Proposito = CodigoCuenta.Recuperacion,
            Hash = HashCodigo(cuenta.Correo, codigo),
            Correo = cuenta.Correo,
            FechaCreacion = ahora,
            FechaExpiracion = ahora.AddMinutes(MinutosRecuperacion)
        });
        await db.SaveChangesAsync(ct);
        await EncolarAsync(CorreosDeCuenta.Recuperacion(cuenta.Correo, cuenta.Nombre, codigo, MinutosRecuperacion, opcionesCorreo.Value.ContactoTaller), ct);
    }

    public async Task<ResultadoRestablecer> RestablecerAsync(string tipo, string? correoEscrito, string? codigoEscrito, string? passwordNueva, CancellationToken ct)
    {
        if (!AuthService.EsPasswordValida(passwordNueva))
            return new ResultadoRestablecer.PasswordInvalida();
        var codigoLimpio = (codigoEscrito ?? "").Trim();
        var correoNormalizado = NormalizarCorreo(correoEscrito);
        if (codigoLimpio.Length != 6 || !codigoLimpio.All(char.IsAsciiDigit) || !Reglas.EsCorreo(correoNormalizado))
            return new ResultadoRestablecer.Invalido();

        var cuenta = await BuscarCuentaAsync(tipo, correoNormalizado, ct);
        if (cuenta is null)
            return new ResultadoRestablecer.Invalido();

        var ahora = reloj.GetUtcNow();
        // Solo vale un codigo enviado al correo actual de la cuenta: si el correo cambio, los anteriores no sirven.
        var registro = await Pendientes(cuenta.Titular, CodigoCuenta.Recuperacion)
            .Where(c => c.FechaExpiracion > ahora && c.Correo == cuenta.Correo)
            .OrderByDescending(c => c.FechaCreacion)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        if (registro is null)
            return new ResultadoRestablecer.Invalido();

        // El intento se reserva en la base ANTES de comparar: aunque lleguen cien peticiones a la vez, solo
        // cinco llegan a probar un codigo.
        var reservado = await db.CodigosCuenta
            .Where(c => c.IdCodigo == registro.IdCodigo && c.FechaUso == null && c.Intentos < MaxIntentosRecuperacion)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Intentos, c => c.Intentos + 1), ct);
        if (reservado == 0)
            return new ResultadoRestablecer.Invalido();

        if (!Secretos.IgualesEnTiempoConstante(registro.Hash, HashCodigo(registro.Correo, codigoLimpio)))
        {
            // Pasados los intentos, el codigo deja de servir aunque luego se escriba bien.
            await db.CodigosCuenta
                .Where(c => c.IdCodigo == registro.IdCodigo && c.FechaUso == null && c.Intentos >= MaxIntentosRecuperacion)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaUso, ahora), ct);
            return new ResultadoRestablecer.Invalido();
        }

        var hash = auth.HashPassword(passwordNueva!);
        // Consumir el codigo, cambiar la contraseña y cerrar las sesiones van juntos: si algo falla, nada cambia.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var consumido = await db.CodigosCuenta.Where(c => c.IdCodigo == registro.IdCodigo && c.FechaUso == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaUso, ahora), ct);
        if (consumido == 0)
            return new ResultadoRestablecer.Invalido();

        if (cuenta.Titular.EsCliente)
        {
            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == cuenta.Titular.IdCliente && c.Activo, ct);
            if (cliente is null)
                return new ResultadoRestablecer.Invalido();
            cliente.PasswordHash = hash;
            // Recibir el codigo en ese correo prueba que es suyo.
            if (string.Equals(cliente.Email, registro.Correo, StringComparison.OrdinalIgnoreCase))
                cliente.FechaVerificacionCorreo ??= ahora;
        }
        else
        {
            var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.IdEmpleado == cuenta.Titular.IdEmpleado && e.Activo, ct);
            if (empleado is null)
                return new ResultadoRestablecer.Invalido();
            empleado.PasswordHash = hash;
        }
        await db.SaveChangesAsync(ct);
        await sesiones.RevocarTodasAsync(cuenta.Titular, ct);
        await transaccion.CommitAsync(ct);
        return new ResultadoRestablecer.Restablecida(cuenta.Titular);
    }

    private sealed record CuentaEncontrada(TitularSesion Titular, string Correo, string Nombre);

    private async Task<CuentaEncontrada?> BuscarCuentaAsync(string tipo, string correoNormalizado, CancellationToken ct)
    {
        if (tipo == TipoCliente)
            return await db.Clientes.AsNoTracking()
                .Where(c => c.Email == correoNormalizado && c.Activo && c.PasswordHash != null)
                .Select(c => new CuentaEncontrada(TitularSesion.DeCliente(c.IdCliente), c.Email!, c.Nombre))
                .FirstOrDefaultAsync(ct);
        if (tipo == TipoPersonal)
            return await db.Empleados.AsNoTracking()
                .Where(e => e.Email == correoNormalizado && e.Activo)
                .Select(e => new CuentaEncontrada(TitularSesion.DeEmpleado(e.IdEmpleado), e.Email, e.Nombre))
                .FirstOrDefaultAsync(ct);
        return null;
    }

    private IQueryable<CodigoCuenta> DelTitular(TitularSesion titular) => titular.EsCliente
        ? db.CodigosCuenta.Where(c => c.IdCliente == titular.IdCliente)
        : db.CodigosCuenta.Where(c => c.IdEmpleado == titular.IdEmpleado);

    private IQueryable<CodigoCuenta> Pendientes(TitularSesion titular, string proposito) =>
        DelTitular(titular).Where(c => c.Proposito == proposito && c.FechaUso == null);

    // Al cambiar el correo: los codigos enviados al correo anterior dejan de servir.
    public Task<int> AnularPendientesAsync(TitularSesion titular, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        return DelTitular(titular).Where(c => c.FechaUso == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.FechaUso, ahora), ct);
    }

    // Aviso al correo anterior cuando la cuenta cambia de correo: si no fue el titular, se entera.
    public Task AvisarCambioDeCorreoAsync(string correoAnterior, string nombre, CancellationToken ct) =>
        EncolarAsync(CorreosDeCuenta.AvisoCambioCorreo(correoAnterior, nombre, opcionesCorreo.Value.ContactoTaller), ct);

    // El codigo tiene solo 6 digitos: se mezcla con una clave del servidor para que el hash guardado no
    // se pueda revertir probando el millon de combinaciones sin esa clave.
    private string HashCodigo(string correoDestino, string codigo)
    {
        var clave = Encoding.UTF8.GetBytes(configuracion["Jwt:Key"] ?? throw new InvalidOperationException("No se configuro Jwt:Key."));
        return Convert.ToHexStringLower(HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes($"recuperacion:{NormalizarCorreo(correoDestino)}:{codigo}")));
    }

    private string? UrlSitio()
    {
        var url = configuracion["Sitio:UrlPublica"];
        return string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _) ? null : url.TrimEnd('/');
    }

    // Encolar es solo insertar una fila; si falla, se registra y la cuenta sigue funcionando (se puede reenviar).
    private async Task EncolarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        try
        {
            await correo.EncolarAsync(mensaje, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "No se pudo dejar en la cola el correo de cuenta ({Tipo}).", mensaje.Tipo);
        }
    }
}

// ── Registro y datos del cliente (RFU04, E01, E23, RNFE01) ──

public sealed record DatosRegistro(
    string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Correo, string? Direccion, string? Password,
    bool AceptaTerminosYPrivacidad, bool AceptaMarketing, string? VersionTerminos, string? VersionPrivacidad, string? VersionMarketing);

public sealed record DatosPerfil(string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Direccion);

// Fase 11: AvisosEstado, la preferencia de los avisos de estado (los de cuenta y seguridad llegan siempre).
public sealed record MiCuenta(
    int IdCliente, string Nombre, string Apellido, string? Rut, string Telefono, string Correo, string? Direccion,
    bool CorreoVerificado, bool Marketing, bool AvisosEstado);

public abstract record ResultadoCuenta
{
    public sealed record Lista(Cliente Cliente) : ResultadoCuenta;
    public sealed record Invalida(IReadOnlyDictionary<string, string[]> Errores) : ResultadoCuenta;
    public sealed record Conflicto(string Mensaje) : ResultadoCuenta;
    public sealed record NoEncontrada : ResultadoCuenta;
}

public sealed class ServicioMiCuenta(
    LetrerosHerreradbContext db,
    IServicioConsentimientos consentimientos,
    ServicioCodigosCuenta codigos,
    ServicioSesiones sesiones,
    AuthService auth,
    TimeProvider reloj)
{
    public const string MensajeTelefonoDelTaller =
        "Ese teléfono o ese RUT ya están registrados en el taller. Pide en el local que activen tu acceso web: así nadie puede quedarse con tus pedidos sabiendo solo tu número.";

    public async Task<ResultadoCuenta> RegistrarAsync(DatosRegistro datos, CancellationToken ct)
    {
        var errores = ValidacionCuenta.Validar(datos.Rut, datos.Nombre, datos.Apellido, datos.Telefono, datos.Direccion);
        var correo = ServicioCodigosCuenta.NormalizarCorreo(datos.Correo);
        ValidacionCuenta.ValidarCorreo(correo, errores);
        if (!AuthService.EsPasswordValida(datos.Password))
            errores["password"] = [AuthService.MensajePasswordInvalida];
        if (!datos.AceptaTerminosYPrivacidad)
            errores["aceptaTerminosYPrivacidad"] = ["Para crear la cuenta debes aceptar los términos y condiciones y la política de privacidad."];
        if (errores.Count > 0)
            return new ResultadoCuenta.Invalida(errores);

        if (datos.VersionTerminos != DocumentosLegales.VersionVigente(DocumentosLegales.Terminos)
            || datos.VersionPrivacidad != DocumentosLegales.VersionVigente(DocumentosLegales.Privacidad)
            || (datos.AceptaMarketing && datos.VersionMarketing != DocumentosLegales.VersionVigente(DocumentosLegales.Marketing)))
            return new ResultadoCuenta.Conflicto("Los términos o la política de privacidad cambiaron mientras llenabas el formulario. Vuelve a leerlos y envía de nuevo.");

        var telefono = ValidacionCuenta.NormalizarTelefono(datos.Telefono)!;
        var rut = ValidacionCuenta.NormalizarRut(datos.Rut);
        if (await db.Clientes.AnyAsync(c => c.Email == correo, ct))
            return new ResultadoCuenta.Conflicto("Ya existe una cuenta con ese correo. Ingresa o recupera tu contraseña.");
        // Telefono y RUT dan el mismo mensaje: no se confirma cual de los dos dato ya conoce el taller.
        if (await db.Clientes.AnyAsync(c => c.Telefono == telefono || (rut != null && c.Rut == rut), ct))
            return new ResultadoCuenta.Conflicto(MensajeTelefonoDelTaller);

        var cliente = new Cliente
        {
            Rut = rut,
            Nombre = datos.Nombre!.Trim(),
            Apellido = datos.Apellido!.Trim(),
            Telefono = telefono,
            Email = correo,
            Direccion = string.IsNullOrWhiteSpace(datos.Direccion) ? null : datos.Direccion.Trim(),
            PasswordHash = auth.HashPassword(datos.Password!)
        };

        // La cuenta y sus consentimientos se guardan juntos: si uno falla, no queda ninguno.
        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            db.Clientes.Add(cliente);
            await db.SaveChangesAsync(ct);
            var titular = TitularConsentimiento.Cliente(cliente.IdCliente);
            foreach (var (tipo, version) in new[] { (DocumentosLegales.Terminos, datos.VersionTerminos!), (DocumentosLegales.Privacidad, datos.VersionPrivacidad!) })
                if ((await consentimientos.RegistrarAsync(titular, tipo, version, ct)).Error is { } error)
                    throw new InvalidOperationException($"No se pudo registrar el consentimiento de {tipo}: {error}");
            if (datos.AceptaMarketing && (await consentimientos.RegistrarAsync(titular, DocumentosLegales.Marketing, datos.VersionMarketing!, ct)).Error is { } errorMarketing)
                throw new InvalidOperationException($"No se pudo registrar el consentimiento de marketing: {errorMarketing}");
            await transaccion.CommitAsync(ct);
        }

        await codigos.EnviarVerificacionAsync(cliente, CancellationToken.None);
        return new ResultadoCuenta.Lista(cliente);
    }

    public async Task<MiCuenta?> ObtenerAsync(int idCliente, CancellationToken ct)
    {
        // Una cuenta dada de baja ya no existe para su titular (aunque su token aun no venza).
        var cliente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente is null)
            return null;
        var marketing = await consentimientos.VigenteAsync(TitularConsentimiento.Cliente(idCliente), DocumentosLegales.Marketing, ct) is not null;
        return new MiCuenta(cliente.IdCliente, cliente.Nombre, cliente.Apellido, cliente.Rut, cliente.Telefono, cliente.Email ?? "",
            cliente.Direccion, cliente.FechaVerificacionCorreo is not null, marketing, cliente.RecibeAvisosEstado);
    }

    // Fase 11: PUT /mi-cuenta/preferencias. Cada cambio queda como consentimiento con fecha, hora y version. El
    // marketing se acepta con la version que leyo el cliente (o la vigente, si no la indica). null si se pudo.
    public async Task<string?> PreferenciasAsync(int idCliente, bool avisosEstado, bool marketing, string? versionMarketing, CancellationToken ct)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente is null)
            return null;
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await PreferenciaAvisosEstado.CambiarAsync(db, consentimientos, cliente, avisosEstado, reloj, ct);
        if (await MarketingAsync(idCliente, marketing, versionMarketing ?? DocumentosLegales.VersionVigente(DocumentosLegales.Marketing), ct) is { } error)
            return error;
        await transaccion.CommitAsync(CancellationToken.None);
        return null;
    }

    public async Task<ResultadoCuenta> ActualizarAsync(int idCliente, DatosPerfil datos, CancellationToken ct)
    {
        var errores = ValidacionCuenta.Validar(datos.Rut, datos.Nombre, datos.Apellido, datos.Telefono, datos.Direccion);
        if (errores.Count > 0)
            return new ResultadoCuenta.Invalida(errores);
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente is null)
            return new ResultadoCuenta.NoEncontrada();

        var telefono = ValidacionCuenta.NormalizarTelefono(datos.Telefono)!;
        var rut = ValidacionCuenta.NormalizarRut(datos.Rut);
        if (telefono != cliente.Telefono && await db.Clientes.AnyAsync(c => c.Telefono == telefono && c.IdCliente != idCliente, ct))
            return new ResultadoCuenta.Conflicto("Ese teléfono ya está registrado por otra persona. Si es tuyo, avísanos en el local.");
        if (rut is not null && rut != cliente.Rut && await db.Clientes.AnyAsync(c => c.Rut == rut && c.IdCliente != idCliente, ct))
            return new ResultadoCuenta.Conflicto("Ese RUT ya está registrado por otra persona. Si es tuyo, avísanos en el local.");

        cliente.Nombre = datos.Nombre!.Trim();
        cliente.Apellido = datos.Apellido!.Trim();
        cliente.Telefono = telefono;
        // El RUT es opcional: el cliente puede corregirlo o quitarlo.
        cliente.Rut = rut;
        cliente.Direccion = string.IsNullOrWhiteSpace(datos.Direccion) ? null : datos.Direccion.Trim();
        await db.SaveChangesAsync(ct);
        return new ResultadoCuenta.Lista(cliente);
    }

    // Cambiar el correo exige la contraseña y deja la cuenta sin verificar hasta confirmar el nuevo.
    public async Task<ResultadoCuenta> CambiarCorreoAsync(int idCliente, string? correoNuevo, string? passwordActual, CancellationToken ct)
    {
        var correo = ServicioCodigosCuenta.NormalizarCorreo(correoNuevo);
        var errores = new Dictionary<string, string[]>();
        ValidacionCuenta.ValidarCorreo(correo, errores);
        if (errores.Count > 0)
            return new ResultadoCuenta.Invalida(errores);
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente?.PasswordHash is null)
            return new ResultadoCuenta.NoEncontrada();
        if (string.IsNullOrEmpty(passwordActual) || !auth.VerifyPassword(passwordActual, cliente.PasswordHash))
            return new ResultadoCuenta.Invalida(new Dictionary<string, string[]> { ["passwordActual"] = [MensajePasswordActual] });
        if (string.Equals(correo, cliente.Email, StringComparison.OrdinalIgnoreCase))
            return new ResultadoCuenta.Lista(cliente);
        if (await db.Clientes.AnyAsync(c => c.Email == correo && c.IdCliente != idCliente, ct))
            return new ResultadoCuenta.Conflicto("Ese correo ya está en uso en otra cuenta.");

        var anterior = cliente.Email;
        cliente.Email = correo;
        cliente.FechaVerificacionCorreo = null;
        await db.SaveChangesAsync(ct);
        // Los codigos enviados al correo anterior ya no sirven, y ese correo recibe un aviso del cambio.
        await codigos.AnularPendientesAsync(TitularSesion.DeCliente(idCliente), ct);
        if (anterior is not null)
            await codigos.AvisarCambioDeCorreoAsync(anterior, cliente.Nombre, CancellationToken.None);
        await codigos.EnviarVerificacionAsync(cliente, CancellationToken.None);
        return new ResultadoCuenta.Lista(cliente);
    }

    public const string MensajePasswordActual = "La contraseña actual no es correcta.";
    public const string MensajeCuentaDadaDeBaja = "Esta cuenta fue dada de baja.";

    public sealed record ResultadoPassword(Cliente? Cliente, IReadOnlyDictionary<string, string[]>? Errores);

    // Cambia la contraseña y cierra todas las sesiones de la cuenta en una sola transaccion.
    public async Task<ResultadoPassword> CambiarPasswordAsync(int idCliente, string? actual, string? nueva, CancellationToken ct)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente?.PasswordHash is null || string.IsNullOrEmpty(actual) || !auth.VerifyPassword(actual, cliente.PasswordHash))
            return new(null, new Dictionary<string, string[]> { ["passwordActual"] = [MensajePasswordActual] });
        if (!AuthService.EsPasswordValida(nueva))
            return new(null, new Dictionary<string, string[]> { ["passwordNueva"] = [AuthService.MensajePasswordInvalida] });

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        cliente.PasswordHash = auth.HashPassword(nueva!);
        await db.SaveChangesAsync(ct);
        await sesiones.RevocarTodasAsync(TitularSesion.DeCliente(idCliente), ct);
        await transaccion.CommitAsync(ct);
        return new(cliente, null);
    }

    // Aceptar o retirar el marketing en cualquier momento (RFU04: separado de los terminos). null si se pudo.
    public async Task<string?> MarketingAsync(int idCliente, bool acepta, string? version, CancellationToken ct)
    {
        // Revision de la fase 11: una cuenta dada de baja no vuelve a aceptar nada (su token puede no haber vencido).
        if (acepta && !await db.Clientes.AnyAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct))
            return MensajeCuentaDadaDeBaja;
        var titular = TitularConsentimiento.Cliente(idCliente);
        if (!acepta)
        {
            await consentimientos.RevocarAsync(titular, DocumentosLegales.Marketing, ct);
            return null;
        }
        return (await consentimientos.RegistrarAsync(titular, DocumentosLegales.Marketing, version ?? "", ct)).Error;
    }
}
