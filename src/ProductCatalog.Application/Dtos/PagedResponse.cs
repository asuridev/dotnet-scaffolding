namespace ProductCatalog.Application.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Respuesta paginada del contrato: los elementos y los metadatos de la página. La forma y el orden de las
/// claves son los del cable (docs/dsl/api.md § Paginación): los escenarios se escriben contra estos nombres.
/// </summary>
public sealed record PagedResponse<T>
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<T> Items { get; init; }

    [JsonPropertyName("page")]
    public required int Page { get; init; }

    [JsonPropertyName("size")]
    public required int Size { get; init; }

    [JsonPropertyName("totalElements")]
    public required long TotalElements { get; init; }

    [JsonPropertyName("totalPages")]
    public required int TotalPages { get; init; }
}
