namespace ProductCatalog.Domain.Errors;

/// <summary>El precio del producto es cero.</summary>
public sealed class ProductPriceNotPositiveError(string message)
    : BusinessException(message, code: "PRODUCT_PRICE_NOT_POSITIVE", httpStatus: 422);
