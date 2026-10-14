namespace ProductCatalog.Domain.Idempotency;

using ProductCatalog.Domain.Errors;

/// <summary>La misma clave de idempotencia con un contenido DISTINTO.</summary>
/// <remarks>
/// El cliente prometió, al mandar la clave, que dos peticiones con ella son la misma; esta no lo es. Reproducir la
/// primera respuesta contestaría a otra pregunta, y ejecutarla rompería la promesa de la clave. Se detecta comparando la
/// firma del contenido con la que se guardó junto a la clave.
/// </remarks>
/// El code es el CANÓNICO del framework (docs/framework-errors.md): el diseño no declara uno propio, y eso es una
/// respuesta legítima. Para cambiarlo, declara en los errors de la operación un code de su familia con status 409.
public sealed class IdempotencyReuseException(string scope, string idempotencyKey)
    : ConflictException($"La clave de idempotencia {scope}/{idempotencyKey} ya se usó con un contenido distinto", code: "IDEMPOTENCY_KEY_REUSED", httpStatus: 409)
{}
