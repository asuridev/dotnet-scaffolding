namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 422.</summary>
public class BusinessException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 422, args, details, cause);
