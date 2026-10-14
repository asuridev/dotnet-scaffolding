namespace ProductCatalog.Domain.Idempotency;

/// <summary>Lo guardado de la primera ejecución de una petición.</summary>
/// <param name="Signature">La firma del contenido (CommandSignature) con la que se usó la clave.</param>
/// <param name="ResourceId">El id del recurso resultante, para reconstruir la respuesta; null si la operación no crea nada.</param>
public sealed record StoredRequest(string Signature, string? ResourceId);

/// <summary>
/// Registro de peticiones ya atendidas, por clave de idempotencia.
/// </summary>
/// <remarks>
/// El contrato del diseño no es rechazar la repetición, sino REPRODUCIRLA: la segunda llamada con la misma clave y el
/// mismo contenido devuelve la respuesta de la primera, sin volver a ejecutar nada. Por eso se guarda el id del recurso
/// resultante y una firma del contenido (para detectar la reutilización de la clave con otro cuerpo, que tiene su propio
/// error: <see cref="IdempotencyReuseException"/>).
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>El registro previo, si esa clave ya se usó y no ha caducado.</summary>
    /// <param name="scope">command.IdempotencyScope(), que build compone desde el diseño (la operación y su partitionBy): no se compone a mano.</param>
    /// <param name="idempotencyKey">La cabecera Idempotency-Key (client-key), CommandSignature.Of(command) (payload-hash) o el campo de la clave (payload-field).</param>
    /// <param name="cancellationToken">El del handler.</param>
    Task<StoredRequest?> Find(string scope, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra la primera ejecución, DENTRO de la transacción del caso de uso: si el comando revierte, el registro
    /// revierte con él. Dos peticiones con la misma clave a la vez chocan en la clave primaria del registro, y la que
    /// pierde sale como <see cref="IdempotencyConflictException"/>: no la captures.
    /// </summary>
    /// <param name="scope">El ámbito, como en <see cref="Find"/>.</param>
    /// <param name="idempotencyKey">La clave, como en <see cref="Find"/>.</param>
    /// <param name="signature">CommandSignature.Of(command).</param>
    /// <param name="resourceId">El id del recurso que esta ejecución va a crear, o null.</param>
    /// <param name="ttlSeconds">La ventana de deduplicación del diseño (idempotency.ttlSeconds).</param>
    /// <param name="cancellationToken">El del handler.</param>
    Task Save(string scope, string idempotencyKey, string signature, string? resourceId, int ttlSeconds, CancellationToken cancellationToken = default);
}
