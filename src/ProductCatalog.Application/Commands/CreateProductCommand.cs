namespace ProductCatalog.Application.Commands;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Domain.ValueObject;

/// <summary>Da de alta un producto en estado draft.</summary>
public sealed class CreateProductCommand : IReturningCommand<CreateProductResponseDto>
{
    // El formato del value type SKU (^[A-Z]{3}-[0-9]{4}$) se valida YA en la entrada: mal formado
    // es un 400 antes que cualquier precondición. No lo valides otra vez al principio del handler.
    // El dominio lo repite con SKUFormat.Validate(...) (src/ProductCatalog.Domain/ValueObject/SKUFormat.cs)
    // en el factory o el método de negocio de la entidad que recibe el valor: es lo que cubre lo que no entra por
    // la API (un evento, una operación interna), y lo exige el gate check-domain-guards.sh.
    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required string? Notes { get; init; }

    public required Money Price { get; init; }

    /// <summary>Token de sincronización con el proveedor; nunca se expone.</summary>
    public required string? ApiToken { get; init; }

    /// <summary>Ámbito de la clave de idempotencia: la operación (sin partitionBy la clave es GLOBAL entre llamantes: lo decidió el diseño). No lo compongas a mano.</summary>
    public string IdempotencyScope() => "createProduct";
}
