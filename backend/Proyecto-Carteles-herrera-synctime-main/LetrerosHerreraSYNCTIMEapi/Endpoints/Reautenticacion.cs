using LetrerosHerreraSYNCTIMEapi.Api;

namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Las acciones delicadas piden de nuevo la contraseña: el token no indica como se inicio la sesion,
// y una sesion obtenida con el rostro no debe bastar para ellas.
// El error se responde con 400 porque el frontend cierra la sesion ante cualquier 401.
// Comprobar la contraseña es de ServicioReautenticacion (fase 9); aqui queda solo la respuesta.
internal static class Reautenticacion
{
    public const string Incorrecta = "La contraseña actual no es correcta.";

    public static IResult Rechazo() => ErrorApi.Invalido(Incorrecta);
}
