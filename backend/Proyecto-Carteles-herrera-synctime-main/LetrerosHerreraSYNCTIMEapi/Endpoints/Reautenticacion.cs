using LetrerosHerreraSYNCTIMEapi.Api;
using System.Security.Claims;
using LetrerosHerreraSYNCTIMEapi.Models;
using LetrerosHerreraSYNCTIMEapi.Security;
using LetrerosHerreraSYNCTIMEapi.Services;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Las acciones delicadas piden de nuevo la contraseña: el token no indica como se inicio la sesion,
// y una sesion obtenida con el rostro no debe bastar para ellas.
// El error se responde con 400 porque el frontend cierra la sesion ante cualquier 401.
internal static class Reautenticacion
{
    public const string Incorrecta = "La contraseña actual no es correcta.";

    // Devuelve el empleado con sesion si esta activo y su contraseña es correcta.
    public static async Task<Empleado?> ConfirmarAsync(ClaimsPrincipal user, string? password, LetrerosHerreradbContext db, AuthService auth)
    {
        if (string.IsNullOrEmpty(password))
            return null;

        var id = user.IdUsuario();
        var empleado = await db.Empleados.FirstOrDefaultAsync(e => e.IdEmpleado == id && e.Activo);
        return empleado is not null && auth.VerifyPassword(password, empleado.PasswordHash) ? empleado : null;
    }

    public static IResult Rechazo() => ErrorApi.Invalido(Incorrecta);
}
