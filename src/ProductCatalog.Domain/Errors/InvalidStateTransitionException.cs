namespace ProductCatalog.Domain.Errors;

/// <summary>Transición de lifecycle no declarada en el diseño.</summary>
public sealed class InvalidStateTransitionException(string from, string to)
    : ConflictException($"Transición de estado no permitida: {from} -> {to}", code: "INVALID_STATE_TRANSITION", httpStatus: 409);
