namespace ProductCatalog.Api.Rest;

using ProductCatalog.Domain.ValueObject;
using System.Text.Json;

/// <summary>Los value objects del diseño leídos de un cuerpo JSON, con las guardas de su constructor.</summary>
public static class ValueReaders
{
    /// <summary>Money desde el cuerpo JSON: si su constructor lo rechaza, la petición es malformada.</summary>
    public static Money? ReadMoney(JsonElement? value) =>
        JsonBodyReader.ValueObject(value, fields => new Money(
            JsonBodyReader.PresentValue(JsonBodyReader.Value<decimal?>(fields, "amount")),
            JsonBodyReader.Present(JsonBodyReader.Value<string?>(fields, "currency"))));
}
