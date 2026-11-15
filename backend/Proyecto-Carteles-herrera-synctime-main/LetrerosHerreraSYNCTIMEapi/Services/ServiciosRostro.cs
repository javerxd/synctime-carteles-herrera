using System.Text;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio.Rostro;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Fase 10: rostro segun requisitos, con prueba de vida (RFS03, RFU21, RFU22, RNFP12, RNFE06, E17, E18, S15, S16).
//  - Solo dueño y administrador usan el rostro, y siempre con un equipo autorizado.
//  - Cada ingreso pide un desafio: el servidor elige al azar el orden de los dos giros, de un solo uso y por
//    pocos segundos. Se guardan solo hashes del token y del email.
//  - El ingreso trae tres capturas (frente, primer giro y segundo giro) con su descriptor y sus 68 puntos. El
//    servidor mide el giro con los puntos (Negocio.Rostro.Giro), compara cada captura con la muestra de su
//    lado y las capturas entre si. Los puntos no se guardan; los descriptores del intento tampoco.
//  - El registro guarda tres muestras cifradas (frente, izquierda y derecha).
// Nada de esto va a los logs: ni descriptores, ni puntos, ni tokens, ni desafios.
public sealed class ServicioRostros(
    LetrerosHerreradbContext db,
    RostroFacialService rostros,
    ServicioReautenticacion reautenticacion,
    LimitadorPorCuenta limitePorCuenta,
    IConfiguration configuracion,
    TimeProvider reloj,
    ILogger<ServicioRostros> logger)
{
    public const string MensajeSoloGestion = "El acceso con rostro es solo para el dueño y la administración.";
    public const string MensajeTitularIncorrecta = "La contraseña del titular no es correcta.";
    private const int LargoMaximoCorreo = 254;
    private const int LargoMaximoToken = 100;

    // ── Desafio ──

    // Siempre se emite, exista o no el email: la respuesta no revela cuentas.
    public async Task<DesafioEmitido> EmitirDesafioAsync(string correo, CancellationToken ct)
    {
        var ahora = Ahora();
        var token = RostroFacialService.CrearToken();
        var orden = OrdenGiros.Validos[System.Security.Cryptography.RandomNumberGenerator.GetInt32(OrdenGiros.Validos.Length)];
        db.DesafiosRostro.Add(new DesafioRostro
        {
            TokenHash = RostroFacialService.HashHex(token),
            CorreoHash = RostroFacialService.HashHex(correo),
            Orden = orden,
            Emitido = ahora,
            Vence = ahora.AddSeconds(rostros.SegundosDesafio)
        });
        await db.SaveChangesAsync(ct);
        return new DesafioEmitido(token, orden, rostros.SegundosDesafio);
    }

    // Marca el desafio como usado en una sola sentencia, que solo afecta a uno vigente, sin usar y de ese
    // email: dos envios simultaneos del mismo desafio no pueden usarlo los dos. Devuelve su orden, o null.
    public async Task<string?> ConsumirDesafioAsync(string? correo, string? desafio, CancellationToken ct)
    {
        if (correo is null || string.IsNullOrEmpty(desafio) || desafio.Length > LargoMaximoToken)
            return null;

        var ahora = Ahora();
        var tokenHash = RostroFacialService.HashHex(desafio);
        var correoHash = RostroFacialService.HashHex(correo);
        var usados = await db.DesafiosRostro
            .Where(d => d.TokenHash == tokenHash && d.CorreoHash == correoHash && d.Usado == null && d.Vence > ahora)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Usado, ahora), ct);
        if (usados != 1)
            return null;
        return await db.DesafiosRostro.AsNoTracking().Where(d => d.TokenHash == tokenHash).Select(d => d.Orden).SingleAsync(ct);
    }

    // Las columnas guardan segundos (datetimeoffset(0)): se trunca aqui para que lo guardado sea lo calculado y
    // «usado» nunca quede antes de «emitido» por un redondeo.
    private DateTimeOffset Ahora()
    {
        var ahora = reloj.GetUtcNow();
        return ahora.AddTicks(-(ahora.UtcTicks % TimeSpan.TicksPerSecond));
    }

    // La misma cuenta escrita de formas equivalentes da el mismo hash (como en LimitadorPorCuenta). null si no sirve.
    public static string? NormalizarCorreo(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > LargoMaximoCorreo)
            return null;
        return correo.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
    }

    // ── Ingreso ──

    // El empleado si todo se cumple; null ante cualquier fallo, sin decir cual (el endpoint responde 401).
    public async Task<Empleado?> IngresarAsync(IngresoFacial ingreso, CancellationToken ct)
    {
        var correo = NormalizarCorreo(ingreso.Email);
        // El desafio se gasta primero, falle lo que falle despues.
        var orden = await ConsumirDesafioAsync(correo, ingreso.Desafio, ct);
        var ahora = reloj.GetUtcNow();
        await BorrarDatosDeInactivosAsync(ahora, ct);

        var capturas = LeerCapturas(ingreso.Capturas);
        var rostro = correo is null
            ? null
            : await db.RostrosEmpleado.AsNoTracking().Include(r => r.Empleado)
                .FirstOrDefaultAsync(r => r.FechaRevocacion == null && r.Empleado!.Activo && r.Empleado.Email == correo, ct);
        if (capturas is null)
            return null;

        // Solo gestion, y solo un rostro con las tres muestras. Si no, se compara con el señuelo.
        var muestras = rostro is not null && RostroFacialService.EsGestion(rostro.Empleado!.Rol) ? MuestrasCifradas.De(rostro) : null;
        var lados = OrdenGiros.Lados(orden);
        var primero = lados?.Primero ?? LadoGiro.Izquierda;
        // La comparacion se hace siempre, para que todo fallo cueste lo mismo.
        var medicion = rostros.Medir(capturas.Descriptores, primero, muestras, rostro?.IdEmpleado ?? 0);
        var coincide = rostros.Coincide(medicion);
        var girosCorrectos = lados is { } l && rostros.Rangos.Cumple(capturas.Giros, l.Primero, l.Segundo);

        // Sin un equipo valido no se cuenta el intento: asi un anonimo no puede bloquear el rostro del dueño.
        if (muestras is null || !rostros.EquipoAutorizado(rostro!, rostro!.Empleado!.Rol, ingreso.TokenEquipo, ahora))
            return null;

        // Solo con un equipo valido: un anonimo no puede llenar el log de errores.
        if (medicion is null)
            logger.LogError("No se pudo descifrar una muestra del rostro del empleado {IdEmpleado}: debe registrar su rostro de nuevo.", rostro.IdEmpleado);

        var permitido = await RegistrarIntentoAsync(rostro.IdRostro, ahora, ct) == 1;
        if (!permitido || !coincide || !girosCorrectos)
            return null;

        await LimpiarIntentosAsync(rostro.IdRostro, ct);
        return rostro.Empleado;
    }

    // Tres capturas con un descriptor valido y un giro medible cada una; null si alguna no sirve.
    public static CapturasLeidas? LeerCapturas(IReadOnlyList<CapturaRostro?>? capturas)
    {
        if (capturas is not { Count: RostroFacialService.CapturasPorIntento })
            return null;

        var descriptores = new float[RostroFacialService.CapturasPorIntento][];
        var giros = new double[RostroFacialService.CapturasPorIntento];
        for (var i = 0; i < capturas.Count; i++)
        {
            if (capturas[i] is not { } captura || !RostroFacialService.EsDescriptorValido(captura.Descriptor) || Medir(captura) is not { } g)
                return null;
            descriptores[i] = captura.Descriptor!;
            giros[i] = g;
        }
        return new CapturasLeidas(descriptores, giros);
    }

    private static double? Medir(CapturaRostro captura) =>
        captura.Ancho is { } ancho && captura.Alto is { } alto ? Giro.Indice(captura.Puntos, ancho, alto) : null;

    // El intento se cuenta antes de usar el resultado, en una sola sentencia que tambien revisa el bloqueo:
    // varias peticiones simultaneas no pueden probar mas veces que las permitidas. Devuelve 0 si esta bloqueado.
    private Task<int> RegistrarIntentoAsync(int idRostro, DateTimeOffset ahora, CancellationToken ct)
    {
        var hasta = ahora + RostroFacialService.DuracionBloqueo;
        return db.RostrosEmpleado
            .Where(r => r.IdRostro == idRostro && (r.BloqueadoHasta == null || r.BloqueadoHasta <= ahora))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.BloqueadoHasta, r => r.IntentosFallidos + 1 >= RostroFacialService.MaxFallos ? hasta : r.BloqueadoHasta)
                .SetProperty(r => r.IntentosFallidos, r => r.IntentosFallidos + 1 >= RostroFacialService.MaxFallos ? 0 : r.IntentosFallidos + 1), ct);
    }

    private Task<int> LimpiarIntentosAsync(int idRostro, CancellationToken ct) =>
        db.RostrosEmpleado
            .Where(r => r.IdRostro == idRostro)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.IntentosFallidos, 0)
                .SetProperty(r => r.BloqueadoHasta, (DateTimeOffset?)null), ct);

    // Desde la fase 9 desactivar a un empleado borra su rostro en el mismo paso. Esto cubre ademas al que se
    // desactive por SQL: sus datos se borran en el siguiente intento de cualquiera, y su rostro deja de
    // aceptarse antes, en la consulta del ingreso.
    private Task<int> BorrarDatosDeInactivosAsync(DateTimeOffset ahora, CancellationToken ct) =>
        db.RostrosEmpleado
            .Where(r => r.FechaRevocacion == null && !r.Empleado!.Activo)
            .ExecuteUpdateAsync(DatosRostro.Borrado(ahora), ct);

    // ── Estado y registro ──

    public async Task<EstadoRostro> EstadoAsync(int idEmpleado, CancellationToken ct)
    {
        // Un token vigente de alguien ya desactivado no debe dar 500: simplemente no puede usar el rostro.
        var rol = await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idEmpleado && e.Activo).Select(e => e.Rol).FirstOrDefaultAsync(ct) ?? "";
        var rostro = await db.RostrosEmpleado.AsNoTracking().FirstOrDefaultAsync(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null, ct);
        var completo = rostro?.TieneTresMuestras == true;
        return new EstadoRostro(
            Disponible: rostros.Configurado,
            Activo: completo,
            DebeRegistrarseDeNuevo: rostro is not null && !completo,
            PuedeUsarRostro: RostroFacialService.EsGestion(rol),
            FechaConsentimiento: rostro?.FechaConsentimiento,
            RequiereEquipo: RostroFacialService.RequiereEquipo(rol),
            EquipoVence: rostro is null ? null : rostros.VencimientoEquipo(rostro),
            EquipoVigente: rostro is not null && rostros.VencimientoEquipo(rostro) > reloj.GetUtcNow(),
            Consentimiento: new TextoConsentimiento(ConsentimientoFacial.Version, ConsentimientoFacial.Texto));
    }

    // El titular registra su propio rostro con su contraseña. Recibe un token de equipo nuevo.
    public async Task<ResultadoRostro<RostroRegistrado>> RegistrarPropioAsync(int idEmpleado, RegistroRostro registro, CancellationToken ct)
    {
        if (!rostros.Configurado)
            return new ResultadoRostro<RostroRegistrado>.NoDisponible();
        var rol = await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idEmpleado && e.Activo).Select(e => e.Rol).FirstOrDefaultAsync(ct);
        if (rol is null || !RostroFacialService.EsGestion(rol))
            return new ResultadoRostro<RostroRegistrado>.SoloGestion();
        if (Revisar(registro) is { } invalido)
            return invalido;
        if (await ConfirmarConLimiteAsync(idEmpleado, registro.Password, $"rostro-propio:{idEmpleado}", ct) is { } fallo)
            return fallo == FalloContraseña.Bloqueada
                ? new ResultadoRostro<RostroRegistrado>.DemasiadosIntentos()
                : new ResultadoRostro<RostroRegistrado>.ReautenticacionFallida("password", Endpoints.Reautenticacion.Incorrecta);

        return new ResultadoRostro<RostroRegistrado>.Correcto(await GuardarAsync(idEmpleado, idEmpleado, registro.Muestras!, conEquipo: true, ct));
    }

    // El dueño o el administrador registra el rostro de OTRO empleado de gestion, con el titular presente: el
    // titular escribe su propia contraseña y acepta el consentimiento. No entrega token de equipo: el titular
    // autoriza su equipo desde su perfil.
    public async Task<ResultadoRostro<RostroRegistrado>> RegistrarParaOtroAsync(int idRegistra, int idTitular, RegistroRostro registro, CancellationToken ct)
    {
        if (!rostros.Configurado)
            return new ResultadoRostro<RostroRegistrado>.NoDisponible();
        if (idRegistra == idTitular)
            return new ResultadoRostro<RostroRegistrado>.Invalido(new Dictionary<string, string[]>(),
                "Para registrar tu propio rostro usa la sección de rostro de tu perfil.");
        var titular = await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idTitular && e.Activo)
            .Select(e => new { e.Rol, e.Email }).FirstOrDefaultAsync(ct);
        if (titular is null)
            return new ResultadoRostro<RostroRegistrado>.NoEncontrado();
        if (!RostroFacialService.EsGestion(titular.Rol))
            return new ResultadoRostro<RostroRegistrado>.SoloGestion();
        if (Revisar(registro) is { } invalido)
            return invalido;

        // Es un intento de adivinar la contraseña del titular: tiene su propio limite por cuenta. No comparte el
        // contador del ingreso con contraseña, para que otra cuenta de gestion no pueda dejar al titular sin entrar.
        if (await ConfirmarConLimiteAsync(idTitular, registro.Password, $"rostro-titular:{idTitular}", ct) is { } fallo)
            return fallo == FalloContraseña.Bloqueada
                ? new ResultadoRostro<RostroRegistrado>.DemasiadosIntentos()
                : new ResultadoRostro<RostroRegistrado>.ReautenticacionFallida("passwordTitular", MensajeTitularIncorrecta);

        return new ResultadoRostro<RostroRegistrado>.Correcto(await GuardarAsync(idTitular, idRegistra, registro.Muestras!, conEquipo: false, ct));
    }

    private enum FalloContraseña { Incorrecta, Bloqueada }

    // Confirma la contraseña de un empleado contando los fallos en una clave propia de esta accion.
    private async Task<FalloContraseña?> ConfirmarConLimiteAsync(int idEmpleado, string? password, string cuenta, CancellationToken ct)
    {
        if (limitePorCuenta.EstaBloqueada(cuenta))
            return FalloContraseña.Bloqueada;
        if (!await reautenticacion.ConfirmarAsync(idEmpleado, password, ct))
        {
            limitePorCuenta.RegistrarFallo(cuenta);
            return FalloContraseña.Incorrecta;
        }
        limitePorCuenta.Olvidar(cuenta);
        return null;
    }

    private ResultadoRostro<RostroRegistrado>? Revisar(RegistroRostro registro)
    {
        if (registro.Consentimiento != true)
            return new ResultadoRostro<RostroRegistrado>.Invalido(
                new Dictionary<string, string[]> { ["consentimiento"] = ["Debe aceptar el consentimiento para registrar el rostro."] },
                "Debe aceptar el consentimiento para registrar el rostro.");
        if (registro.VersionConsentimiento != ConsentimientoFacial.Version)
            return new ResultadoRostro<RostroRegistrado>.Invalido(
                new Dictionary<string, string[]> { ["versionConsentimiento"] = ["El texto del consentimiento cambió. Vuelva a leerlo."] },
                "El texto del consentimiento cambió. Vuelva a leerlo.");

        var errores = RevisionMuestras.Revisar(registro.Muestras, rostros.Rangos, rostros.UmbralEntreCapturas);
        return errores.Count == 0
            ? null
            : new ResultadoRostro<RostroRegistrado>.Invalido(errores, "Una o más muestras no sirven. Repítelas como se indica.");
    }

    private async Task<RostroRegistrado> GuardarAsync(int idTitular, int idRegistra, MuestrasRostro muestras, bool conEquipo, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var token = conEquipo ? RostroFacialService.CrearTokenEquipo() : null;

        // Volver a registrar cierra el registro anterior, que queda como constancia. Se guarda en dos pasos
        // porque el indice unico exige revocar la fila vigente antes de insertar la nueva; el bloqueo por
        // empleado evita que dos registros simultaneos choquen en ese indice.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, $"rostro:{idTitular}", BloqueoDeAplicacion.Espera(configuracion), ct);
        var vigente = await db.RostrosEmpleado.FirstOrDefaultAsync(r => r.IdEmpleado == idTitular && r.FechaRevocacion == null, ct);
        if (vigente is not null)
        {
            DatosRostro.Revocar(vigente, ahora);
            await db.SaveChangesAsync(ct);
        }

        var rostro = new RostroEmpleado
        {
            IdEmpleado = idTitular,
            DescriptorFrente = rostros.Cifrar(muestras.Frente!.Descriptor!, idTitular, LadoMuestra.Frente),
            DescriptorIzquierda = rostros.Cifrar(muestras.Izquierda!.Descriptor!, idTitular, LadoMuestra.Izquierda),
            DescriptorDerecha = rostros.Cifrar(muestras.Derecha!.Descriptor!, idTitular, LadoMuestra.Derecha),
            FormatoMuestras = RostroEmpleado.FormatoMuestrasPorLado,
            IdEmpleadoRegistra = idRegistra,
            FechaConsentimiento = ahora,
            VersionConsentimiento = ConsentimientoFacial.Version,
            TokenEquipoHash = token is null ? null : RostroFacialService.HashToken(token),
            FechaAutorizacionEquipo = token is null ? null : ahora
        };
        db.RostrosEmpleado.Add(rostro);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);

        if (idRegistra == idTitular)
            logger.LogInformation("Empleado {IdEmpleado} registro su acceso facial", idTitular);
        else
            logger.LogInformation("Empleado {IdRegistra} registro el acceso facial del empleado {IdEmpleado}, con su contraseña", idRegistra, idTitular);
        return new RostroRegistrado(idTitular, true, rostro.FechaConsentimiento, token, token is null ? null : rostros.VencimientoEquipo(rostro));
    }

    public async Task<ResultadoRostro<EquipoAutorizado>> AutorizarEquipoAsync(int idEmpleado, string? password, CancellationToken ct)
    {
        var rol = await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idEmpleado && e.Activo).Select(e => e.Rol).FirstOrDefaultAsync(ct);
        if (await ConfirmarConLimiteAsync(idEmpleado, password, $"rostro-propio:{idEmpleado}", ct) is { } fallo)
            return fallo == FalloContraseña.Bloqueada
                ? new ResultadoRostro<EquipoAutorizado>.DemasiadosIntentos()
                : new ResultadoRostro<EquipoAutorizado>.ReautenticacionFallida("password", Endpoints.Reautenticacion.Incorrecta);
        if (rol is null || !RostroFacialService.RequiereEquipo(rol))
            return new ResultadoRostro<EquipoAutorizado>.SoloGestion();

        var rostro = await db.RostrosEmpleado.FirstOrDefaultAsync(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null, ct);
        if (rostro is null || !rostro.TieneTresMuestras)
            return new ResultadoRostro<EquipoAutorizado>.Invalido(new Dictionary<string, string[]>(), "Primero registre su rostro con las tres muestras.");

        var token = RostroFacialService.CrearTokenEquipo();
        rostro.TokenEquipoHash = RostroFacialService.HashToken(token);
        rostro.FechaAutorizacionEquipo = reloj.GetUtcNow();
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Empleado {IdEmpleado} autorizo un equipo para su acceso facial", idEmpleado);
        return new ResultadoRostro<EquipoAutorizado>.Correcto(new EquipoAutorizado(token, rostros.VencimientoEquipo(rostro)));
    }

    // Desactivar borra las cuatro columnas cifradas y el token de equipo; queda la constancia del consentimiento.
    // Retirar el consentimiento no pide contraseña: debe ser tan expedito como sea posible.
    public async Task<bool> DesactivarAsync(int idEmpleado, int idQuienDesactiva, CancellationToken ct)
    {
        var afectadas = await db.RostrosEmpleado
            .Where(r => r.IdEmpleado == idEmpleado && r.FechaRevocacion == null)
            .ExecuteUpdateAsync(DatosRostro.Borrado(reloj.GetUtcNow()), ct);
        if (afectadas > 0)
        {
            if (idEmpleado == idQuienDesactiva)
                logger.LogInformation("Empleado {IdEmpleado} retiro su acceso facial", idEmpleado);
            else
                logger.LogInformation("Empleado {IdQuien} desactivo el acceso facial del empleado {IdEmpleado}", idQuienDesactiva, idEmpleado);
        }
        return afectadas > 0;
    }

    // ── Rostros registrados (S16) ──

    public async Task<IReadOnlyList<FilaRostroRegistrado>> ListarAsync(CancellationToken ct)
    {
        var empleados = await db.Empleados.AsNoTracking()
            .Where(e => e.Activo && (e.Rol == Roles.Dueno || e.Rol == Roles.Administrador))
            .OrderBy(e => e.Nombre).ThenBy(e => e.Apellido).ThenBy(e => e.IdEmpleado)
            .Select(e => new { e.IdEmpleado, e.Nombre, e.Apellido, e.Rol })
            .ToListAsync(ct);
        var ids = empleados.Select(e => e.IdEmpleado).ToList();
        var filas = await db.RostrosEmpleado.AsNoTracking()
            .Where(r => ids.Contains(r.IdEmpleado))
            .Select(r => new
            {
                r.IdRostro,
                r.IdEmpleado,
                r.FechaConsentimiento,
                r.VersionConsentimiento,
                r.FechaRevocacion,
                Completo = r.FormatoMuestras == RostroEmpleado.FormatoMuestrasPorLado
                    && r.DescriptorFrente != null && r.DescriptorIzquierda != null && r.DescriptorDerecha != null,
                RegistradoPor = r.EmpleadoRegistra == null ? null : r.EmpleadoRegistra.Nombre + " " + r.EmpleadoRegistra.Apellido
            })
            .ToListAsync(ct);

        return empleados.Select(e =>
        {
            var propias = filas.Where(f => f.IdEmpleado == e.IdEmpleado).ToList();
            var vigente = propias.FirstOrDefault(f => f.FechaRevocacion is null);
            var historial = propias.Where(f => f.FechaRevocacion is not null)
                .OrderByDescending(f => f.FechaRevocacion).ThenByDescending(f => f.IdRostro)
                .Select(f => new ConstanciaRostro(f.FechaConsentimiento, f.FechaConsentimiento, f.VersionConsentimiento, f.RegistradoPor, f.FechaRevocacion))
                .ToList();
            var ultima = historial.FirstOrDefault();
            var estado = vigente is not null
                ? (vigente.Completo ? EstadosRostro.Activo : EstadosRostro.DebeRegistrarse)
                : (ultima is not null ? EstadosRostro.Desactivado : EstadosRostro.SinRegistro);
            return new FilaRostroRegistrado(
                e.IdEmpleado,
                $"{e.Nombre} {e.Apellido}",
                e.Rol,
                estado,
                vigente?.FechaConsentimiento ?? ultima?.FechaRegistro,
                vigente?.FechaConsentimiento ?? ultima?.FechaConsentimiento,
                vigente is not null ? vigente.RegistradoPor : ultima?.RegistradoPor,
                vigente is null ? ultima?.FechaDesactivacion : null,
                historial);
        }).ToList();
    }

    public Task<bool> EsGestionActivoAsync(int idEmpleado, CancellationToken ct) =>
        db.Empleados.AsNoTracking().AnyAsync(e => e.IdEmpleado == idEmpleado && e.Activo && (e.Rol == Roles.Dueno || e.Rol == Roles.Administrador), ct);
}

// Revision de las tres muestras de un registro, con un texto por muestra que dice como repetirla.
public static class RevisionMuestras
{
    public const string Frente = "muestras.frente";
    public const string Izquierda = "muestras.izquierda";
    public const string Derecha = "muestras.derecha";

    public static Dictionary<string, string[]> Revisar(MuestrasRostro? muestras, RangosGiro rangos, double umbralEntreMuestras)
    {
        var errores = new Dictionary<string, string[]>();
        var leidas = new (string Clave, LadoGiro? Lado, CapturaRostro? Captura)[]
        {
            (Frente, null, muestras?.Frente),
            (Izquierda, LadoGiro.Izquierda, muestras?.Izquierda),
            (Derecha, LadoGiro.Derecha, muestras?.Derecha)
        };

        foreach (var (clave, lado, captura) in leidas)
        {
            var g = captura is { Ancho: { } ancho, Alto: { } alto } ? Giro.Indice(captura.Puntos, ancho, alto) : null;
            if (captura is null || !RostroFacialService.EsDescriptorValido(captura.Descriptor) || g is null)
            {
                errores[clave] = ["No se pudo leer esta muestra. Repítela con el rostro completo dentro del óvalo y buena luz."];
                continue;
            }
            if (Mensaje(rangos.Revisar(lado, g.Value), lado) is { } mensaje)
                errores[clave] = [mensaje];
        }
        if (errores.Count > 0)
            return errores;

        // Las tres deben ser de la misma persona. Se culpa a la muestra que queda lejos de las demas.
        var descriptores = leidas.Select(l => l.Captura!.Descriptor!).ToArray();
        var lejanas = new int[descriptores.Length];
        for (var i = 0; i < descriptores.Length; i++)
            for (var j = i + 1; j < descriptores.Length; j++)
                if (RostroFacialService.Distancia(descriptores[i], descriptores[j]) > umbralEntreMuestras)
                {
                    lejanas[i]++;
                    lejanas[j]++;
                }
        var peor = lejanas.Max();
        for (var i = 0; i < descriptores.Length && peor > 0; i++)
            if (lejanas[i] == peor)
                errores[leidas[i].Clave] = ["Esta muestra no parece de la misma persona que las otras. Repítela; debe verse un solo rostro."];
        return errores;
    }

    private static string? Mensaje(ProblemaGiro problema, LadoGiro? lado)
    {
        var hacia = lado == LadoGiro.Izquierda ? "tu izquierda" : "tu derecha";
        return problema switch
        {
            ProblemaGiro.NoEstaDeFrente => "La muestra de frente salió girada. Repítela mirando directo a la cámara.",
            ProblemaGiro.LadoContrario => $"Giraste hacia el otro lado. Repite la muestra girando la cabeza hacia {hacia}.",
            ProblemaGiro.GiroInsuficiente => $"Falta giro. Repite la muestra girando la cabeza un poco más hacia {hacia}, unos 30 a 45 grados.",
            ProblemaGiro.GiroExcesivo => $"Giraste demasiado. Repite la muestra girando la cabeza un poco menos hacia {hacia}, unos 30 a 45 grados.",
            _ => null
        };
    }
}

// Lo que se borra al revocar un rostro: las cuatro columnas cifradas y el token de equipo. Lo usan tambien
// la desactivacion de un empleado (fase 9) y la limpieza de los inactivos.
public static class DatosRostro
{
    public static Action<UpdateSettersBuilder<RostroEmpleado>> Borrado(DateTimeOffset ahora) => setters => setters
        .SetProperty(r => r.DescriptorCifrado, (byte[]?)null)
        .SetProperty(r => r.DescriptorFrente, (byte[]?)null)
        .SetProperty(r => r.DescriptorIzquierda, (byte[]?)null)
        .SetProperty(r => r.DescriptorDerecha, (byte[]?)null)
        .SetProperty(r => r.TokenEquipoHash, (byte[]?)null)
        .SetProperty(r => r.FechaAutorizacionEquipo, (DateTimeOffset?)null)
        .SetProperty(r => r.FechaRevocacion, ahora);

    public static void Revocar(RostroEmpleado rostro, DateTimeOffset ahora)
    {
        rostro.DescriptorCifrado = null;
        rostro.DescriptorFrente = null;
        rostro.DescriptorIzquierda = null;
        rostro.DescriptorDerecha = null;
        rostro.TokenEquipoHash = null;
        rostro.FechaAutorizacionEquipo = null;
        rostro.FechaRevocacion = ahora;
    }
}

public static class EstadosRostro
{
    public const string Activo = "activo";
    public const string DebeRegistrarse = "debe_registrarse";
    public const string SinRegistro = "sin_registro";
    public const string Desactivado = "desactivado";
}

// Una captura del navegador: descriptor (128 numeros) y los 68 puntos en el cuadro sin espejo, con sus medidas.
public sealed record CapturaRostro(float[]? Descriptor, double[][]? Puntos, double? Ancho, double? Alto);

public sealed record MuestrasRostro(CapturaRostro? Frente, CapturaRostro? Izquierda, CapturaRostro? Derecha);

public sealed record IngresoFacial(string? Email, string? Desafio, string? TokenEquipo, IReadOnlyList<CapturaRostro?>? Capturas);

// Password es la del titular (la suya propia en /auth/rostro; passwordTitular en /rostros/{id}).
public sealed record RegistroRostro(string? Password, bool? Consentimiento, string? VersionConsentimiento, MuestrasRostro? Muestras);

public sealed record CapturasLeidas(float[][] Descriptores, double[] Giros);

public sealed record DesafioEmitido(string Token, string Orden, int Segundos);

public sealed record TextoConsentimiento(string Version, string[] Texto);

public sealed record EstadoRostro(bool Disponible, bool Activo, bool DebeRegistrarseDeNuevo, bool PuedeUsarRostro, DateTimeOffset? FechaConsentimiento,
    bool RequiereEquipo, DateTimeOffset? EquipoVence, bool EquipoVigente, TextoConsentimiento Consentimiento);

public sealed record RostroRegistrado(int IdEmpleado, bool Activo, DateTimeOffset FechaConsentimiento, string? TokenEquipo, DateTimeOffset? EquipoVence);

public sealed record EquipoAutorizado(string TokenEquipo, DateTimeOffset? EquipoVence);

public sealed record ConstanciaRostro(DateTimeOffset FechaRegistro, DateTimeOffset FechaConsentimiento, string VersionConsentimiento, string? RegistradoPor,
    DateTimeOffset? FechaDesactivacion);

public sealed record FilaRostroRegistrado(int IdEmpleado, string Nombre, string Rol, string Estado, DateTimeOffset? FechaRegistro,
    DateTimeOffset? FechaConsentimiento, string? RegistradoPor, DateTimeOffset? FechaDesactivacion, IReadOnlyList<ConstanciaRostro> Historial);

// Resultado de las operaciones del rostro; el endpoint lo traduce a la respuesta HTTP.
public abstract record ResultadoRostro<T>
{
    public sealed record Correcto(T Valor) : ResultadoRostro<T>;
    public sealed record Invalido(IReadOnlyDictionary<string, string[]> Errores, string Mensaje) : ResultadoRostro<T>;
    public sealed record NoEncontrado : ResultadoRostro<T>;
    public sealed record NoDisponible : ResultadoRostro<T>;
    // 403 solo_gestion: el rostro es solo para el dueño y la administración.
    public sealed record SoloGestion : ResultadoRostro<T>;
    public sealed record DemasiadosIntentos : ResultadoRostro<T>;
    // Campo: password o passwordTitular.
    public sealed record ReautenticacionFallida(string Campo, string Mensaje) : ResultadoRostro<T>;
}
