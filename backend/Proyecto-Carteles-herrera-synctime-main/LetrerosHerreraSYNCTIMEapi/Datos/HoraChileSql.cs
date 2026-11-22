namespace LetrerosHerreraSYNCTIMEapi.Datos;

// Fase 16A (H3): los valores por defecto de las fechas «sin zona» (datetime2 y date) se guardan en hora de Chile, calculada desde el
// instante UTC con la zona de Windows que SQL Server conoce tambien en Linux y en Azure SQL. SYSDATETIME() daba la hora LOCAL del
// servidor: correcta solo mientras SQL Server corria en hora de Chile (Azure SQL y los contenedores corren en UTC).
// SYSDATETIMEOFFSET() lleva el desfase del servidor, asi que el resultado no depende de la zona en que corra.
// Estas constantes las usa el modelo; la migracion HoraDeChileEnLaBase escribe las expresiones completas (una migracion no cambia).
public static class HoraChileSql
{
    public const string Zona = "Pacific SA Standard Time";
    public const string Ahora = "CONVERT(datetime2(0), SYSDATETIMEOFFSET() AT TIME ZONE 'Pacific SA Standard Time')";
    public const string Hoy = "CONVERT(date, SYSDATETIMEOFFSET() AT TIME ZONE 'Pacific SA Standard Time')";
}
