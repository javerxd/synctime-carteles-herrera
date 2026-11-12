using Microsoft.Data.SqlClient;

namespace LetrerosHerreraSYNCTIMEapi.Datos;

// Reglas sobre que base de datos admite operaciones automaticas, segun su nombre.
// La base real nunca cumple ninguna: sus migraciones se aplican a mano, con respaldo previo.
public static class BasesDeDatos
{
    public const string SufijoDemo = "_Demo";
    public const string PrefijoPruebas = "SyncTime_Pruebas_";

    public static string Nombre(string cadenaDeConexion) =>
        new SqlConnectionStringBuilder(cadenaDeConexion).InitialCatalog;

    // Base de desarrollo y demostracion: se puede sembrar y regenerar.
    public static bool EsDemo(string nombre) => nombre.EndsWith(SufijoDemo, StringComparison.OrdinalIgnoreCase);

    // Bases temporales de las pruebas automaticas: las unicas que el codigo de pruebas puede eliminar.
    public static bool EsDePruebas(string nombre) => nombre.StartsWith(PrefijoPruebas, StringComparison.Ordinal);

    public static bool AdmiteMigracionAutomatica(string nombre) => EsDemo(nombre) || EsDePruebas(nombre);
}
