using System.Globalization;
using System.Security.Claims;

namespace LetrerosHerreraSYNCTIMEapi.Security;

// Valores exactos de la columna empleados.rol y del claim de rol del JWT.
public static class Roles
{
    public const string Administrador = "administrador";
    public const string Dueno = "dueño";
    public const string Disenador = "diseñador";
    public const string Operario = "operario";
    public const string Cliente = "cliente";

    public static readonly string[] Empleados = [Administrador, Dueno, Disenador, Operario];
}

public static class Politicas
{
    public const string Empleado = "Empleado";
    public const string Gestion = "Gestion";
    public const string Cliente = "Cliente";
}

public static class ClaimsPrincipalExtensions
{
    // Id del usuario autenticado: id_empleado o id_cliente segun el tipo de token.
    public static int IdUsuario(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
