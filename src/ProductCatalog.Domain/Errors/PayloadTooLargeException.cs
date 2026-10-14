namespace ProductCatalog.Domain.Errors;

/// <summary>Errores de dominio que responden 413.</summary>
public class PayloadTooLargeException(
    string? message,
    string? code = null,
    int? httpStatus = null,
    IReadOnlyList<object?>? args = null,
    IReadOnlyList<string>? details = null,
    Exception? cause = null)
    : DomainException(message, code, httpStatus ?? 413, args, details, cause);
