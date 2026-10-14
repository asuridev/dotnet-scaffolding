namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductCatalog.Domain.Idempotency;
using ProductCatalog.Infrastructure.Persistence.Rows;

/// <summary>
/// Adaptador EF Core del registro de idempotencia. Usa el DbContext del ámbito, el de la transacción del caso de uso: el
/// registro y el efecto del comando se confirman o revierten juntos, porque una clave marcada sin recurso detrás haría
/// que el reintento devolviese una respuesta que nunca existió.
/// </summary>
/// <remarks>
/// Carreras: dos peticiones simultáneas con la misma clave insertan la misma clave primaria y el MOTOR arbitra: la que
/// pierde espera a que la otra confirme y choca. Sale como <see cref="IdempotencyConflictException"/> (su code, no un 409
/// anónimo indistinguible de un conflicto de negocio), sin la causa del motor detrás: el traductor de la transacción no
/// tiene que volver a mirarla.
/// </remarks>
public sealed class IdempotencyStore(ProductCatalogDbContext db, PersistenceTransactions transactions) : IIdempotencyStore
{
    public async Task<StoredRequest?> Find(string scope, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var found = await db.Set<IdempotencyRecordRow>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.OperationScope == scope && r.IdempotencyKey == idempotencyKey, cancellationToken);
        // Una fila caducada es como si no estuviera: la ventana la fija el diseño, no la purga.
        return found is null || found.ExpiresAt <= DateTimeOffset.UtcNow ? null : new StoredRequest(found.Signature, found.ResourceId);
    }

    public async Task Save(string scope, string idempotencyKey, string signature, string? resourceId, int ttlSeconds, CancellationToken cancellationToken = default)
    {
        await transactions.InTransaction(
            async () =>
            {
                var now = DateTimeOffset.UtcNow;
                // Una fila CADUCADA es como si no estuviera —es lo que ya asume Find—, y tiene que serlo aquí: si no, la
                // clave quedaría inutilizable entre su caducidad y la purga, con un 409 de clave en curso todo ese tiempo.
                await db.Set<IdempotencyRecordRow>()
                    .Where(r => r.OperationScope == scope && r.IdempotencyKey == idempotencyKey && r.ExpiresAt <= now)
                    .ExecuteDeleteAsync(cancellationToken);
                var row = new IdempotencyRecordRow
                {
                    OperationScope = scope,
                    IdempotencyKey = idempotencyKey,
                    Signature = signature,
                    ResourceId = resourceId,
                    CreatedAt = now,
                    ExpiresAt = now.AddSeconds(ttlSeconds)
                };
                db.Add(row);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException error) when (PersistenceErrors.EngineError(error) is { SqlState: PostgresErrorCodes.UniqueViolation } engine
                    && string.Equals(engine.ConstraintName, "idempotency_record_pkey", StringComparison.OrdinalIgnoreCase))
                {
                    db.Entry(row).State = EntityState.Detached;
                    throw new IdempotencyConflictException(scope, idempotencyKey);
                }
                return true;
            },
            readOnly: false,
            cancellationToken);
    }
}
