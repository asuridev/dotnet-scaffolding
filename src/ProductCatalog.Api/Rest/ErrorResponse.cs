namespace ProductCatalog.Api.Rest;

using ProductCatalog.Infrastructure.Correlation;
using System.Text.Json.Serialization;

/// <summary>
/// Contrato de error de la API: el cuerpo uniforme de todo fallo, con la forma y el orden del cable (keel-core,
/// WIRE_SHAPES.errorResponse).
/// </summary>
/// <remarks>
/// Los campos sin valor viajan como null (details fuera de una validación, code en un fallo sin código): no se
/// omiten, porque la forma del cuerpo de error es contrato estable. Por eso este tipo no lleva [OmitNulls].
/// </remarks>
public sealed record ErrorResponse
{
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("status")]
    public required int Status { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("code")]
    public required string? Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("details")]
    public required IReadOnlyList<string>? Details { get; init; }

    [JsonPropertyName("correlationId")]
    public required string? CorrelationId { get; init; }

    public static ErrorResponse Of(int status, string error, string? code, string message, IReadOnlyList<string>? details = null) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Status = status,
            Error = error,
            Code = code,
            Message = message,
            Details = details,
            CorrelationId = CorrelationContext.Get()
        };
}
