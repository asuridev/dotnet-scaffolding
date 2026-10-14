namespace ProductCatalog.Domain.Idempotency;

using ProductCatalog.Domain.Errors;

/// <summary>Dos peticiones con la misma clave de idempotencia, a la vez.</summary>
/// <remarks>
/// No es el reintento normal —ese encuentra el registro ya confirmado y reproduce la respuesta—: es la ventana en la
/// que la primera aún no ha confirmado, así que no hay respuesta que reproducir. La transacción de quien pierde revierte
/// entera: de dos peticiones idénticas se ejecutó exactamente una.
/// </remarks>
/// El code es el CANÓNICO del framework (docs/framework-errors.md): el diseño no declara uno propio, y eso es una
/// respuesta legítima. Para cambiarlo, declara en los errors de la operación un code de su familia con status 409.
public sealed class IdempotencyConflictException(string scope, string idempotencyKey)
    : ConflictException($"Otra petición con la misma clave de idempotencia está en curso: {scope}/{idempotencyKey}", code: "IDEMPOTENCY_KEY_IN_PROGRESS", httpStatus: 409)
{}
