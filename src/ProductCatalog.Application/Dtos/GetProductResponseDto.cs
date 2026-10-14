namespace ProductCatalog.Application.Dtos;

using ProductCatalog.Domain.Enums;
using ProductCatalog.Domain.ValueObject;
using System.Text.Json.Serialization;

public sealed record GetProductResponseDto
{
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("notes")]
    public required string? Notes { get; init; }

    [JsonPropertyName("price")]
    public required Money Price { get; init; }

    [JsonPropertyName("status")]
    public required ProductStatus Status { get; init; }
}
