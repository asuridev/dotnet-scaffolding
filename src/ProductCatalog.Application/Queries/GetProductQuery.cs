namespace ProductCatalog.Application.Queries;

using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Dtos;

/// <summary>Recupera un producto por su identificador.</summary>
public sealed class GetProductQuery : IQuery<GetProductResponseDto>
{
    public required Guid Id { get; init; }
}
