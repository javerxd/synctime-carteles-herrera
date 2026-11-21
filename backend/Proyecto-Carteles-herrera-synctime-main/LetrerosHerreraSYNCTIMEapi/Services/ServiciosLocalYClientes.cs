using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Negocio.Local;
using LetrerosHerreraSYNCTIMEapi.Negocio.Mantenedores;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// ── Fase 9: estado del local, horario y disponibilidad (RFS02, RNFO03, RFU16, RFU20, E10, E11) ──

// Fase 11: con su version (ETag) para guardar con If-Match.
public sealed record VistaCartel(bool AbiertoEnLocal, string MensajeEstado, string? TiempoEstimadoRegreso, DateTime FechaUltimaModificacion,
    int? IdEmpleadoModifica, string? ModificadoPor, byte[] RowVersion) : IConVersion;

// Lo que ve la portada del cartel: sin el empleado que lo cambio.
public sealed record CartelPublico(bool AbiertoEnLocal, string MensajeEstado, string? TiempoEstimadoRegreso, DateTime FechaUltimaModificacion, byte[] RowVersion)
    : IConVersion;

// Las horas en «HH:mm»; un dia cerrado, sin horas. Fase 11: rowVersion es la version del horario completo (la misma en
// los siete dias y en el ETag): se envia en If-Match al guardar.
public sealed record VistaDiaHorario(int DiaSemana, string NombreDia, bool Abierto, string? HoraInicio, string? HoraFin, byte[] RowVersion);

public sealed record HorarioConVersion(IReadOnlyList<VistaDiaHorario> Dias, byte[] RowVersion);

public sealed record VistaFranja(int DiaSemana, string HoraInicio, string HoraFin);
public sealed record MiDisponibilidad(IReadOnlyList<VistaFranja> Franjas);
public sealed record DisponibilidadDePersona(int IdEmpleado, string Nombre, string Rol, IReadOnlyList<VistaFranja> Franjas);

public sealed class ServicioLocal(LetrerosHerreradbContext db, IConfiguration configuracion, TimeProvider reloj)
{
    // RFU16, RNFO03 (decision 1 del 14-11-2026): todo el personal da vuelta el cartel. Queda quien y cuando;
    // la portada lo muestra sin el empleado. Fase 11: con la version vista (If-Match); si otra persona lo dio vuelta
    // entretanto, 409 version_obsoleta.
    public async Task<ResultadoGestion<VistaCartel>> ActualizarCartelAsync(int idEmpleado, EntradaCartel entrada, byte[]? version, CancellationToken ct)
    {
        var errores = ValidacionMantenedores.Cartel(entrada);
        if (errores.Count > 0)
            return new ResultadoGestion<VistaCartel>.Invalido(errores);
        if (version is not { Length: > 0 })
            return new ResultadoGestion<VistaCartel>.SinVersion();
        var cartel = await db.CartelDigital.FirstOrDefaultAsync(ct);
        if (cartel is null)
            return new ResultadoGestion<VistaCartel>.NoEncontrado();
        if (!Versiones.Iguales(version, cartel.RowVersion))
            return new ResultadoGestion<VistaCartel>.VersionObsoleta();
        cartel.AbiertoEnLocal = entrada.AbiertoEnLocal;
        cartel.MensajeEstado = entrada.MensajeEstado!.Trim();
        cartel.TiempoEstimadoRegreso = ValidacionMantenedores.Limpiar(entrada.TiempoEstimadoRegreso);
        cartel.IdEmpleadoModifica = idEmpleado;
        // La portada la muestra tal cual: en hora de Chile, no en la del servidor (un contenedor usa UTC).
        cartel.FechaUltimaModificacion = HoraChile.Convertir(reloj.GetUtcNow());
        // La base compara con la version vista: dos cambios a la vez no se pisan.
        db.Entry(cartel).Property(c => c.RowVersion).OriginalValue = version;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResultadoGestion<VistaCartel>.VersionObsoleta();
        }
        var nombre = await db.Empleados.AsNoTracking().Where(e => e.IdEmpleado == idEmpleado).Select(e => e.Nombre + " " + e.Apellido).FirstOrDefaultAsync(ct);
        return new ResultadoGestion<VistaCartel>.Correcto(new VistaCartel(cartel.AbiertoEnLocal, cartel.MensajeEstado, cartel.TiempoEstimadoRegreso,
            cartel.FechaUltimaModificacion, idEmpleado, nombre, cartel.RowVersion));
    }

    // GET /cartel-digital (publico): sin quien lo cambio, con su version.
    public Task<CartelPublico?> CartelAsync(CancellationToken ct) =>
        db.CartelDigital.AsNoTracking().OrderBy(c => c.IdCartel)
            .Select(c => new CartelPublico(c.AbiertoEnLocal, c.MensajeEstado, c.TiempoEstimadoRegreso, c.FechaUltimaModificacion, c.RowVersion))
            .FirstOrDefaultAsync(ct);

    public async Task<HorarioConVersion> HorarioAsync(CancellationToken ct)
    {
        var filas = await db.HorariosAtencion.AsNoTracking().OrderBy(h => h.DiaSemana).ToListAsync(ct);
        var version = VersionDelHorario(filas);
        return new HorarioConVersion(filas.Select(h => Vista(new DiaHorario(h.DiaSemana, h.NombreDia, h.Abierto, h.HoraInicio, h.HoraFin), version)).ToList(),
            version);
    }

    // La version del horario completo: la mayor de las siete filas (rowversion crece con cada cambio en la base).
    private static byte[] VersionDelHorario(IEnumerable<HorarioAtencion> filas) =>
        filas.Select(f => f.RowVersion).Where(v => v is { Length: 8 })
            .OrderByDescending(v => System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(v)).FirstOrDefault() ?? new byte[8];

    // E10: solo gestion. Los 7 dias en una transaccion, con quien y cuando. Fase 11: con la version vista del horario
    // completo (If-Match); los guardados van en fila, asi el segundo con la misma version recibe 409.
    public async Task<ResultadoGestion<HorarioConVersion>> GuardarHorarioAsync(int idEmpleado, IReadOnlyList<EntradaDiaHorario?>? dias, byte[]? version,
        CancellationToken ct)
    {
        var (validados, errores) = ReglasHorario.Validar(dias);
        if (errores.Count > 0)
            return new ResultadoGestion<HorarioConVersion>.Invalido(errores, "Revisa el horario.");
        if (version is not { Length: > 0 })
            return new ResultadoGestion<HorarioConVersion>.SinVersion();

        var ahora = reloj.GetUtcNow();
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, "horario_atencion", BloqueoDeAplicacion.Espera(configuracion), ct);
        var filas = await db.HorariosAtencion.ToListAsync(ct);
        if (!Versiones.Iguales(version, VersionDelHorario(filas)))
            return new ResultadoGestion<HorarioConVersion>.VersionObsoleta();
        foreach (var dia in validados)
        {
            var fila = filas.FirstOrDefault(f => f.DiaSemana == dia.DiaSemana);
            if (fila is null)
            {
                fila = new HorarioAtencion { DiaSemana = dia.DiaSemana, NombreDia = dia.NombreDia };
                db.HorariosAtencion.Add(fila);
            }
            fila.Abierto = dia.Abierto;
            fila.HoraInicio = dia.HoraInicio;
            fila.HoraFin = dia.HoraFin;
            fila.IdEmpleadoModifica = idEmpleado;
            fila.FechaModificacion = ahora;
        }
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoGestion<HorarioConVersion>.Correcto(await HorarioAsync(CancellationToken.None));
    }

    public async Task<MiDisponibilidad> MiDisponibilidadAsync(int idEmpleado, CancellationToken ct) =>
        new((await db.DisponibilidadEmpleados.AsNoTracking().Where(d => d.IdEmpleado == idEmpleado)
                .OrderBy(d => d.DiaSemana).ThenBy(d => d.HoraInicio).ToListAsync(ct))
            .Select(d => new VistaFranja(d.DiaSemana, ReglasHorario.Texto(d.HoraInicio), ReglasHorario.Texto(d.HoraFin))).ToList());

    // Reemplaza todas las franjas propias, en una transaccion.
    // Dos guardados a la vez del mismo empleado quedan en fila (bloqueo por empleado), y el horario se lee
    // dentro de la transaccion.
    public async Task<ResultadoGestion<MiDisponibilidad>> GuardarMiDisponibilidadAsync(int idEmpleado, IReadOnlyList<EntradaFranja?>? franjas,
        CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await BloqueoDeAplicacion.TomarAsync(db, $"disponibilidad:{idEmpleado}", BloqueoDeAplicacion.Espera(configuracion), ct);
        var horario = await LeerHorarioAsync(ct);
        var (validas, errores) = ReglasHorario.ValidarFranjas(franjas, horario);
        if (errores.Count > 0)
            return new ResultadoGestion<MiDisponibilidad>.Invalido(errores, ValidacionMantenedores.Resumen(errores, "Revisa tus franjas."));

        await db.DisponibilidadEmpleados.Where(d => d.IdEmpleado == idEmpleado).ExecuteDeleteAsync(ct);
        db.DisponibilidadEmpleados.AddRange(validas.Select(f => new DisponibilidadEmpleado
        {
            IdEmpleado = idEmpleado, DiaSemana = f.DiaSemana, HoraInicio = f.HoraInicio, HoraFin = f.HoraFin
        }));
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(CancellationToken.None);
        return new ResultadoGestion<MiDisponibilidad>.Correcto(await MiDisponibilidadAsync(idEmpleado, CancellationToken.None));
    }

    // Gestion: las franjas de todo el personal activo, con su nombre (quien no registro ninguna no aparece).
    public async Task<IReadOnlyList<DisponibilidadDePersona>> DisponibilidadEquipoAsync(CancellationToken ct)
    {
        var filas = await db.DisponibilidadEmpleados.AsNoTracking()
            .Where(d => d.Empleado!.Activo)
            .Select(d => new { d.IdEmpleado, Nombre = d.Empleado!.Nombre + " " + d.Empleado.Apellido, d.Empleado.Rol, d.DiaSemana, d.HoraInicio, d.HoraFin })
            .ToListAsync(ct);
        return filas.GroupBy(f => new { f.IdEmpleado, f.Nombre, f.Rol })
            .OrderBy(g => g.Key.Nombre, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("es-CL"), ignoreCase: true))
            .Select(g => new DisponibilidadDePersona(g.Key.IdEmpleado, g.Key.Nombre, g.Key.Rol,
                g.OrderBy(f => f.DiaSemana).ThenBy(f => f.HoraInicio)
                    .Select(f => new VistaFranja(f.DiaSemana, ReglasHorario.Texto(f.HoraInicio), ReglasHorario.Texto(f.HoraFin))).ToList()))
            .ToList();
    }

    // Portada: la union de las franjas del personal activo, recortada al horario de cada dia (si el horario
    // cambio despues, una franja vieja no muestra horas en que el taller esta cerrado). Sin nombres ni ids.
    public async Task<IReadOnlyList<AtencionDelDia>> AtencionAsync(CancellationToken ct)
    {
        var horario = await LeerHorarioAsync(ct);
        var franjas = await db.DisponibilidadEmpleados.AsNoTracking()
            .Where(d => d.Empleado!.Activo)
            .Select(d => new { d.DiaSemana, d.HoraInicio, d.HoraFin })
            .ToListAsync(ct);
        var recortadas = new List<Franja>();
        foreach (var f in franjas)
        {
            var dia = horario.FirstOrDefault(h => h.DiaSemana == f.DiaSemana);
            if (dia is not { Abierto: true, HoraInicio: { } apertura, HoraFin: { } cierre })
                continue;
            var desde = f.HoraInicio < apertura ? apertura : f.HoraInicio;
            var hasta = f.HoraFin > cierre ? cierre : f.HoraFin;
            if (desde < hasta)
                recortadas.Add(new Franja(f.DiaSemana, desde, hasta));
        }
        return ReglasHorario.Unir(recortadas);
    }

    private async Task<IReadOnlyList<DiaHorario>> LeerHorarioAsync(CancellationToken ct) =>
        (await db.HorariosAtencion.AsNoTracking().OrderBy(h => h.DiaSemana).ToListAsync(ct))
            .Select(h => new DiaHorario(h.DiaSemana, h.NombreDia, h.Abierto, h.HoraInicio, h.HoraFin)).ToList();

    private static VistaDiaHorario Vista(DiaHorario d, byte[] version) => new(d.DiaSemana, d.NombreDia, d.Abierto,
        d.Abierto && d.HoraInicio is { } inicio ? ReglasHorario.Texto(inicio) : null,
        d.Abierto && d.HoraFin is { } fin ? ReglasHorario.Texto(fin) : null, version);
}

// ── Fase 9: la ficha de un cliente (RNFP10: editar exige la version vista) ──

// Fase 11: CuentaDadaDeBaja, si el cliente dio de baja su cuenta web (se conservan sus pedidos).
public sealed record FichaCliente(int IdCliente, string? Rut, string Nombre, string Apellido, string Telefono, string? Email,
    string? Direccion, bool Registrado, bool CorreoConfirmado, bool Activo, DateTime FechaRegistro, byte[] RowVersion, bool CuentaDadaDeBaja = false) : IConVersion;

// Activo null: al crear, true; al editar, se conserva (no se reactiva sin pedirlo).
public sealed record EntradaCliente(string? Rut, string? Nombre, string? Apellido, string? Telefono, string? Email, string? Direccion, bool? Activo);

public sealed class ServicioFichaClientes(LetrerosHerreradbContext db, ServicioSesiones sesiones, ServicioCodigosCuenta codigos,
    Datos.Correo.ColaEnTransaccion cola, Microsoft.Extensions.Options.IOptions<Infraestructura.Correo.OpcionesCorreo> opcionesCorreo)
{
    public const string MensajeVersionObsoleta = "Otra persona modificó los datos de este cliente mientras los veías. Vuelve a cargarlos para ver los cambios.";

    public Task<FichaCliente?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Clientes.AsNoTracking().Where(c => c.IdCliente == id)
            .Select(c => new FichaCliente(c.IdCliente, c.Rut, c.Nombre, c.Apellido, c.Telefono, c.Email, c.Direccion,
                // Fase 14A: quien nacio con Google tampoco tiene contraseña, pero si tiene cuenta web.
                c.Registrado || c.IdentidadesExternas.Any(),
                c.FechaVerificacionCorreo != null, c.Activo, c.FechaRegistro, c.RowVersion, c.InhabilitadoEn != null))
            .FirstOrDefaultAsync(ct);

    // Cliente presencial (sin cuenta web). Las reglas de formato las hace cumplir la base (fase 7).
    public async Task<ResultadoGestion<FichaCliente>> CrearAsync(EntradaCliente entrada, CancellationToken ct)
    {
        // RNFO06: el RUT, si viene, se valida y se guarda normalizado, igual que en el registro del cliente.
        var faltan = ValidacionCuenta.Validar(entrada.Rut, entrada.Nombre, entrada.Apellido, entrada.Telefono, entrada.Direccion);
        if (faltan.Count > 0)
            return new ResultadoGestion<FichaCliente>.Invalido(faltan, "Revisa los datos del cliente.");
        var telefono = ValidacionCuenta.NormalizarTelefono(entrada.Telefono)!;
        if (await db.Clientes.AnyAsync(c => c.Telefono == telefono, ct))
            return new ResultadoGestion<FichaCliente>.Conflicto("conflicto", "Ya existe un cliente con ese teléfono.");
        var cliente = new Cliente
        {
            Rut = ValidacionCuenta.NormalizarRut(entrada.Rut), Nombre = entrada.Nombre!, Apellido = entrada.Apellido!, Telefono = telefono,
            Email = entrada.Email, Direccion = entrada.Direccion, Activo = entrada.Activo ?? true
        };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);
        return new ResultadoGestion<FichaCliente>.Correcto((await ObtenerAsync(cliente.IdCliente, CancellationToken.None))!);
    }

    // Fase 6: el correo de una cuenta se normaliza y, si cambia, queda sin verificar hasta confirmarlo.
    // Desactivar la cuenta, o cambiarle el correo, cierra sus sesiones (RFS03).
    public async Task<ResultadoGestion<FichaCliente>> EditarAsync(int id, EntradaCliente entrada, byte[]? version, CancellationToken ct)
    {
        if (version is not { Length: > 0 })
            return new ResultadoGestion<FichaCliente>.SinVersion();
        var existente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (existente is null)
            return new ResultadoGestion<FichaCliente>.NoEncontrado();
        if (!Versiones.Iguales(version, existente.RowVersion))
            return new ResultadoGestion<FichaCliente>.VersionObsoleta();
        // Revision de seguridad: nombre, apellido y telefono se validan por campo en vez de llegar a la base.
        var erroresDatos = ValidacionCuenta.Validar(entrada.Rut, entrada.Nombre, entrada.Apellido, entrada.Telefono, entrada.Direccion);
        if (erroresDatos.Count > 0)
            return new ResultadoGestion<FichaCliente>.Invalido(erroresDatos, "Revisa los datos del cliente.");

        var correo = string.IsNullOrWhiteSpace(entrada.Email) ? null : ServicioCodigosCuenta.NormalizarCorreo(entrada.Email);
        // Fase 14A: «cuenta web» es tener contraseña o un Google vinculado (quien nacio con Google no tiene contraseña).
        var tieneAccesoWeb = existente.PasswordHash is not null || await db.IdentidadesExternas.AnyAsync(i => i.IdCliente == id, ct);
        if (correo is null && tieneAccesoWeb)
            return new ResultadoGestion<FichaCliente>.Invalido(new Dictionary<string, string[]> { ["correo"] = ["Un cliente con cuenta web necesita un correo."] },
                "Un cliente con cuenta web necesita un correo.");
        if (correo is not null)
        {
            var errores = new Dictionary<string, string[]>();
            ValidacionCuenta.ValidarCorreo(correo, errores);
            if (errores.Count > 0)
                return new ResultadoGestion<FichaCliente>.Invalido(errores, "Revisa el correo del cliente.");
        }
        var cambioCorreo = !string.Equals(existente.Email, correo, StringComparison.OrdinalIgnoreCase);
        var activo = entrada.Activo ?? existente.Activo;
        var seDesactiva = existente.Activo && !activo;

        existente.Rut = ValidacionCuenta.NormalizarRut(entrada.Rut);
        existente.Nombre = entrada.Nombre!.Trim();
        existente.Apellido = entrada.Apellido!.Trim();
        existente.Telefono = entrada.Telefono!.Trim();
        existente.Email = correo;
        existente.Direccion = entrada.Direccion;
        existente.Activo = activo;
        db.Entry(existente).Property(c => c.Activo).IsModified = true;
        if (cambioCorreo)
            existente.FechaVerificacionCorreo = null;
        db.Entry(existente).Property(c => c.RowVersion).OriginalValue = version;
        // Guardar y cerrar sus sesiones van juntos (igual que al desactivar a un empleado).
        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new ResultadoGestion<FichaCliente>.VersionObsoleta();
            }
            if (seDesactiva || (cambioCorreo && tieneAccesoWeb))
                await sesiones.RevocarTodasAsync(TitularSesion.DeCliente(id), ct);
            await transaccion.CommitAsync(CancellationToken.None);
        }
        if (cambioCorreo && tieneAccesoWeb && existente.Activo)
        {
            await codigos.AnularPendientesAsync(TitularSesion.DeCliente(id), ct);
            await codigos.EnviarVerificacionAsync(existente, ct);
        }
        return new ResultadoGestion<FichaCliente>.Correcto((await ObtenerAsync(id, CancellationToken.None))!);
    }

    // Activa el acceso web de un cliente que ya existe, una vez que el personal comprobo su identidad (quien
    // lo hace ya confirmo su propia contraseña).
    public async Task<ResultadoGestion<bool>> AsignarCredencialesAsync(int id, string? password, string? correoEscrito, AuthService auth, CancellationToken ct)
    {
        if (!AuthService.EsPasswordValida(password))
            return new ResultadoGestion<bool>.Invalido(new Dictionary<string, string[]> { ["password"] = [AuthService.MensajePasswordInvalida] },
                AuthService.MensajePasswordInvalida);
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (cliente is null)
            return new ResultadoGestion<bool>.NoEncontrado();
        if (!cliente.Activo)
            return new ResultadoGestion<bool>.Conflicto("conflicto", "El cliente está desactivado: actívalo antes de darle acceso web.");

        // Sin correo no hay cuenta (fase 6): es el usuario y el canal para recuperar la contraseña.
        var correo = string.IsNullOrWhiteSpace(correoEscrito) ? cliente.Email : ServicioCodigosCuenta.NormalizarCorreo(correoEscrito);
        var errores = new Dictionary<string, string[]>();
        ValidacionCuenta.ValidarCorreo(correo ?? "", errores);
        if (errores.Count > 0)
            return new ResultadoGestion<bool>.Invalido(errores, "El cliente necesita un correo válido para tener cuenta web.");
        if (await db.Clientes.AnyAsync(c => c.Email == correo && c.IdCliente != id, ct))
            return new ResultadoGestion<bool>.Conflicto("conflicto", "Ese correo ya está en uso en otra cuenta.");

        var cambioCorreo = !string.Equals(cliente.Email, correo, StringComparison.OrdinalIgnoreCase);
        // Revision de seguridad: si la cuenta ya tenia contraseña, su correo anterior se entera del cambio.
        // Fase 14A: tambien si la cuenta es de Google (sin contraseña): su titular se entera de que el acceso se reasigno.
        var tieneAccesoWeb = cliente.PasswordHash is not null || await db.IdentidadesExternas.AnyAsync(i => i.IdCliente == id, ct);
        var correoAnterior = tieneAccesoWeb && Reglas.EsCorreo(cliente.Email) ? cliente.Email : null;
        cliente.Email = correo;
        if (cambioCorreo)
            cliente.FechaVerificacionCorreo = null;
        cliente.PasswordHash = auth.HashPassword(password!);
        // Fase 11: si habia dado de baja su cuenta, el personal (que comprobo su identidad) la habilita de nuevo. Los
        // avisos de estado siguen apagados hasta que el cliente los encienda.
        cliente.InhabilitadoEn = null;
        // La nueva contraseña y el cierre de las sesiones anteriores van juntos.
        await using (var transaccion = await db.Database.BeginTransactionAsync(ct))
        {
            await db.SaveChangesAsync(ct);
            await sesiones.RevocarTodasAsync(TitularSesion.DeCliente(id), ct);
            if (correoAnterior is not null)
            {
                await cola.EncolarAsync(Negocio.Correo.CorreosDeCuenta.AvisoAccesoReasignado(correoAnterior, cliente.Nombre, opcionesCorreo.Value.ContactoTaller), ct);
                await db.SaveChangesAsync(ct);
            }
            await transaccion.CommitAsync(CancellationToken.None);
        }
        if (correoAnterior is not null)
            cola.AvisarAlTrabajador();
        if (cliente.FechaVerificacionCorreo is null)
            await codigos.EnviarVerificacionAsync(cliente, ct);
        return new ResultadoGestion<bool>.Correcto(true);
    }
}
