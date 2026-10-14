namespace ProductCatalog.Domain.Errors;

/// <summary>No existe un producto con ese id.</summary>
public sealed class ProductNotFoundError(string message)
    : NotFoundException(message, code: "PRODUCT_NOT_FOUND", httpStatus: 404);
