namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 400.</summary>
public class BadRequestException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 400, args, details, cause);
