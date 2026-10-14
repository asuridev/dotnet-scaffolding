namespace ProductCatalog.Application.Queries;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Dtos;
using ProductCatalog.Domain.Repository;

/// <summary>Lista los productos del catálogo paginados.</summary>
public sealed class ListProductsQuery : IQuery<PagedResponse<ListProductsResponseDto>>
{
    public required Pageable Pageable { get; init; }
}
