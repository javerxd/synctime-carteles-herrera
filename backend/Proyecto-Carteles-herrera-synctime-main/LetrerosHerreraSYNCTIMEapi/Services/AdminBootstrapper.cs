using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Services;

// Sin empleados nadie puede iniciar sesion ni crear otros empleados. Este paso crea el primero
// desde la seccion Bootstrap:Admin, para que la password nunca quede en el codigo ni en una migracion.
public static class AdminBootstrapper
{
    public static async Task CrearPrimerAdministradorAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var admin = configuration.GetSection("Bootstrap:Admin");
        if (string.IsNullOrWhiteSpace(admin["Email"]) && string.IsNullOrWhiteSpace(admin["Password"]))
            return;

        string Requerido(string clave) => string.IsNullOrWhiteSpace(admin[clave])
            ? throw new InvalidOperationException($"Falta Bootstrap:Admin:{clave} en la configuracion.")
            : admin[clave]!;

        // Despues del primer arranque hay que quitar la password: su ausencia no es un error si ya hay empleados.
        var passwordConfigurada = admin["Password"];
        if (!string.IsNullOrWhiteSpace(passwordConfigurada) && !AuthService.EsPasswordValida(passwordConfigurada))
            throw new InvalidOperationException($"Bootstrap:Admin:Password no es valida. {AuthService.MensajePasswordInvalida}");

        var rol = admin["Rol"] ?? Roles.Administrador;
        if (!Roles.Empleados.Contains(rol))
            throw new InvalidOperationException($"Bootstrap:Admin:Rol invalido. Valores validos: {string.Join(", ", Roles.Empleados)}");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LetrerosHerreradbContext>();
        if (await db.Empleados.AnyAsync())
        {
            logger.LogInformation("Bootstrap:Admin ignorado porque ya existen empleados. Se puede quitar de la configuracion.");
            return;
        }

        var password = Requerido("Password");
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var email = Requerido("Email");
        db.Empleados.Add(new Empleado
        {
            Rut = Requerido("Rut"),
            Nombre = Requerido("Nombre"),
            Apellido = Requerido("Apellido"),
            Telefono = Requerido("Telefono"),
            Email = email,
            PasswordHash = auth.HashPassword(password),
            Rol = rol
        });
        await db.SaveChangesAsync();

        logger.LogWarning("Primer empleado creado ({Email}). Quite Bootstrap:Admin:Password de la configuracion.", email);
    }
}
