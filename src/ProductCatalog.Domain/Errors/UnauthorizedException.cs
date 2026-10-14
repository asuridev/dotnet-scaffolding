namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 401.</summary>
public class UnauthorizedException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 401, args, details, cause);
