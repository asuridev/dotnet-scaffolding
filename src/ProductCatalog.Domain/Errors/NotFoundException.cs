namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 404.</summary>
public class NotFoundException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 404, args, details, cause);
