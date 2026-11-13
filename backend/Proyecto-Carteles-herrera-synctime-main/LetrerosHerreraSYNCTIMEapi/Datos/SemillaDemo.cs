using LetrerosHerreraSYNCTIMEapi.Infraestructura.Imagenes;
using SkiaSharp;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Negocio;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Datos;

// Datos sinteticos minimos para probar la aplicacion (RNFO12, reducido por decision del usuario).
// Ninguna persona es real: los nombres son inventados, los correos usan el dominio reservado ".test"
// y los RUT se eligieron en un rango alto (45 y 46 millones) para reducir la posibilidad de coincidir con el de alguien.
// Cada fase amplia esta semilla con lo que construye.
//
// Uso: dotnet run --project LetrerosHerreraSYNCTIMEapi -- sembrar-demo
public static class SemillaDemo
{
    public const string Comando = "sembrar-demo";
    public const string DominioCorreo = "@letreria.test";

    public static async Task<int> EjecutarAsync(IServiceProvider services, IConfiguration configuration, string nombreBase, ILogger logger)
    {
        // La semilla nunca escribe en la base real: solo acepta una cuyo nombre termine en "_Demo".
        if (!BasesDeDatos.EsDemo(nombreBase))
        {
            logger.LogError("La semilla solo se carga en una base cuyo nombre termine en {Sufijo}. La conexion actual apunta a {Base}.", BasesDeDatos.SufijoDemo, nombreBase);
            return 1;
        }

        // La contraseña de los usuarios de demostracion la define quien administra el equipo, nunca el codigo.
        var password = configuration["Demo:Password"];
        if (!AuthService.EsPasswordValida(password))
        {
            logger.LogError("Falta Demo:Password, o no es valida. {Regla} El README del backend indica como definirla sin que se muestre en pantalla.", AuthService.MensajePasswordInvalida);
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        await db.Database.MigrateAsync();

        var creada = await SembrarAsync(db, scope.ServiceProvider.GetRequiredService<AuthService>(), password!);
        if (creada)
            logger.LogInformation("Semilla de demostracion cargada en {Base}. Usuarios: dueno, admin, disenador, operario y cliente, todos {Dominio}.", nombreBase, DominioCorreo);
        else
            logger.LogInformation("La base {Base} ya tenia la semilla de demostracion: no se cambio nada.", nombreBase);

        // Los pedidos se cargan aparte, para que una base demo ya sembrada tambien los reciba.
        if (await SembrarPedidosAsync(db))
            logger.LogInformation("Pedidos de demostracion cargados en {Base}: todos los estados del pedido y de pago, con abonos.", nombreBase);

        // Fase 5: la vitrina con imagenes de ejemplo (dibujadas aqui, rotuladas como tales).
        var publicadas = await SembrarVitrinaAsync(db, scope.ServiceProvider.GetRequiredService<AlmacenImagenes>());
        if (publicadas > 0)
            logger.LogInformation("Vitrina de demostracion: {Cantidad} trabajo(s) con imagen de ejemplo.", publicadas);
        return 0;
    }

    private sealed record TrabajoDemo(string Titulo, string Descripcion, int AnchoMm, int AltoMm, int Precio, string Producto, string Categoria, ImagenesDeEjemplo.Diseno Diseno);

    private static readonly TrabajoDemo[] Vitrina =
    [
        new("Cartel metálico para ferretería", "Estructura metálica con gráfica impresa y terminación mate.", 2400, 1200, 180000, "Cartel metálico impreso", "Letreros",
            new("FERRETERÍA EL TRIGAL", "Materiales de construcción", new SKColor(0xE7, 0xE5, 0xE4), new SKColor(0x0F, 0x17, 0x2A), SKColors.White, ImagenesDeEjemplo.Forma.Fachada)),
        new("Lienzo publicitario para evento", "Lienzo de PVC con ojetillos en todo el perímetro.", 3000, 1000, 45000, "Lienzo publicitario", "Letreros",
            new("FERIA DE TEMPORADA", "Sábado y domingo · Plaza de Armas", new SKColor(0xD6, 0xD3, 0xD1), new SKColor(0xB4, 0x53, 0x09), SKColors.White, ImagenesDeEjemplo.Forma.Lienzo)),
        new("Letrero tipo palomita para cafetería", "Letrero plegable de doble cara para la vereda.", 600, 900, 60000, "Letrero tipo Palomita", "Letreros",
            new("CAFÉ", "Menú del día", new SKColor(0xCB, 0xD5, 0xE1), new SKColor(0x1E, 0x29, 0x3B), new SKColor(0xFD, 0xE6, 0x8A), ImagenesDeEjemplo.Forma.Palomita)),
        new("Letras corpóreas para clínica dental", "Letras de acrílico con luz LED posterior.", 2000, 500, 320000, "Cartel metálico impreso", "Letreros",
            new("CLÍNICA SONRÍE", "Odontología familiar", new SKColor(0xF1, 0xF5, 0xF9), new SKColor(0x0E, 0x74, 0x90), SKColors.White, ImagenesDeEjemplo.Forma.Fachada)),
        new("Rotulación de camioneta de reparto", "Vinilo impreso con laminado protector en ambos costados.", 4200, 1500, 260000, "Rotulación de vehículo", "Vehículos",
            new("FLETES BIOBÍO", "Mudanzas y despachos", new SKColor(0xE2, 0xE8, 0xF0), new SKColor(0xF8, 0xFA, 0xFC), new SKColor(0x0F, 0x17, 0x2A), ImagenesDeEjemplo.Forma.Vehiculo)),
        new("Poleras estampadas para equipo", "Estampado en serigrafía a dos colores.", 300, 400, 8500, "Polera estampada", "Estampados",
            new("CLUB DEPORTIVO", "Temporada 2026", new SKColor(0xE5, 0xE7, 0xEB), new SKColor(0x15, 0x80, 0x3D), SKColors.White, ImagenesDeEjemplo.Forma.Lienzo)),
        new("Mural para fachada de minimarket", "Pintura acrílica para exteriores con sello UV.", 6000, 2500, 450000, "Mural pintado", "Muros",
            new("MINIMARKET DON LUCHO", "Abierto todos los días", new SKColor(0xFE, 0xF3, 0xC7), new SKColor(0xC2, 0x41, 0x0C), SKColors.White, ImagenesDeEjemplo.Forma.Muro))
    ];

    // Trabajos de la vitrina con su imagen de ejemplo. Repetirla solo completa lo que falta: agrega los
    // trabajos y tipos de letrero que no existen y la imagen a los trabajos demo que aun no la tienen.
    public static async Task<int> SembrarVitrinaAsync(LetrerosHerreradbContext db, AlmacenImagenes almacen)
    {
        ExigirBaseDeDemostracion(db);
        var dueno = await db.Empleados.Where(e => e.Email == $"dueno{DominioCorreo}").Select(e => (int?)e.IdEmpleado).FirstOrDefaultAsync();
        if (dueno is null)
            return 0;

        var completados = 0;
        foreach (var demo in Vitrina)
        {
            var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Nombre == demo.Categoria)
                ?? db.Categorias.Add(new Categoria { Nombre = demo.Categoria }).Entity;
            var producto = await db.Productos.FirstOrDefaultAsync(p => p.Nombre == demo.Producto)
                ?? db.Productos.Add(new Producto { Nombre = demo.Producto, Categoria = categoria }).Entity;
            var trabajo = await db.Catalogos.FirstOrDefaultAsync(c => c.Titulo == demo.Titulo)
                ?? db.Catalogos.Add(new Catalogo
                {
                    Producto = producto, IdEmpleadoCreador = dueno, Titulo = demo.Titulo, Descripcion = demo.Descripcion,
                    AnchoMm = demo.AnchoMm, AltoMm = demo.AltoMm, PrecioReferencial = demo.Precio
                }).Entity;
            if (trabajo.IdImagen is not null)
                continue;

            var procesada = ProcesadorImagenes.Procesar(ImagenesDeEjemplo.Dibujar(demo.Diseno));
            var clave = AlmacenImagenes.NuevaClave();
            await almacen.GuardarAsync(clave, procesada, CancellationToken.None);
            trabajo.ImagenUrl = null;
            trabajo.Imagen = new Imagen
            {
                Clave = clave, Ancho = procesada.Ancho, Alto = procesada.Alto, BytesGrande = procesada.Grande.Length,
                BytesMiniatura = procesada.Miniatura.Length, EsEjemplo = true, IdEmpleadoSube = dueno.Value, FechaSubida = DateTimeOffset.UtcNow
            };
            await db.SaveChangesAsync();
            completados++;
        }
        return completados;
    }

    // Devuelve false, sin cambiar nada, si la semilla ya estaba cargada.
    public static async Task<bool> SembrarAsync(LetrerosHerreradbContext db, AuthService auth, string password)
    {
        ExigirBaseDeDemostracion(db);
        if (await db.Empleados.AnyAsync(e => e.Email.EndsWith(DominioCorreo)))
            return false;

        var hash = auth.HashPassword(password);

        var dueno = Empleado(45000001, "Marta", "Fuentes", "900000101", "dueno", Roles.Dueno, hash);
        var administrador = Empleado(45000002, "Pablo", "Rojas", "900000102", "admin", Roles.Administrador, hash);
        var disenador = Empleado(45000003, "Daniela", "Soto", "900000103", "disenador", Roles.Disenador, hash);
        var operario = Empleado(45000004, "Hugo", "Vera", "900000104", "operario", Roles.Operario, hash);
        db.Empleados.AddRange(dueno, administrador, disenador, operario);

        // Tres clientes: uno con cuenta web, y dos que solo existen porque el personal los registro.
        var conCuenta = new Cliente
        {
            Rut = RutDe(46000001), Nombre = "Rosa", Apellido = "Pino", Telefono = "900000201",
            Email = $"cliente{DominioCorreo}", Direccion = "Av. Alemania 100, Los Ángeles", PasswordHash = hash
        };
        var presencial = new Cliente { Rut = RutDe(46000002), Nombre = "Luis", Apellido = "Araya", Telefono = "900000202" };
        var sinRut = new Cliente { Nombre = "Camila", Apellido = "Reyes", Telefono = "900000203" };
        db.Clientes.AddRange(conCuenta, presencial, sinRut);

        // Publicaciones de la vitrina. Las imagenes definitivas llegan en una fase posterior.
        db.Catalogos.AddRange(
            new Catalogo
            {
                IdProducto = 1, EmpleadoCreador = dueno, Titulo = "Cartel metálico para ferretería",
                Descripcion = "Estructura metálica con gráfica impresa y terminación mate.",
                ImagenUrl = "/imagenes/demo/cartel-metalico.webp", AnchoMm = 2400, AltoMm = 1200, PrecioReferencial = 180000
            },
            new Catalogo
            {
                IdProducto = 2, EmpleadoCreador = dueno, Titulo = "Lienzo publicitario para evento",
                Descripcion = "Lienzo de PVC con ojetillos en todo el perímetro.",
                ImagenUrl = "/imagenes/demo/lienzo-evento.webp", AnchoMm = 3000, AltoMm = 1000, PrecioReferencial = 45000
            },
            new Catalogo
            {
                IdProducto = 3, EmpleadoCreador = administrador, Titulo = "Letrero tipo palomita para cafetería",
                Descripcion = "Letrero plegable de doble cara para la vereda.",
                ImagenUrl = "/imagenes/demo/palomita-cafeteria.webp", AnchoMm = 600, AltoMm = 900, PrecioReferencial = 60000
            });

        // Solicitudes en estado inicial: dos registradas en el local y una enviada desde la web.
        db.Solicitudes.AddRange(
            new Solicitud
            {
                Cliente = presencial, EmpleadoRegistra = operario, DescripcionGeneral = "Cartel para el frente del local.",
                Detalles = { new SolicitudDetalle { IdProducto = 1, Descripcion = "Cartel con el nombre del negocio", AnchoMm = 2000, AltoMm = 1000, Cantidad = 1 } }
            },
            new Solicitud
            {
                Cliente = sinRut, EmpleadoRegistra = operario, DescripcionGeneral = "Dos letreros para la vereda.",
                Detalles = { new SolicitudDetalle { IdProducto = 3, Descripcion = "Palomita con la carta del día", AnchoMm = 600, AltoMm = 900, Cantidad = 2 } }
            },
            new Solicitud
            {
                Cliente = conCuenta, DescripcionGeneral = "Lienzo para una feria.",
                Detalles = { new SolicitudDetalle { IdProducto = 2, Descripcion = "Lienzo con logo y fecha", AnchoMm = 3000, AltoMm = 1000, Cantidad = 1 } }
            });

        await db.SaveChangesAsync();
        return true;
    }

    private sealed record PedidoDemo(
        string Cliente, int Producto, string Trabajo, int Monto, int Instalacion, string Origen, string Estado,
        int MesesAtras, int Dia, int DiasEntrega, int? DiasEntregaReal, string? Factura, (int Monto, string Medio, int DiasDespues)[] Abonos);

    // Datos inventados para la revision: cubren cada estado del pedido y de pago, los dos origenes
    // (cotizacion y presencial directo), un cliente habitual con pedido entregado sin pagar y entregas a
    // tiempo y atrasadas, repartidos en los ultimos seis meses.
    private static readonly PedidoDemo[] Pedidos =
    [
        new("trigal", 1, "Letrero metálico de fachada", 185000, 0, "cotizacion", "en_proceso", 0, 4, 12, null, null, [(100000, "transferencia", 0)]),
        new("biobio", 2, "Lienzos para liquidación de temporada", 96000, 0, "cotizacion", "entregado", 0, 2, 5, 4, null, []),
        new("sonrie", 1, "Letras metálicas para la recepción", 240000, 35000, "cotizacion", "terminado", 1, 28, 10, null, null, [(275000, "transferencia", 1)]),
        new("lucho", 3, "Palomita de doble cara para la vereda", 54000, 0, "directo", "aceptado", 0, 6, 7, null, null, []),
        new("ruiz", 1, "Cartel metálico para la bodega", 410000, 0, "cotizacion", "entregado", 1, 15, 14, 13, "F-1021", [(200000, "transferencia", 0), (210000, "transferencia", 13)]),
        new("900000202", 3, "Palomita para cafetería", 72000, 0, "cotizacion", "anulado", 2, 1, 7, null, null, []),
        new("900000201", 2, "Lienzo con logo para una feria", 128000, 0, "cotizacion", "entregado", 2, 10, 6, 8, null, [(100000, "efectivo", 0)]),
        new("biobio", 1, "Cartel de horario de atención", 64000, 0, "directo", "entregado", 3, 5, 4, 4, "F-0998", [(64000, "tarjeta_debito", 4)]),
        new("trigal", 2, "Lienzo de temporada", 45000, 0, "cotizacion", "entregado", 4, 12, 5, 5, "F-0975", [(45000, "efectivo", 5)]),
        new("sonrie", 3, "Señalética para el estacionamiento", 88000, 0, "cotizacion", "en_proceso", 0, 1, 3, null, null, []),
        new("900000203", 1, "Cartel para el almacén", 150000, 25000, "directo", "entregado", 5, 20, 10, 12, null, [(175000, "transferencia", 12)]),
        new("ruiz", 2, "Lienzos para la feria del transporte", 99000, 0, "cotizacion", "terminado", 0, 3, 6, null, null, [(30000, "efectivo", 0)]),
    ];

    /// <summary>
    /// Pedidos de demostracion con su solicitud, su cotizacion aprobada y sus abonos. Devuelve false, sin
    /// cambiar nada, si ya hay pedidos o si falta la semilla del personal.
    /// </summary>
    public static async Task<bool> SembrarPedidosAsync(LetrerosHerreradbContext db)
    {
        ExigirBaseDeDemostracion(db);
        if (await db.Pedidos.AnyAsync())
            return false;
        var personal = await db.Empleados.Where(e => e.Email.EndsWith(DominioCorreo)).ToDictionaryAsync(e => e.Email);
        if (!personal.TryGetValue($"dueno{DominioCorreo}", out var dueno)
            || !personal.TryGetValue($"operario{DominioCorreo}", out var operario)
            || !personal.TryGetValue($"disenador{DominioCorreo}", out var disenador))
            return false;

        var clientes = new Dictionary<string, Cliente>();
        foreach (var (clave, nombre, apellido, telefono, rut) in new[]
        {
            ("trigal", "Panadería", "El Trigal", "900000211", 46000011),
            ("biobio", "Ferretería", "Biobío", "900000212", 46000012),
            ("sonrie", "Clínica Dental", "Sonríe", "900000213", 46000013),
            ("lucho", "Minimarket", "Don Lucho", "900000214", 46000014),
            ("ruiz", "Transportes", "Ruiz", "900000215", 46000015),
        })
        {
            clientes[clave] = await db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono)
                ?? new Cliente { Rut = RutDe(rut), Nombre = nombre, Apellido = apellido, Telefono = telefono };
        }
        foreach (var telefono in new[] { "900000201", "900000202", "900000203" })
        {
            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono);
            if (cliente is not null)
                clientes[telefono] = cliente;
        }

        var hoy = DateTime.Today;
        DateTime Tope(DateTime fecha) => fecha > hoy ? hoy : fecha;

        foreach (var d in Pedidos.Where(d => clientes.ContainsKey(d.Cliente)))
        {
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-d.MesesAtras);
            var fecha = Tope(inicioMes.AddDays(d.Dia - 1).AddHours(10));
            var directo = d.Origen == "directo";
            var solicitud = new Solicitud
            {
                Cliente = clientes[d.Cliente],
                // La de Rosa Pino llega desde la web (sin empleado); las demas, desde el mostrador.
                EmpleadoRegistra = d.Cliente == "900000201" ? null : operario,
                Estado = "aceptado",
                FechaSolicitud = fecha.AddDays(directo ? 0 : -3),
                DescripcionGeneral = d.Trabajo,
                Detalles = { new SolicitudDetalle { IdProducto = d.Producto, Descripcion = d.Trabajo, Cantidad = 1 } },
            };
            var cotizacion = new Cotizacion
            {
                Solicitud = solicitud,
                Empleado = disenador,
                FechaCotizacion = fecha.AddDays(directo ? 0 : -1),
                CostoManoObra = d.Monto,
                Estado = "aprobada",
                Observaciones = directo ? "Generada en el mostrador junto con el pedido." : null,
            };
            var pedido = new Pedido
            {
                Solicitud = solicitud,
                Cotizacion = cotizacion,
                Origen = d.Origen,
                EmpleadoResponsable = operario,
                FechaPedido = fecha,
                FechaEntregaEstimada = fecha.AddDays(d.DiasEntrega),
                FechaEntregaReal = d.Estado == "entregado" && d.DiasEntregaReal is { } real ? Tope(fecha.AddDays(real)) : null,
                ModalidadEntrega = d.Instalacion > 0 ? "instalacion_terreno" : "retiro_local",
                RequiereInstalacion = d.Instalacion > 0,
                DireccionInstalacion = d.Instalacion > 0 ? "Dirección del cliente, Los Ángeles" : null,
                CostoInstalacionAdicional = d.Instalacion,
                MontoBaseTrabajo = d.Monto,
                EstadoPedido = d.Estado,
                NumeroFactura = d.Factura,
            };
            db.Pedidos.Add(pedido);
            foreach (var abono in d.Abonos)
            {
                db.Pagos.Add(new Pago
                {
                    Pedido = pedido,
                    Monto = abono.Monto,
                    MedioPago = abono.Medio,
                    FechaPago = DateOnly.FromDateTime(Tope(fecha.AddDays(abono.DiasDespues))),
                    EmpleadoRegistra = dueno,
                });
            }
        }

        await db.SaveChangesAsync();
        return true;
    }

    // Segunda barrera, por si alguien llama a la semilla sin pasar por EjecutarAsync: solo bases demo o de pruebas.
    private static void ExigirBaseDeDemostracion(LetrerosHerreradbContext db)
    {
        var nombre = db.Database.GetDbConnection().Database;
        if (!BasesDeDatos.EsDemo(nombre) && !BasesDeDatos.EsDePruebas(nombre))
            throw new InvalidOperationException($"La semilla de demostracion no se carga en la base {nombre}.");
    }

    private static Empleado Empleado(int cuerpoRut, string nombre, string apellido, string telefono, string usuario, string rol, string hash) => new()
    {
        Rut = RutDe(cuerpoRut), Nombre = nombre, Apellido = apellido, Telefono = telefono,
        Email = $"{usuario}{DominioCorreo}", PasswordHash = hash, Rol = rol
    };

    private static string RutDe(int cuerpo) => $"{cuerpo}-{Rut.DigitoVerificador(cuerpo.ToString())}";
}
