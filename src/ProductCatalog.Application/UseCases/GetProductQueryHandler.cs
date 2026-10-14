namespace ProductCatalog.Application.UseCases;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Application.Mappers;
using ProductCatalog.Application.Queries;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Domain.Errors;

/// <summary>Recupera un producto por su identificador.</summary>
public sealed class GetProductQueryHandler(ProductApplicationMapper productApplicationMapper, IProductRepository productRepository) : IQueryHandler<GetProductQuery, GetProductResponseDto>
{
    private readonly ProductApplicationMapper productApplicationMapper = productApplicationMapper;
    private readonly IProductRepository productRepository = productRepository;

    public async Task<GetProductResponseDto> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        var product = await productRepository.FindById(query.Id, cancellationToken)
            ?? throw new ProductNotFoundError($"No existe el producto {query.Id}");
        return productApplicationMapper.ToGetProductResponseDto(product);
    }
}
