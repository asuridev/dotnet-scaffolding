namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductCatalog.Domain.Errors;

/// <summary>
/// Conflicto de bloqueo optimista: otra escritura subió la versión del agregado (o lo borró) entre que este lo leyó y
/// lo guardó. Es un 409 y NO se reintenta.
/// </summary>
public sealed class OptimisticLockConflict(string entity, string id)
    : Exception($"{entity} {id}: la versión cambió desde que se leyó");

/// <summary>Un conflicto de escritura transitorio (interbloqueo) que agotó sus reintentos: sale como 409.</summary>
public sealed class WriteConflictExhaustedException(int attempts, Exception cause)
    : Exception($"Conflicto de escritura concurrente tras {attempts} intentos", cause);

/// <summary>El motor cortó la transacción por su tope: transitorio, y la transacción revirtió entera (503 con Retry-After).</summary>
public sealed class TransactionTimeoutException(Exception cause) : Exception(DefaultMessage, cause)
{
    public const string Code = "TRANSACTION_TIMEOUT";

    public const int Status = 503;

    public const string DefaultMessage = "La operación no terminó a tiempo; reinténtala";
}

/// <summary>Una violación de integridad que no casa con ninguna constraint conocida: 409 sin <c>code</c>, como keel-spring.</summary>
public sealed class IntegrityViolationException(Exception cause) : Exception(DefaultMessage, cause)
{
    public const string DefaultMessage = "Violación de integridad de datos: alguna restricción no se cumplió";
}

/// <summary>
/// Traduce un fallo de la persistencia al contrato. Los nombres de constraint los pone el esquema
/// (keel-core/gen/relational.js) y el error, con su mensaje, keel-core/gen/constraint-errors.js: los tres servidores
/// del diseño traducen la misma violación al mismo error.
/// </summary>
public static class PersistenceErrors
{
    private static readonly Dictionary<string, Func<DomainException>> ConstraintToError = new(StringComparer.Ordinal)
    {
        // Unicidad de Product.sku: el error que el diseño declara para ella.
        ["uk_products_natural"] = () => new SkuAlreadyExistsError("Ya existe un Product con ese sku"),
    };

    /// <summary>El error de PostgreSQL de la cadena de causas, o null.</summary>
    public static PostgresException? EngineError(Exception error)
    {
        for (Exception? cause = error; cause is not null; cause = cause.InnerException)
        {
            if (cause is PostgresException engine)
            {
                return engine;
            }
        }
        return null;
    }

    /// <summary>¿Un interbloqueo o un fallo de serialización? Se arregla repitiendo la transacción entera.</summary>
    public static bool IsTransientWriteConflict(Exception error) =>
        EngineError(error)?.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure;

    /// <summary>
    /// El error del contrato de un fallo de persistencia, o null si no es de persistencia:
    /// conflicto de versión o interbloqueo agotado → 409 de concurrencia (el del diseño si lo declara); violación de
    /// una constraint conocida → su error; desconocida → 409 sin code; tope de transacción → 503.
    /// </summary>
    public static Exception? Translate(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is OptimisticLockConflict or WriteConflictExhaustedException or DbUpdateConcurrencyException)
        {
            return new ConflictException("El recurso fue modificado por otra operación concurrente; reintenta con el estado actual", code: "CONCURRENT_MODIFICATION", httpStatus: 409, details: [], cause: error);
        }
        var engine = EngineError(error);
        if (engine is null)
        {
            return null;
        }
        // Sentencia cancelada por statement_timeout (también la espera de un bloqueo).
        if (engine.SqlState is PostgresErrorCodes.QueryCanceled or PostgresErrorCodes.LockNotAvailable)
        {
            return new TransactionTimeoutException(error);
        }
        // Clase 23: violaciones de integridad (unicidad, FK, NOT NULL, CHECK).
        if (engine.SqlState.StartsWith("23", StringComparison.Ordinal))
        {
            var constraint = engine.ConstraintName?.ToLowerInvariant();
            return constraint is not null && ConstraintToError.TryGetValue(constraint, out var build) ? build() : new IntegrityViolationException(error);
        }
        return null;
    }
}
