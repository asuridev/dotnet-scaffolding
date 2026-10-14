namespace ProductCatalog.Domain.Enums;

using System.Text.Json.Serialization;

/// <summary>Estados de publicación de un producto.</summary>
public enum ProductStatus
{
    [JsonStringEnumMemberName("draft")]
    Draft,
    [JsonStringEnumMemberName("active")]
    Active,
    [JsonStringEnumMemberName("retired")]
    Retired,
}
