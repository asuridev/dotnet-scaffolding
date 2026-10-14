namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductCatalog.Infrastructure.UseCase;
using System.Globalization;

/// <summary>
/// La transacción del caso de uso sobre el DbContext del ámbito: la abre el UseCaseMediator (las consultas en solo
/// lectura) y los adaptadores de repositorio se UNEN a ella sin verla. Una escritura sin transacción abarcadora (un
/// barrido despachado sin ella) abre la suya.
/// </summary>
/// <remarks>
/// <para>
/// El tope: <c>SET LOCAL statement_timeout</c> con DB_TRANSACTION_TIMEOUT, al abrir. Es por SENTENCIA (espera de bloqueo
/// incluida), no por la transacción entera, como el setQueryTimeout de Hibernate; lo cortado sale como 503.
/// </para>
/// <para>
/// Un interbloqueo o un fallo de serialización se arreglan repitiendo la transacción ENTERA —el caso de uso otra vez,
/// sobre el estado nuevo— hasta 3 intentos; agotados, son el 409 de concurrencia. Un conflicto de
/// versión NO se repite: reaplicar la intención del cliente sobre un estado que no vio escondería la actualización
/// perdida.
/// </para>
/// </remarks>
public sealed partial class PersistenceTransactions(ProductCatalogDbContext db, DatabaseSettings settings, ILogger<PersistenceTransactions> logger) : IUseCaseTransactions
{
    /// <summary>Intentos de una transacción de escritura que pierde un conflicto transitorio (keel-core/gen/transactions.js).</summary>
    public const int WriteConflictAttempts = 3;

    private readonly string statementTimeout = string.Create(
        CultureInfo.InvariantCulture,
        $"SET LOCAL statement_timeout = {(long)settings.TransactionTimeout.TotalMilliseconds}");

    public Task<T> Run<T>(Func<Task<T>> work, bool readOnly, CancellationToken cancellationToken) => InTransaction(work, readOnly, cancellationToken);

    /// <summary>
    /// Ejecuta <paramref name="work"/> en una transacción: la ABIERTA si la hay (se une a ella, como la propagación por
    /// defecto de Spring) o una nueva, que confirma si termina y revierte si lanza. Los fallos del motor salen ya
    /// traducidos al contrato (PersistenceErrors).
    /// </summary>
    public async Task<T> InTransaction<T>(Func<Task<T>> work, bool readOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (db.Database.CurrentTransaction is not null)
        {
            return await work();
        }
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await Once(work, readOnly, cancellationToken);
            }
            catch (Exception error) when (!readOnly && attempt < WriteConflictAttempts && PersistenceErrors.IsTransientWriteConflict(error))
            {
                // Lo que la transacción revertida dejó en el contexto ya no es verdad: el intento siguiente lee de nuevo.
                db.ChangeTracker.Clear();
                LogRetry(attempt, error.GetType().Name);
            }
            catch (Exception error) when (PersistenceErrors.IsTransientWriteConflict(error))
            {
                db.ChangeTracker.Clear();
                throw PersistenceErrors.Translate(new WriteConflictExhaustedException(attempt, error))!;
            }
            catch (Exception error) when (PersistenceErrors.Translate(error) is { } translated)
            {
                db.ChangeTracker.Clear();
                throw translated;
            }
        }
    }

    private async Task<T> Once<T>(Func<Task<T>> work, bool readOnly, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Solo lectura primero: SET TRANSACTION tiene que ir antes de cualquier consulta.
        if (readOnly)
        {
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        }
        await db.Database.ExecuteSqlRawAsync(statementTimeout, cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflicto de escritura transitorio en el intento {Attempt} ({Type}): se repite la transacción")]
    private partial void LogRetry(int attempt, string type);
}
