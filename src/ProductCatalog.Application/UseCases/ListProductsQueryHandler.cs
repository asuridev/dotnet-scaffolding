namespace ProductCatalog.Application.UseCases;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Application.Mappers;
using ProductCatalog.Application.Queries;
using ProductCatalog.Domain.Repository;

/// <summary>Lista los productos del catálogo paginados.</summary>
public sealed class ListProductsQueryHandler(ProductApplicationMapper productApplicationMapper, IProductRepository productRepository) : IQueryHandler<ListProductsQuery, PagedResponse<ListProductsResponseDto>>
{
    private readonly ProductApplicationMapper productApplicationMapper = productApplicationMapper;
    private readonly IProductRepository productRepository = productRepository;

    public async Task<PagedResponse<ListProductsResponseDto>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var page = await productRepository.List(query.Pageable, cancellationToken);
        return new PagedResponse<ListProductsResponseDto>
        {
            Items = [.. page.Items.Select(productApplicationMapper.ToListProductsResponseDto)],
            Page = page.Number,
            Size = page.Size,
            TotalElements = page.TotalElements,
            TotalPages = page.TotalPages,
        };
    }
}
