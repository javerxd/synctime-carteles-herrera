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
        return 0;
    }

    // Devuelve false, sin cambiar nada, si la semilla ya estaba cargada.
    public static async Task<bool> SembrarAsync(LetrerosHerreradbContext db, AuthService auth, string password)
    {
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

    private static Empleado Empleado(int cuerpoRut, string nombre, string apellido, string telefono, string usuario, string rol, string hash) => new()
    {
        Rut = RutDe(cuerpoRut), Nombre = nombre, Apellido = apellido, Telefono = telefono,
        Email = $"{usuario}{DominioCorreo}", PasswordHash = hash, Rol = rol
    };

    private static string RutDe(int cuerpo) => $"{cuerpo}-{Rut.DigitoVerificador(cuerpo.ToString())}";
}
