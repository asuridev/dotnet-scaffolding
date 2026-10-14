namespace ProductCatalog.Domain.Errors;

/// <summary>Ya existe un producto con ese sku.</summary>
public sealed class SkuAlreadyExistsError(string message)
    : ConflictException(message, code: "SKU_ALREADY_EXISTS", httpStatus: 409);
