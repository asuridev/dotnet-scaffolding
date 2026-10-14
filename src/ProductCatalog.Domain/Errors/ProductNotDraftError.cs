namespace ProductCatalog.Domain.Errors;

/// <summary>El producto no está en draft.</summary>
public sealed class ProductNotDraftError(string message)
    : ConflictException(message, code: "PRODUCT_NOT_DRAFT", httpStatus: 409);
