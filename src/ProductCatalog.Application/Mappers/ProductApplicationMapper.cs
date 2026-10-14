namespace ProductCatalog.Application.Mappers;

using ProductCatalog.Application.Dtos;
using ProductCatalog.Domain.Aggregate;
using ProductCatalog.Domain.Enums;
using ProductCatalog.Domain.ValueObject;

/// <summary>Traduce Product a los DTOs de las operaciones que lo devuelven.</summary>
public sealed class ProductApplicationMapper
{
    public CreateProductResponseDto ToCreateProductResponseDto(Product entity) =>
        new()
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Notes = entity.Notes,
            Price = entity.Price,
            Status = entity.Status,
        };

    public GetProductResponseDto ToGetProductResponseDto(Product entity) =>
        new()
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Notes = entity.Notes,
            Price = entity.Price,
            Status = entity.Status,
        };

    public ListProductsResponseDto ToListProductsResponseDto(Product entity) =>
        new()
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Name = entity.Name,
            Notes = entity.Notes,
            Price = entity.Price,
            Status = entity.Status,
        };
}
