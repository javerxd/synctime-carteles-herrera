using Microsoft.Data.SqlClient;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Fase 16B (RNFO15): arma la cadena de conexion de las pruebas de integracion.
//   TEST_SQL_SERVER    servidor (por omision, la instancia local de SQL Server Express).
//   TEST_SQL_PASSWORD  si existe, se usa autenticacion de SQL Server (el pipeline, contra un contenedor, donde no hay Windows).
//   TEST_SQL_USER      usuario de esa autenticacion (por omision «sa»).
// Sin TEST_SQL_PASSWORD todo sigue como siempre: autenticacion de Windows. Un usuario sin clave es un error (no se cae en Windows en
// silencio: en el pipeline eso daria un fallo de conexion confuso). La cadena se arma con SqlConnectionStringBuilder para que una clave con
// «;» o comillas no pueda agregar opciones. Los mensajes de error NUNCA incluyen la clave.
public static class ConexionDePruebas
{
    public const string ServidorPredeterminado = @"localhost\SQLEXPRESS";

    public static string DesdeEntorno(Func<string, string?> leer, string baseDeDatos)
    {
        // El servidor y el usuario se recortan (un salto de linea de mas daria un «Login failed» confuso); la CLAVE jamas: puede llevar espacios.
        var servidor = leer("TEST_SQL_SERVER")?.Trim();
        var usuario = leer("TEST_SQL_USER")?.Trim();
        var clave = leer("TEST_SQL_PASSWORD");

        var partes = new SqlConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(servidor) ? ServidorPredeterminado : servidor,
            InitialCatalog = baseDeDatos,
            TrustServerCertificate = true,
            // Esperas amplias: en un equipo con poca memoria libre (o en un contenedor recien arrancado) SQL Server tarda en aceptar
            // conexiones y en crear la base, y un fallo por tiempo de espera no dice nada sobre el codigo que se prueba.
            ConnectTimeout = 60,
            CommandTimeout = 180
        };

        if (string.IsNullOrEmpty(clave))
        {
            if (usuario is not null)
                throw new InvalidOperationException("TEST_SQL_USER esta definida pero falta TEST_SQL_PASSWORD: defina las dos o ninguna.");
            partes.IntegratedSecurity = true;
        }
        else
        {
            if (usuario is not null && string.IsNullOrWhiteSpace(usuario))
                throw new InvalidOperationException("TEST_SQL_USER esta vacia: defina el usuario o quite la variable (por omision, «sa»).");
            partes.IntegratedSecurity = false;
            partes.UserID = usuario ?? "sa";
            partes.Password = clave;
        }
        return partes.ConnectionString;
    }
}
