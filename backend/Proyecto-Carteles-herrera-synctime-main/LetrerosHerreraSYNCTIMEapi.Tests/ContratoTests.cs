using System.Net;
using System.Text;
using System.Text.Json;
using LetrerosHerreraSYNCTIMEapi.Security;
using Xunit;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Contrato primero (RNFP19). Las rutas se declaran en docs/api/openapi.json ANTES de implementarlas:
// esta prueba falla mientras el contrato y la API no digan lo mismo, en cualquiera de los dos sentidos.
[Collection(ApiCollection.Name)]
public class ContratoTests(ApiFixture api)
{
    private static readonly string[] Verbos = ["get", "post", "put", "patch", "delete"];
    private static readonly string[] Accesos = ["publico", "cliente", "personal", "gestion", "sesion"];

    private static string RutaDelContrato()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docs", "api", "openapi.json")))
            carpeta = carpeta.Parent;
        Assert.True(carpeta is not null, "No se encontro docs/api/openapi.json subiendo desde la carpeta de pruebas.");
        return Path.Combine(carpeta!.FullName, "docs", "api", "openapi.json");
    }

    private static Dictionary<string, JsonElement> Operaciones(JsonElement documento)
    {
        var operaciones = new Dictionary<string, JsonElement>();
        foreach (var ruta in documento.GetProperty("paths").EnumerateObject())
        {
            foreach (var operacion in ruta.Value.EnumerateObject().Where(o => Verbos.Contains(o.Name)))
                operaciones[$"{operacion.Name.ToUpperInvariant()} {ruta.Name}"] = operacion.Value;
        }
        return operaciones;
    }

    [Fact]
    public async Task ElContratoYLaApi_DeclaranLasMismasRutas()
    {
        var generado = await api.Cliente().GetAsync("/openapi/v1.json").EsperarAsync(HttpStatusCode.OK);

        // Ayuda para quien edita el contrato: deja en un archivo lo que la API expone hoy.
        if (Environment.GetEnvironmentVariable("SYNCTIME_VOLCAR_OPENAPI") is { Length: > 0 } destino)
            await File.WriteAllTextAsync(destino, generado.GetRawText());

        using var contrato = JsonDocument.Parse(await File.ReadAllTextAsync(RutaDelContrato()));
        var enApi = Operaciones(generado).Keys.ToHashSet();
        var enContrato = Operaciones(contrato.RootElement).Keys.ToHashSet();

        var sinDeclarar = enApi.Except(enContrato).Order().ToList();
        var sinImplementar = enContrato.Except(enApi).Order().ToList();

        Assert.True(sinDeclarar.Count == 0 && sinImplementar.Count == 0,
            $"El contrato y la API no coinciden.\nEn la API pero no en el contrato ({sinDeclarar.Count}):\n  {string.Join("\n  ", sinDeclarar)}" +
            $"\nEn el contrato pero no en la API ({sinImplementar.Count}):\n  {string.Join("\n  ", sinImplementar)}");
    }

    // El acceso declarado no es solo documentacion: se compara con lo que la API permite de verdad.
    // Cada operacion se llama como visitante, cliente y tres roles del personal. Las peticiones con
    // cuerpo envian un JSON que no se puede leer: quien tiene permiso recibe 400 antes de que se
    // ejecute nada, y quien no lo tiene recibe 401 o 403. Asi la prueba no modifica datos.
    [Fact]
    public async Task ElAccesoDeclarado_CoincideConLoQueLaApiPermite()
    {
        using var contrato = JsonDocument.Parse(await File.ReadAllTextAsync(RutaDelContrato()));
        var actores = new (string Nombre, string? Rol, string? Token)[]
        {
            ("visitante", null, null),
            ("cliente", Roles.Cliente, (await api.RegistrarClienteAsync()).Token),
            ("operario", Roles.Operario, await api.TokenEmpleadoAsync(Roles.Operario)),
            ("diseñador", Roles.Disenador, await api.TokenEmpleadoAsync(Roles.Disenador)),
            ("administrador", Roles.Administrador, await api.TokenAdminAsync())
        };

        var diferencias = new List<string>();
        foreach (var (clave, operacion) in Operaciones(contrato.RootElement))
        {
            var partes = clave.Split(' ');
            var metodo = new HttpMethod(partes[0]);
            var acceso = operacion.GetProperty("x-acceso").GetString()!;
            var roles = operacion.TryGetProperty("x-roles", out var lista) ? lista.EnumerateArray().Select(r => r.GetString()).ToArray() : null;

            // Un DELETE sin {id} actua sobre algo propio de quien lo llama (su acceso con rostro) y no lleva
            // cuerpo que lo frene: se ejecutaria de verdad. De esas rutas solo se comprueba a quien se rechaza.
            var borraLoPropio = metodo == HttpMethod.Delete && !partes[1].Contains("{id}");

            foreach (var actor in actores)
            {
                if (borraLoPropio && Permite(acceso, roles, actor.Rol))
                    continue;

                using var peticion = new HttpRequestMessage(metodo, partes[1].Replace("{id}", "999999"));
                if (metodo != HttpMethod.Get && metodo != HttpMethod.Delete)
                    peticion.Content = new StringContent("\"cuerpo ilegible a proposito\"", Encoding.UTF8, "application/json");

                using var respuesta = await api.Cliente(actor.Token).SendAsync(peticion);
                var rechazado = respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
                var debePermitir = Permite(acceso, roles, actor.Rol);

                if (rechazado == debePermitir)
                    diferencias.Add($"{clave} como {actor.Nombre}: el contrato dice {(debePermitir ? "permitido" : "rechazado")} y la API respondio {(int)respuesta.StatusCode}");
            }
        }

        Assert.True(diferencias.Count == 0, $"El acceso declarado no coincide con la API:\n  {string.Join("\n  ", diferencias)}");
    }

    private static bool Permite(string acceso, string?[]? roles, string? rol) => acceso switch
    {
        "publico" => true,
        "sesion" => rol is not null,
        "cliente" => rol == Roles.Cliente,
        "personal" => Roles.Empleados.Contains(rol) && (roles is null || roles.Contains(rol)),
        "gestion" => rol is Roles.Dueno or Roles.Administrador,
        _ => false
    };

    [Fact]
    public async Task CadaOperacionDelContrato_DiceQueHaceYQuienPuedeUsarla()
    {
        using var contrato = JsonDocument.Parse(await File.ReadAllTextAsync(RutaDelContrato()));

        var incompletas = Operaciones(contrato.RootElement)
            .Where(o => !o.Value.TryGetProperty("summary", out var resumen) || string.IsNullOrWhiteSpace(resumen.GetString())
                     || !o.Value.TryGetProperty("x-acceso", out var acceso) || !Accesos.Contains(acceso.GetString()))
            .Select(o => o.Key)
            .Order();

        Assert.Empty(incompletas);
    }
}
