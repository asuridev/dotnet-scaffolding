namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 409.</summary>
public class ConflictException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 409, args, details, cause);
