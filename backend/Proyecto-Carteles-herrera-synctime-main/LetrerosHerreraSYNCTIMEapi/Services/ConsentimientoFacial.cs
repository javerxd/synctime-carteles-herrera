namespace LetrerosHerreraSYNCTIMEapi.Services;

// Texto que acepta el empleado al activar el rostro. Cubre lo que el articulo 16 ter de la ley 19.628
// (modificada por la ley 21.719) manda informar: sistema, finalidad, periodo y forma de ejercer derechos.
// BORRADOR: debe revisarlo un asesor legal antes de usarlo con datos reales.
// Cualquier cambio del texto exige cambiar Version, para que quede claro que acepto cada persona.
public static class ConsentimientoFacial
{
    public const string Version = "2026-11-06";

    public static readonly string[] Texto =
    [
        "Sistema biométrico: reconocimiento facial con la librería @vladmandic/face-api, que funciona en su navegador. A partir de la cámara calcula 128 números que representan su rostro. No se guardan fotos ni video, y esos números se guardan cifrados.",
        "Finalidad: únicamente comprobar su identidad al iniciar sesión en SyncTime.",
        "Período: mientras tenga activado este acceso. Los datos se borran si lo desactiva o si su cuenta de empleado queda inactiva. Solo se conserva la constancia: las fechas de aceptación y retiro, y la versión de este texto.",
        "Sus derechos: es voluntario. Si no acepta, sigue entrando con su contraseña, sin ninguna desventaja. Puede desactivarlo cuando quiera desde Mi perfil, sin dar motivos y sin costo. Para otras solicitudes sobre sus datos, diríjase a la administración de Letreros Herrera.",
        "Limitación: este sistema no puede garantizar que haya una persona presente, y una fotografía podría engañarlo. Por eso las acciones delicadas siguen pidiendo su contraseña."
    ];
}
