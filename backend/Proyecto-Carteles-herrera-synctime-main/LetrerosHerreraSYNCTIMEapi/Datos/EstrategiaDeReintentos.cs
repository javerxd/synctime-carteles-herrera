using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace LetrerosHerreraSYNCTIMEapi.Datos;

// Fase 16A (H7): reintentos para los fallos transitorios de Azure SQL (la base de la capa gratuita se pausa sola y la primera
// conexion despues de la pausa falla o tarda varios segundos; ademas, Azure corta conexiones al mover la base de nodo).
//
// EnableRetryOnFailure «a secas» no sirve aqui: EF Core lanza «la estrategia no admite transacciones iniciadas por el usuario»
// en cuanto el codigo abre una transaccion, y esta API abre unas cuarenta (BeginTransactionAsync en los servicios y en el
// interceptor de auditoria). Reescribirlas todas para ejecutarlas como una unidad reintentable es un cambio enorme y arriesgado.
//
// Esta estrategia hace lo seguro y suficiente:
//   - Fuera de una transaccion (lecturas, un SaveChanges suelto), reintenta como la estrategia de EF Core para SQL Server.
//   - Dentro de una transaccion manual, NO reintenta y NO lanza: ejecuta una vez y deja pasar el error. Si el fallo es transitorio,
//     la transaccion ya se perdio con la conexion, y repetir solo la ultima instruccion la escribiria fuera de la transaccion.
//     La peticion falla (500) y el cliente la repite; las rutas que crean ya aceptan Idempotency-Key (fase 11).
// Los reintentos se apagan con BaseDeDatos:ReintentosMaximos = 0.
//
// Limites que se aceptan (como cualquier uso de reintentos de EF Core sin verificar el commit):
//   - Si la conexion cae justo DESPUES de que el servidor confirmo un SaveChanges suelto, el reintento lo repetiria. Es muy improbable;
//     Idempotency-Key protege los reintentos del cliente, no los de esta capa.
//   - BeginTransaction* no pasa por la estrategia: si el primer contacto tras la pausa es una transaccion manual, no se reintenta aqui
//     y depende de «Connect Timeout=60» de la cadena de conexion y del reintento de conexion de SqlClient.
public sealed class EstrategiaDeReintentos(ExecutionStrategyDependencies dependencias, int reintentosMaximos, TimeSpan retardoMaximo)
    : SqlServerRetryingExecutionStrategy(dependencias, reintentosMaximos, retardoMaximo, errorNumbersToAdd: null)
{
    // Un contexto ya liberado no tiene transaccion que respetar; y no debe enmascarar el error original.
    private bool EnTransaccion
    {
        get
        {
            try
            {
                return Dependencies.CurrentContext.Context.Database.CurrentTransaction is not null;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }

    protected override void OnFirstExecution()
    {
        if (EnTransaccion)
        {
            // La base lanza aqui cuando ya hay una transaccion; esta estrategia la admite y simplemente no reintenta.
            ExceptionsEncountered.Clear();
            return;
        }
        base.OnFirstExecution();
    }

    protected override bool ShouldRetryOn(Exception exception) => !EnTransaccion && base.ShouldRetryOn(exception);
}
