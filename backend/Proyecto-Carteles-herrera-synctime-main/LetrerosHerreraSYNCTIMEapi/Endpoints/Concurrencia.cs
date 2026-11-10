namespace LetrerosHerreraSYNCTIMEapi.Endpoints;

// Evita que una edicion hecha con datos viejos pise el trabajo de otra persona.
// Solo solicitudes y pedidos tienen row_version en la base.
public static class Concurrencia
{
    public const string VersionObsoleta = "El registro fue modificado por otra persona. Vuelva a cargarlo.";

    // Devuelve la respuesta de error, o null si la version enviada es la vigente.
    // Si otro guarda justo despues de esta comprobacion, lo detecta EF al guardar y ApiExceptionHandler responde 409.
    public static IResult? Verificar(byte[]? enviada, byte[] vigente)
    {
        if (enviada is null or { Length: 0 })
            return Results.BadRequest(new { error = "Indique rowVersion: es el valor que entrega el GET del registro." });

        return enviada.AsSpan().SequenceEqual(vigente)
            ? null
            : Results.Conflict(new { error = VersionObsoleta });
    }
}
