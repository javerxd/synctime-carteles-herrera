using System.Text.RegularExpressions;
using LetrerosHerreraSYNCTIMEapi.Datos.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace LetrerosHerreraSYNCTIMEapi.UnitTests;

// Reglas de la arquitectura en capas (RNFO10), comprobadas sobre el codigo fuente:
//   Api (Endpoints, Middleware, Api)  ->  Negocio (Negocio, Services)  ->  Datos (Datos, Repositories, Models)
// Cada capa solo conoce a las que tiene a su derecha.
public class ArquitecturaTests
{
    // Archivos de Endpoints/ que todavia consultan la base directamente, sin pasar por un servicio de
    // negocio y un repositorio. Vienen de antes del plan y se migran en la fase que rehace cada modulo.
    // Esta lista solo puede achicarse: al terminar la fase 17 debe estar vacia.
    private static readonly string[] EndpointsPendientesDeMigrar =
    [
        "AuthApi.cs"           // fase 6 (RostroApi.cs y Reautenticacion.cs salieron de la lista en la fase 10)
    ];

    private static readonly string Proyecto = UbicarProyecto();

    private static string UbicarProyecto()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "LetrerosHerreraSYNCTIMEapi.slnx")))
            carpeta = carpeta.Parent;
        Assert.True(carpeta is not null, "No se encontro la carpeta de la solucion.");
        return Path.Combine(carpeta!.FullName, "LetrerosHerreraSYNCTIMEapi");
    }

    // Una carpeta de capa puede quedar vacia (git no guarda carpetas vacias): en la fase 7 el antiguo
    // SolicitudRepository dio paso a ServicioSolicitudes y Repositories quedo sin archivos.
    private static IEnumerable<(string Nombre, string Texto)> Fuentes(string carpeta) =>
        !Directory.Exists(Path.Combine(Proyecto, carpeta))
            ? []
            : Directory.EnumerateFiles(Path.Combine(Proyecto, carpeta), "*.cs", SearchOption.AllDirectories)
                .Select(ruta => (Path.GetFileName(ruta), File.ReadAllText(ruta)));

    private static bool UsaLaBase(string texto) => texto.Contains("LetrerosHerreradbContext");

    [Fact]
    public void UnEndpointNuevo_NoConsultaLaBaseDirectamente()
    {
        var infractores = Fuentes("Endpoints")
            .Where(f => UsaLaBase(f.Texto) && !EndpointsPendientesDeMigrar.Contains(f.Nombre))
            .Select(f => f.Nombre);

        Assert.Empty(infractores);
    }

    [Fact]
    public void LaListaDePendientes_NoConservaModulosYaMigrados()
    {
        var fuentes = Fuentes("Endpoints").ToDictionary(f => f.Nombre, f => f.Texto);

        // Si un archivo ya no usa la base, hay que quitarlo de la lista para que no pueda volver atras.
        var obsoletos = EndpointsPendientesDeMigrar.Where(nombre => !fuentes.TryGetValue(nombre, out var texto) || !UsaLaBase(texto));

        Assert.Empty(obsoletos);
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("LetrerosHerreradbContext")]
    [InlineData("IResult")]
    // El negocio compone los correos y decide sobre las imagenes; enviarlos y procesarlas es de Infraestructura.
    [InlineData("MailKit")]
    [InlineData("MimeKit")]
    [InlineData("SkiaSharp")]
    public void ElNegocio_NoConoceHttpNiLaBase(string prohibido)
    {
        var infractores = Fuentes("Negocio").Where(f => f.Texto.Contains(prohibido)).Select(f => f.Nombre);
        Assert.Empty(infractores);
    }

    [Theory]
    [InlineData("LetrerosHerreraSYNCTIMEapi.Api")]
    [InlineData("LetrerosHerreraSYNCTIMEapi.Endpoints")]
    [InlineData("Microsoft.AspNetCore.Http")]
    [InlineData("IResult")]
    public void LosDatos_NoConocenLaCapaDeApi(string prohibido)
    {
        var infractores = Fuentes("Datos").Concat(Fuentes("Repositories")).Concat(Fuentes("Infraestructura"))
            .Where(f => f.Texto.Contains(prohibido)).Select(f => f.Nombre);
        Assert.Empty(infractores);
    }

    // Los destinatarios de los avisos del taller salen de un solo servicio (IDestinatariosAvisos): desde la
    // fase 9 lee primero los destinos activos de la base y, si no hay, Correo:AvisosTaller. Si otro archivo
    // leyera esa configuracion, se saltaria los destinos que el dueño guardo desde su perfil.
    [Fact]
    public void SoloUnArchivo_ConoceLaConfiguracionDeLosAvisosDelTaller()
    {
        var lectores = Directory.EnumerateFiles(Proyecto, "*.cs", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !ruta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(ruta => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(ruta), @"\bAvisosTaller\b"))
            .Select(ruta => Path.GetRelativePath(Proyecto, ruta).Replace(Path.DirectorySeparatorChar, '/'));

        Assert.Equal(["Datos/Correo/DestinatariosAvisos.cs"], lectores);
    }

    // ── Fase 11: auditoria ──

    // Las entidades principales del plan: el interceptor de la auditoria debe cubrirlas todas.
    private static readonly Type[] Principales =
    [
        typeof(Models.Solicitud), typeof(Models.SolicitudDetalle), typeof(Models.Cotizacion), typeof(Models.Pedido), typeof(Models.Pago),
        typeof(Models.Catalogo), typeof(Models.Producto), typeof(Models.Categoria), typeof(Models.Material), typeof(Models.Cliente),
        typeof(Models.Empleado), typeof(Models.DestinatarioAviso), typeof(Models.RostroEmpleado), typeof(Models.HorarioAtencion), typeof(Models.CartelDigital)
    ];

    [Fact]
    public void LaAuditoria_CubreCadaEntidadPrincipal_YCadaEntidadNuevaTieneQueDecidirse()
    {
        foreach (var tipo in Principales)
            Assert.True(ReglasAuditoria.Entidades.ContainsKey(tipo), $"{tipo.Name} no esta en la auditoria.");

        // El modelo se arma sin conectarse a ninguna base.
        var opciones = new DbContextOptionsBuilder<Models.LetrerosHerreradbContext>().UseSqlServer("Server=sin-servidor;Database=sin-base").Options;
        using var contexto = new Models.LetrerosHerreradbContext(opciones);
        var tipos = contexto.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() is not null).Select(e => e.ClrType).ToList();
        var sinDecidir = tipos.Where(t => !ReglasAuditoria.Entidades.ContainsKey(t) && !ReglasAuditoria.NoAuditadas.ContainsKey(t)).Select(t => t.Name);
        Assert.Empty(sinDecidir);
        Assert.All(ReglasAuditoria.NoAuditadas, n => Assert.False(string.IsNullOrWhiteSpace(n.Value)));
    }

    // Escrituras que el interceptor no ve (ExecuteUpdate, ExecuteDelete) sobre las tablas auditadas: solo las de
    // esta lista, y cada archivo con una de ellas registra a mano (IAuditoria). Las sentencias que no nombran su
    // DbSet (pasan por una variable o un metodo) se revisaron una por una y se cuentan aparte.
    private static readonly (string Archivo, string DbSet, int Cantidad, string Motivo)[] EscriturasJustificadas =
    [
        ("Services/ServiciosSolicitudes.cs", "Solicitudes", 2, "borrado logico condicionado en un UPDATE; renovar la version al cambiar un adjunto"),
        ("Services/ServiciosSolicitudes.cs", "Cotizaciones", 1, "rechazar la cotizacion pendiente al editar o rechazar la solicitud"),
        ("Services/ServiciosPedidos.cs", "Pedidos", 2, "factura que trae el abono; borrado logico condicionado"),
        ("Services/ServiciosRostro.cs", "RostrosEmpleado", 4, "contadores de intentos (tecnicos) y borrado de las muestras"),
        ("Services/ServiciosMantenedores.cs", "RostrosEmpleado", 1, "borrar el rostro al desactivar a un empleado")
    ];

    // Sentencias sin el DbSet a la vista, revisadas: ninguna toca una tabla auditada.
    private static readonly (string Archivo, int Cantidad)[] IndireccionesRevisadas =
    [
        ("Infraestructura/Correo/ColaCorreoTrabajador.cs", 3), // propio y Disponibles: correos_salientes
        ("Services/ServiciosComunes.cs", 1),                    // DelTitular: consentimientos
        ("Services/ServiciosCuenta.cs", 3)                      // DelTitular y Pendientes: codigos_cuenta
    ];

    private static readonly string[] TablasAuditadas =
    [
        "solicitudes", "solicitud_detalles", "cotizaciones", "pedidos", "pagos", "catalogo", "productos", "categorias", "materiales",
        "clientes", "empleados", "destinatarios_avisos", "rostros_empleado", "horarios_atencion", "cartel_digital"
    ];

    private static IEnumerable<(string Ruta, string[] Lineas)> FuentesDeProduccion() =>
        Directory.EnumerateFiles(Proyecto, "*.cs", SearchOption.AllDirectories)
            .Where(ruta => !ruta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !ruta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !ruta.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Select(ruta => (Path.GetRelativePath(Proyecto, ruta).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllLines(ruta)));

    // La sentencia que llega hasta la linea indicada: hacia atras, hasta la linea que cierra la anterior.
    private static string Sentencia(string[] lineas, int indice)
    {
        var partes = new List<string> { lineas[indice] };
        for (var i = indice - 1; i >= 0; i--)
        {
            var texto = lineas[i].Trim();
            if (texto.EndsWith(';') || texto.EndsWith('{') || texto.EndsWith('}') || texto.StartsWith("//", StringComparison.Ordinal))
                break;
            partes.Insert(0, texto);
        }
        return string.Join(' ', partes);
    }

    [Fact]
    public void NingunaEscrituraInvisible_SobreLasTablasAuditadas()
    {
        var dbSets = typeof(Models.LetrerosHerreradbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToDictionary(p => p.Name, p => p.PropertyType.GetGenericArguments()[0]);
        var encontradas = new Dictionary<string, int>();
        var indirectas = new Dictionary<string, int>();
        var sqlDirecto = new List<string>();

        foreach (var (ruta, lineas) in FuentesDeProduccion())
        {
            for (var i = 0; i < lineas.Length; i++)
            {
                if (lineas[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (Regex.IsMatch(lineas[i], @"\.Execute(Update|Delete)(Async)?\("))
                {
                    var usados = Regex.Matches(Sentencia(lineas, i), @"\bdb\.(\w+)").Select(m => m.Groups[1].Value)
                        .Where(dbSets.ContainsKey).Take(1).ToList(); // la primera tabla es la que se escribe; las demas (un Any) solo se leen
                    if (usados.Count == 0)
                        indirectas[ruta] = indirectas.GetValueOrDefault(ruta) + 1;
                    foreach (var usado in usados.Where(u => ReglasAuditoria.Entidades.ContainsKey(dbSets[u])))
                        encontradas[$"{ruta} {usado}"] = encontradas.GetValueOrDefault($"{ruta} {usado}") + 1;
                }
                if (Regex.IsMatch(lineas[i], @"ExecuteSql(Raw|Interpolated)?(Async)?\("))
                {
                    var siguientes = string.Join(' ', lineas.Skip(i).Take(4));
                    if (TablasAuditadas.Any(t => Regex.IsMatch(siguientes, $@"\b(UPDATE|DELETE\s+FROM|INSERT\s+INTO)\s+(dbo\.)?\[?{t}\b", RegexOptions.IgnoreCase)))
                        sqlDirecto.Add($"{ruta}:{i + 1}");
                }
            }
        }

        Assert.Empty(sqlDirecto);
        Assert.Equal(EscriturasJustificadas.Select(e => $"{e.Archivo} {e.DbSet} x{e.Cantidad}").Order(),
            encontradas.Select(e => $"{e.Key} x{e.Value}").Order());
        Assert.Equal(IndireccionesRevisadas.Select(r => $"{r.Archivo} x{r.Cantidad}").Order(),
            indirectas.Select(r => $"{r.Key} x{r.Value}").Order());

        // Cada archivo con una excepcion registra a mano en la auditoria.
        foreach (var archivo in EscriturasJustificadas.Select(e => e.Archivo).Distinct())
            Assert.Matches(@"auditoria\.(RegistrarAsync|Anotar)\(", File.ReadAllText(Path.Combine(Proyecto, archivo)));
    }
}
