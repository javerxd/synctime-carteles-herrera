namespace LetrerosHerreraSYNCTIMEapi.Services;

// Texto que acepta el empleado al registrar el rostro. Cubre lo que el articulo 16 ter de la ley 19.628
// (modificada por la ley 21.719) manda informar: sistema, finalidad, periodo y forma de ejercer derechos.
// BORRADOR: debe revisarlo un asesor legal antes de usarlo con datos reales.
// Cualquier cambio del texto exige cambiar Version, para que quede claro que acepto cada persona.
public static class ConsentimientoFacial
{
    // Fase 10: cambio el texto (tres muestras, prueba de vida y solo gestion), asi que cambio la version.
    public const string Version = "2026-11-15";

    public static readonly string[] Texto =
    [
        "Sistema biométrico: reconocimiento facial con la librería @vladmandic/face-api, que funciona en su navegador. A partir de la cámara calcula 128 números que representan su rostro. Al registrarlo se guardan tres muestras de esos números (de frente y con la cabeza girada hacia cada lado), cifradas. No se guardan fotos ni video.",
        "Prueba de vida: al entrar se le pide girar la cabeza en un orden que elige el sistema. Para medir el giro se usan 68 puntos de su rostro, que no se guardan.",
        "Finalidad: únicamente comprobar su identidad al iniciar sesión en SyncTime. Solo lo usan el dueño y la administración.",
        "Período: mientras tenga activado este acceso. Los datos se borran si lo desactiva o si su cuenta de empleado queda inactiva. Solo se conserva la constancia: las fechas de aceptación y retiro, y la versión de este texto.",
        "Sus derechos: es voluntario. Si no acepta, sigue entrando con su contraseña, sin ninguna desventaja. Puede desactivarlo cuando quiera desde Mi perfil, sin dar motivos y sin costo. Para otras solicitudes sobre sus datos, diríjase a la administración de Letreros Herrera.",
        "Limitación: la prueba de vida dificulta engañar al sistema con una fotografía o una pantalla, pero no lo impide por completo. Por eso el acceso con rostro exige además un equipo autorizado y las acciones delicadas siguen pidiendo su contraseña."
    ];
}
