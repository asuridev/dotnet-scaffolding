namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 403.</summary>
public class ForbiddenException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 403, args, details, cause);
