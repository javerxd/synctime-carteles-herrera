using System.Globalization;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using LetrerosHerreraSYNCTIMEapi.Infraestructura.Google;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.IngresoGoogle;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 14A: ingreso con Google (RFS03, RFS12, RFU05, RFU06, RNFE02, RNFP17) ──

public abstract record ResultadoGoogle
{
    // El cliente ya tiene su sesion (ingreso por su Google, o cuenta recien creada al completar el registro).
    public sealed record Sesion(Cliente Cliente) : ResultadoGoogle;
    // Fase 14C: el empleado ya tiene su sesion (su Google estaba vinculado). Es la misma sesion de personal que da la contraseña.
    public sealed record SesionPersonal(Empleado Empleado) : ResultadoGoogle;
    // Fase 14C: el correo es de un empleado que aun no vinculo su Google: ingresa con su contraseña y lo vincula desde Mi perfil.
    public sealed record CuentaPersonalExistente : ResultadoGoogle;
    // Estado de Mi perfil (GET /mi-perfil/google).
    public sealed record EstadoGoogle(bool Vinculado, string? Correo) : ResultadoGoogle;
    // Correo nuevo: todavia no se creo nada; la persona completa su registro con este pase.
    public sealed record RegistroPendiente(string Pase, DateTimeOffset VenceEn, IdentidadGoogle Identidad) : ResultadoGoogle;
    public sealed record CuentaExistente : ResultadoGoogle;
    // El token, el nonce o la cuenta no sirven. Una sola respuesta para todos los motivos: no se revela cual fue.
    public sealed record TokenInvalido : ResultadoGoogle;
    public sealed record PaseInvalido : ResultadoGoogle;
    public sealed record NoDisponible : ResultadoGoogle;
    public sealed record NoConfigurado : ResultadoGoogle;
    public sealed record Invalido(IReadOnlyDictionary<string, string[]> Errores, string Mensaje) : ResultadoGoogle;
    public sealed record Conflicto(string Codigo, string Mensaje) : ResultadoGoogle;
    public sealed record NoEncontrado : ResultadoGoogle;
    public sealed record Vinculada(string Correo) : ResultadoGoogle;
    public sealed record Desvinculada : ResultadoGoogle;
    // Fase 14C: el empleado desvinculo su Google. Se cerraron TODAS sus sesiones (si su Google estaba comprometido, la del atacante tambien):
    // el endpoint le abre una nueva en este equipo, como al cambiar la contraseña.
    public sealed record DesvinculadaPersonal(Empleado Empleado) : ResultadoGoogle;
}

// Una violacion de indice unico de SQL Server (2601 o 2627): el unico fallo de la base que se explica como «ya existe». El nombre del
// indice dice cual fue. Cualquier otro error (deadlock, tiempo agotado, FK) se relanza y lo registra el manejador de errores.
internal static class ViolacionUnica
{
    public static bool Es(DbUpdateException error) => error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    public static bool Contiene(DbUpdateException error, string indice) =>
        error.InnerException?.Message.Contains(indice, StringComparison.OrdinalIgnoreCase) == true;
}

public sealed record DatosCompletarGoogle(
    string? Pase, string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Direccion,
    bool AceptaTerminosYPrivacidad, bool AceptaMarketing, string? VersionTerminos, string? VersionPrivacidad, string? VersionMarketing);

public sealed class ServicioGoogle(
    LetrerosHerreradbContext db,
    IOptions<OpcionesGoogle> opciones,
    IVerificadorGoogle verificador,
    NoncesGoogle nonces,
    PaseGoogle pases,
    ServicioMiCuenta cuentas,
    AuthService auth,
    ServicioSesiones sesiones,
    IAuditoria auditoria,
    IActorActual actores,
    TimeProvider reloj,
    ILogger<ServicioGoogle> logger)
{
    public const string MensajeTokenInvalido = "No pudimos verificar tu cuenta de Google. Inténtalo de nuevo.";
    public const string MensajePaseInvalido = "El registro con Google venció o no es válido. Vuelve a ingresar con Google.";
    public const string MensajeNoDisponible = "Google no responde en este momento. Inténtalo de nuevo en unos minutos o ingresa con tu contraseña.";
    public const string MensajeNoConfigurado = "El ingreso con Google no está disponible. Ingresa con tu contraseña.";
    public const string MensajeCuentaExistente =
        "Ya tienes una cuenta con este correo. Ingresa con tu contraseña y vincula Google desde Mi cuenta. Si no la recuerdas, usa «Olvidé mi contraseña».";
    public const string MensajeCuentaPersonalExistente =
        "Este correo es de una cuenta del personal. Ingresa con tu contraseña y vincula Google desde Mi perfil.";
    public const string MensajeSinContrasena = "Tu cuenta no tiene contraseña: crea una antes de desvincular Google, o te quedarías sin forma de entrar.";

    public bool Configurado => opciones.Value.Configurado;

    // null si hay demasiados pendientes a la vez.
    public NonceEmitido? EmitirNonce() => nonces.Emitir();

    // ── POST /auth/google ──

    public async Task<ResultadoGoogle> IngresarAsync(string? idToken, string? nonce, CancellationToken ct)
    {
        if (!Configurado)
            return new ResultadoGoogle.NoConfigurado();
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(nonce))
            return new ResultadoGoogle.Invalido(new Dictionary<string, string[]> { ["idToken"] = ["Falta el token de Google."] }, "Falta el token de Google.");

        var (identidad, error) = await VerificarAsync(idToken, nonce, ct);
        if (identidad is null)
            return error!;

        var vinculada = await db.IdentidadesExternas.Include(i => i.Cliente).Include(i => i.Empleado)
            .FirstOrDefaultAsync(i => i.Proveedor == ReglasGoogle.Proveedor && i.Sub == identidad.Sub, ct);
        // Fase 14C: la identidad es de un cliente o de un empleado. Un empleado entra solo si sigue activo.
        var habilitada = vinculada?.Cliente is { Activo: true, InhabilitadoEn: null } || vinculada?.Empleado is { Activo: true };
        var conElCorreo = vinculada is not null ? null : await db.Clientes.AsNoTracking().Where(c => c.Email == identidad.Correo)
            .Select(c => new { c.Activo, c.InhabilitadoEn, TieneAccesoWeb = c.PasswordHash != null || c.IdentidadesExternas.Any() })
            .FirstOrDefaultAsync(ct);
        if (conElCorreo is { } existente)
        {
            // Una cuenta dada de baja o desactivada no entra ni se vuelve a crear por Google: igual que un token malo, sin decir que existia.
            if (!existente.Activo || existente.InhabilitadoEn is not null)
            {
                logger.LogInformation("Ingreso con Google rechazado: el correo es de una cuenta dada de baja o desactivada.");
                return new ResultadoGoogle.TokenInvalido();
            }
            // Un cliente del local con correo pero sin acceso web: no tiene contraseña que usar ni se puede recuperar. Se le dice como activarlo.
            if (!existente.TieneAccesoWeb)
                return new ResultadoGoogle.Conflicto("sin_acceso_web", ServicioMiCuenta.MensajeTelefonoDelTaller);
        }
        var correoTieneCuenta = conElCorreo is not null;
        // Fase 14C: el correo de un empleado. Desactivado, igual que un token malo: no se revela que la cuenta existia.
        var delPersonal = vinculada is not null || correoTieneCuenta ? null
            : await db.Empleados.AsNoTracking().Where(e => e.Email == identidad.Correo).Select(e => new { e.Activo }).FirstOrDefaultAsync(ct);
        if (delPersonal is { Activo: false })
        {
            logger.LogInformation("Ingreso con Google rechazado: el correo es de un empleado desactivado.");
            return new ResultadoGoogle.TokenInvalido();
        }

        switch (ReglasGoogle.Decidir(vinculada is not null, habilitada, correoTieneCuenta, delPersonal is not null))
        {
            case CaminoGoogle.IniciarSesion:
                var ahora = reloj.GetUtcNow();
                vinculada!.FechaUltimoIngreso = ahora;
                // El CHECK de la base garantiza que la identidad es de un cliente O de un empleado.
                if (vinculada.Empleado is { } empleado)
                {
                    // Defensa extra para el personal (tiene mas privilegios): el correo verificado del token debe seguir siendo el de la ficha.
                    if (!ReglasGoogle.MismoCorreo(empleado.Email, identidad.Correo))
                    {
                        logger.LogInformation("Ingreso con Google rechazado: el correo del token ya no es el de la ficha del empleado.");
                        return new ResultadoGoogle.TokenInvalido();
                    }
                    auditoria.Anotar("empleado", empleado.IdEmpleado.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.IngresoGoogle,
                        DetalleAuditoria.Libre(new Dictionary<string, object?> { ["nueva"] = false }), ActorDe(empleado));
                    return await GuardarIngresoAsync(new ResultadoGoogle.SesionPersonal(empleado), ct);
                }
                AnotarIngreso(vinculada.Cliente!, nueva: false);
                return await GuardarIngresoAsync(new ResultadoGoogle.Sesion(vinculada.Cliente!), ct);
            case CaminoGoogle.CuentaPersonalExistente:
                logger.LogInformation("Ingreso con Google: el correo es de un empleado que no vinculo su Google; no se fusiona.");
                return new ResultadoGoogle.CuentaPersonalExistente();
            case CaminoGoogle.CuentaExistente:
                logger.LogInformation("Ingreso con Google: el correo ya tiene una cuenta; no se fusiona.");
                return new ResultadoGoogle.CuentaExistente();
            case CaminoGoogle.RegistroPendiente:
                var pase = pases.Emitir(identidad);
                return new ResultadoGoogle.RegistroPendiente(pase.Pase, pase.Vence, identidad);
            default:
                // Cuenta dada de baja o desactivada: igual que un token malo, para no revelar que existia.
                logger.LogInformation("Ingreso con Google rechazado: la cuenta vinculada no esta habilitada.");
                return new ResultadoGoogle.TokenInvalido();
        }
    }

    // ── POST /auth/google/completar ──

    public async Task<ResultadoGoogle> CompletarAsync(DatosCompletarGoogle datos, CancellationToken ct)
    {
        if (!Configurado)
            return new ResultadoGoogle.NoConfigurado();
        if (pases.Leer(datos.Pase) is not { } pase || pases.EstaUsado(pase))
            return new ResultadoGoogle.PaseInvalido();

        string Elegir(string? escrito, string deGoogle) => string.IsNullOrWhiteSpace(escrito) ? deGoogle : escrito;
        var resultado = await cuentas.RegistrarAsync(new DatosRegistro(datos.Rut, Elegir(datos.Nombre, pase.Identidad.Nombre), Elegir(datos.Apellido, pase.Identidad.Apellido),
            datos.Telefono, pase.Identidad.Correo, datos.Direccion, null, datos.AceptaTerminosYPrivacidad, datos.AceptaMarketing,
            datos.VersionTerminos, datos.VersionPrivacidad, datos.VersionMarketing), ct, pase.Identidad);

        switch (resultado)
        {
            case ResultadoCuenta.Lista lista:
                // Recien ahora el pase se gasta: con un error de validacion, la persona lo corrige y vuelve a enviar.
                pases.MarcarUsado(pase);
                // La cuenta ya esta confirmada: el registro de la bitacora no se corta si el navegador cierra la conexion.
                await auditoria.RegistrarAsync("cuenta", lista.Cliente.IdCliente.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.IngresoGoogle,
                    DetalleAuditoria.Libre(new Dictionary<string, object?> { ["nueva"] = true }), CancellationToken.None, ActorDe(lista.Cliente.IdCliente));
                return new ResultadoGoogle.Sesion(lista.Cliente);
            case ResultadoCuenta.Invalida invalida:
                return new ResultadoGoogle.Invalido(invalida.Errores, "Revisa los datos del formulario.");
            case ResultadoCuenta.Conflicto conflicto:
                return new ResultadoGoogle.Conflicto("conflicto", conflicto.Mensaje);
            default:
                throw new InvalidOperationException($"Resultado de registro no previsto: {resultado.GetType().Name}.");
        }
    }

    // ── POST /mi-cuenta/google ──

    public async Task<ResultadoGoogle> VincularAsync(int idCliente, string? idToken, string? nonce, string? passwordActual, CancellationToken ct)
    {
        if (!Configurado)
            return new ResultadoGoogle.NoConfigurado();
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente is null)
            return new ResultadoGoogle.NoEncontrado();
        // La contraseña se comprueba antes de gastar el nonce o de llamar a Google.
        if (cliente.PasswordHash is null || string.IsNullOrEmpty(passwordActual) || !auth.VerifyPassword(passwordActual, cliente.PasswordHash))
            return ErrorPassword();
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(nonce))
            return new ResultadoGoogle.Invalido(new Dictionary<string, string[]> { ["idToken"] = ["Falta el token de Google."] }, "Falta el token de Google.");
        if (await db.IdentidadesExternas.AnyAsync(i => i.IdCliente == idCliente && i.Proveedor == ReglasGoogle.Proveedor, ct))
            return new ResultadoGoogle.Conflicto("google_ya_vinculada", "Tu cuenta ya tiene un Google vinculado. Desvincúlalo antes de vincular otro.");

        var (identidad, error) = await VerificarAsync(idToken, nonce, ct);
        if (identidad is null)
            return error!;
        // Que el Google sea de la misma persona: su correo verificado debe ser el de la cuenta.
        if (!ReglasGoogle.MismoCorreo(cliente.Email, identidad.Correo))
            return new ResultadoGoogle.Conflicto("google_correo_distinto", "El correo de esa cuenta de Google no es el de tu cuenta. Usa el Google con el correo de tu cuenta.");
        if (await db.IdentidadesExternas.AnyAsync(i => i.Proveedor == ReglasGoogle.Proveedor && i.Sub == identidad.Sub, ct))
            return new ResultadoGoogle.Conflicto("google_en_uso", "Esa cuenta de Google ya está vinculada a otra cuenta.");

        var ahora = reloj.GetUtcNow();
        db.IdentidadesExternas.Add(new IdentidadExterna
        {
            IdCliente = idCliente, Proveedor = ReglasGoogle.Proveedor, Sub = identidad.Sub, Correo = identidad.Correo, FechaVinculacion = ahora
        });
        auditoria.Anotar("cuenta", idCliente.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.VincularGoogle, null, ActorDe(idCliente));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException conflicto) when (ViolacionUnica.Es(conflicto))
        {
            // Dos peticiones a la vez: lo decide la base (indices unicos). Cualquier otro fallo de la base no se disfraza de conflicto.
            return ViolacionUnica.Contiene(conflicto, "uq_identidades_externas_cliente")
                ? new ResultadoGoogle.Conflicto("google_ya_vinculada", "Tu cuenta ya tiene un Google vinculado. Desvincúlalo antes de vincular otro.")
                : new ResultadoGoogle.Conflicto("google_en_uso", "Esa cuenta de Google ya está vinculada a otra cuenta.");
        }
        return new ResultadoGoogle.Vinculada(identidad.Correo);
    }

    // ── DELETE /mi-cuenta/google ──

    public async Task<ResultadoGoogle> DesvincularAsync(int idCliente, string? passwordActual, CancellationToken ct)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == idCliente && c.Activo && c.InhabilitadoEn == null, ct);
        if (cliente is null)
            return new ResultadoGoogle.NoEncontrado();
        // Sin contraseña no hay otra forma de entrar: se le pide crear una antes.
        if (!ReglasGoogle.PuedeDesvincular(cliente.PasswordHash is not null))
            return new ResultadoGoogle.Conflicto("sin_contrasena", MensajeSinContrasena);
        if (string.IsNullOrEmpty(passwordActual) || !auth.VerifyPassword(passwordActual, cliente.PasswordHash!))
            return ErrorPassword();

        var identidades = await db.IdentidadesExternas.Where(i => i.IdCliente == idCliente && i.Proveedor == ReglasGoogle.Proveedor).ToListAsync(ct);
        if (identidades.Count == 0)
            return new ResultadoGoogle.NoEncontrado();
        db.IdentidadesExternas.RemoveRange(identidades);
        auditoria.Anotar("cuenta", idCliente.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.DesvincularGoogle, null, ActorDe(idCliente));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra peticion (un doble clic, o la baja de la cuenta) ya la quito.
            return new ResultadoGoogle.NoEncontrado();
        }
        return new ResultadoGoogle.Desvinculada();
    }

    // ── Fase 14C: Mi perfil del personal (/mi-perfil/google) ──

    public async Task<ResultadoGoogle> EstadoPersonalAsync(int idEmpleado, CancellationToken ct)
    {
        var correo = await db.IdentidadesExternas.AsNoTracking()
            .Where(i => i.IdEmpleado == idEmpleado && i.Proveedor == ReglasGoogle.Proveedor).Select(i => i.Correo).FirstOrDefaultAsync(ct);
        return new ResultadoGoogle.EstadoGoogle(correo is not null, correo);
    }

    // Igual que el cliente: pide la contraseña actual (antes de gastar el nonce) y exige que el correo verificado de Google sea el de su ficha.
    public async Task<ResultadoGoogle> VincularPersonalAsync(int idEmpleado, string? idToken, string? nonce, string? passwordActual, CancellationToken ct)
    {
        if (!Configurado)
            return new ResultadoGoogle.NoConfigurado();
        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.IdEmpleado == idEmpleado && e.Activo, ct);
        if (empleado is null)
            return new ResultadoGoogle.NoEncontrado();
        if (string.IsNullOrEmpty(passwordActual) || !auth.VerifyPassword(passwordActual, empleado.PasswordHash))
            return ErrorPassword();
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(nonce))
            return new ResultadoGoogle.Invalido(new Dictionary<string, string[]> { ["idToken"] = ["Falta el token de Google."] }, "Falta el token de Google.");
        if (await db.IdentidadesExternas.AnyAsync(i => i.IdEmpleado == idEmpleado && i.Proveedor == ReglasGoogle.Proveedor, ct))
            return new ResultadoGoogle.Conflicto("google_ya_vinculada", "Tu cuenta ya tiene un Google vinculado. Desvincúlalo antes de vincular otro.");

        var (identidad, error) = await VerificarAsync(idToken, nonce, ct);
        if (identidad is null)
            return error!;
        if (!ReglasGoogle.MismoCorreo(empleado.Email, identidad.Correo))
            return new ResultadoGoogle.Conflicto("google_correo_distinto", "El correo de esa cuenta de Google no es el de tu cuenta. Usa el Google con el correo de tu cuenta.");
        if (await db.IdentidadesExternas.AnyAsync(i => i.Proveedor == ReglasGoogle.Proveedor && i.Sub == identidad.Sub, ct))
            return new ResultadoGoogle.Conflicto("google_en_uso", "Esa cuenta de Google ya está vinculada a otra cuenta.");

        // Se vuelve a leer al empleado con un candado de actualizacion (ya no hay llamadas a Google): si gestion le cambia el correo o
        // lo desactiva justo ahora, esa edicion espera a este guardado y despues borra el vinculo recien creado. Sin esto podria
        // quedar un vinculo con un correo que ya no es el suyo.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var vigente = await db.Empleados.FromSqlInterpolated($"SELECT * FROM empleados WITH (UPDLOCK, ROWLOCK) WHERE id_empleado = {idEmpleado}")
            .AsNoTracking().FirstOrDefaultAsync(ct);
        if (vigente is not { Activo: true } || !ReglasGoogle.MismoCorreo(vigente.Email, identidad.Correo))
            return new ResultadoGoogle.Conflicto("google_correo_distinto", "El correo de esa cuenta de Google no es el de tu cuenta. Usa el Google con el correo de tu cuenta.");

        db.IdentidadesExternas.Add(new IdentidadExterna
        {
            IdEmpleado = idEmpleado, Proveedor = ReglasGoogle.Proveedor, Sub = identidad.Sub, Correo = identidad.Correo, FechaVinculacion = reloj.GetUtcNow()
        });
        auditoria.Anotar("empleado", idEmpleado.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.VincularGoogle, null, ActorDe(empleado));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException conflicto) when (ViolacionUnica.Es(conflicto))
        {
            return ViolacionUnica.Contiene(conflicto, "uq_identidades_externas_empleado")
                ? new ResultadoGoogle.Conflicto("google_ya_vinculada", "Tu cuenta ya tiene un Google vinculado. Desvincúlalo antes de vincular otro.")
                : new ResultadoGoogle.Conflicto("google_en_uso", "Esa cuenta de Google ya está vinculada a otra cuenta.");
        }
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoGoogle.Vinculada(identidad.Correo);
    }

    // Todo empleado tiene contraseña: siempre se puede desvincular (con ella); no existe el caso «sin contraseña» de los clientes.
    public async Task<ResultadoGoogle> DesvincularPersonalAsync(int idEmpleado, string? passwordActual, CancellationToken ct)
    {
        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.IdEmpleado == idEmpleado && e.Activo, ct);
        if (empleado is null)
            return new ResultadoGoogle.NoEncontrado();
        if (string.IsNullOrEmpty(passwordActual) || !auth.VerifyPassword(passwordActual, empleado.PasswordHash))
            return ErrorPassword();

        var identidades = await db.IdentidadesExternas.Where(i => i.IdEmpleado == idEmpleado && i.Proveedor == ReglasGoogle.Proveedor).ToListAsync(ct);
        if (identidades.Count == 0)
            return new ResultadoGoogle.NoEncontrado();
        db.IdentidadesExternas.RemoveRange(identidades);
        auditoria.Anotar("empleado", idEmpleado.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.DesvincularGoogle, null, ActorDe(empleado));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGoogle.NoEncontrado();
        }
        // Si su Google estaba comprometido, quien entro con el sigue con una sesion de hasta 12 horas: desvincular las cierra todas.
        await sesiones.RevocarTodasAsync(TitularSesion.DeEmpleado(idEmpleado), ct);
        return new ResultadoGoogle.DesvinculadaPersonal(empleado);
    }

    // ── Comunes ──

    // El ingreso actualiza «ultimo ingreso»: si la identidad se borro justo entonces (se cambio el correo del empleado, se dio de baja
    // la cuenta), no hay sesion y la respuesta es la unica de un token malo (antes era un 500).
    private async Task<ResultadoGoogle> GuardarIngresoAsync(ResultadoGoogle sesion, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return sesion;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Ingreso con Google rechazado: la identidad se quito mientras se iniciaba la sesion.");
            return new ResultadoGoogle.TokenInvalido();
        }
    }

    private static ResultadoGoogle ErrorPassword() => new ResultadoGoogle.Invalido(
        new Dictionary<string, string[]> { ["passwordActual"] = [ServicioMiCuenta.MensajePasswordActual] }, ServicioMiCuenta.MensajePasswordActual);

    private void AnotarIngreso(Cliente cliente, bool nueva) =>
        auditoria.Anotar("cuenta", cliente.IdCliente.ToString(CultureInfo.InvariantCulture), AccionesAuditoria.IngresoGoogle,
            DetalleAuditoria.Libre(new Dictionary<string, object?> { ["nueva"] = nueva }), ActorDe(cliente.IdCliente));

    // Las peticiones de ingreso son anonimas: quien hace la accion es el cliente, con la IP de la peticion.
    private ActorAuditoria ActorDe(int idCliente) => new(null, idCliente, Roles.Cliente, actores.Actual().Ip);

    // Fase 14C: lo mismo para un empleado (con su rol, que es el de su sesion).
    private ActorAuditoria ActorDe(Empleado empleado) => new(empleado.IdEmpleado, null, empleado.Rol, actores.Actual().Ip);

    // El nonce se gasta al presentarlo (salga bien o mal), Google se consulta con un limite de tiempo y las condiciones
    // del token las decide ReglasGoogle. Nada del token, del nonce ni del error de Google llega a los registros.
    private async Task<(IdentidadGoogle? Identidad, ResultadoGoogle? Error)> VerificarAsync(string idToken, string nonce, CancellationToken ct)
    {
        if (!nonces.Consumir(nonce))
        {
            logger.LogInformation("Ingreso con Google rechazado: el nonce no se emitio, ya se uso o vencio.");
            return (null, new ResultadoGoogle.TokenInvalido());
        }

        var clientId = opciones.Value.ClientId!.Trim();
        ResultadoVerificadorGoogle respuesta;
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(opciones.Value.Limite);
            // WaitAsync corta la espera aunque el verificador no atienda la cancelacion.
            respuesta = await verificador.VerificarAsync(idToken, clientId, limite.Token).WaitAsync(opciones.Value.Limite, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // Tiempo agotado o fallo de la libreria: solo el tipo; el mensaje puede traer parte del token.
            logger.LogWarning("Google no respondio a tiempo o fallo ({Tipo}).", error.GetType().Name);
            return (null, new ResultadoGoogle.NoDisponible());
        }

        switch (respuesta)
        {
            case ResultadoVerificadorGoogle.Verificado verificado:
                var validacion = ReglasGoogle.Validar(verificado.Claims, clientId, nonce, reloj.GetUtcNow());
                if (validacion.Identidad is { } identidad)
                    return (identidad, null);
                logger.LogInformation("Token de Google rechazado ({Motivo}).", validacion.Motivo);
                return (null, new ResultadoGoogle.TokenInvalido());
            case ResultadoVerificadorGoogle.NoDisponible:
                return (null, new ResultadoGoogle.NoDisponible());
            default:
                logger.LogInformation("Token de Google rechazado (firma o formato).");
                return (null, new ResultadoGoogle.TokenInvalido());
        }
    }
}
