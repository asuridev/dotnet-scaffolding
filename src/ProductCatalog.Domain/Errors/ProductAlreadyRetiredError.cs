namespace ProductCatalog.Domain.Errors;

/// <summary>El producto ya está retired.</summary>
public sealed class ProductAlreadyRetiredError(string message)
    : ConflictException(message, code: "PRODUCT_ALREADY_RETIRED", httpStatus: 409);
