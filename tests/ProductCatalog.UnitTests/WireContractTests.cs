// El contrato del cable, ejecutado. Los casos los genera keel-dotnet desde keel-core/gen/wire.js, la misma
// fuente contra la que se miden los servidores de keel-spring y keel-nest: si uno de estos falla, este
// servidor y los otros dos del mismo diseño ya no responden igual.

namespace ProductCatalog.UnitTests;

using ProductCatalog.Application.Support;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

/// <summary>Un enum como los que emite el dominio: el miembro C# y el valor del diseño, que es el que viaja.</summary>
public enum WireSampleEnum
{
    [JsonStringEnumMemberName("in-review")]
    InReview,
}

public sealed class WireContractTests
{
    private static string Write<T>(T value) => JsonSerializer.Serialize(value, WireJson.Options);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, WireJson.Options)!;

    private static string WriteJson(string literal)
    {
        using var document = JsonDocument.Parse(literal);
        return Write(document.RootElement);
    }

    private static WireSampleEnum EnumOf(string value) => value switch
    {
        "in-review" => WireSampleEnum.InReview,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "valor de enum sin caso")
    };

    /// <summary>El valor tipado a partir de su literal del diseño, escrito con el contrato.</summary>
    private static string WriteTyped(string type, string literal) => type switch
    {
        "decimal" => Write(decimal.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture)),
        "long" => Write(long.Parse(literal, CultureInfo.InvariantCulture)),
        "int" => Write(int.Parse(literal, CultureInfo.InvariantCulture)),
        "boolean" => Write(bool.Parse(literal)),
        "timestamp" => Write(DateTimeOffset.Parse(literal, CultureInfo.InvariantCulture)),
        "date" => Write(DateOnly.ParseExact(literal, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
        "uuid" => Write(Guid.Parse(literal)),
        "json" => WriteJson(literal),
        "enum" => Write(EnumOf(literal)),
        _ => Write(literal)
    };

    /// <summary>Lee un valor del cable con el tipo del diseño, como lo hará un DTO, y lo vuelve a escribir.</summary>
    private static string RoundTrip(string type, string json) => type switch
    {
        "decimal" => Write(Read<decimal>(json)),
        "long" => Write(Read<long>(json)),
        "int" => Write(Read<int>(json)),
        "boolean" => Write(Read<bool>(json)),
        "timestamp" => Write(Read<DateTimeOffset>(json)),
        "date" => Write(Read<DateOnly>(json)),
        "uuid" => Write(Read<Guid>(json)),
        "json" => Write(Read<JsonElement>(json)),
        "enum" => Write(Read<WireSampleEnum>(json)),
        _ => Write(Read<string>(json))
    };

    [Theory]
    [InlineData("decimal-keeps-scale", "decimal", "2.50", "2.50")]
    [InlineData("decimal-integer-scale", "decimal", "10", "10")]
    [InlineData("decimal-negative", "decimal", "-0.50", "-0.50")]
    [InlineData("decimal-small-plain", "decimal", "0.0000001", "0.0000001")]
    [InlineData("decimal-large-plain", "decimal", "12345678901234567890.12", "12345678901234567890.12")]
    [InlineData("long-beyond-double", "long", "9007199254740993", "9007199254740993")]
    [InlineData("long-negative", "long", "-9223372036854775808", "-9223372036854775808")]
    [InlineData("int", "int", "42", "42")]
    [InlineData("timestamp-millis", "timestamp", "2026-03-14T09:21:07.482Z", "\"2026-03-14T09:21:07.482Z\"")]
    [InlineData("timestamp-pads", "timestamp", "2026-03-14T09:21:07Z", "\"2026-03-14T09:21:07.000Z\"")]
    [InlineData("timestamp-truncates", "timestamp", "2026-03-14T09:21:07.4829Z", "\"2026-03-14T09:21:07.482Z\"")]
    [InlineData("date", "date", "2026-03-14", "\"2026-03-14\"")]
    [InlineData("uuid", "uuid", "0190f3c1-7b2e-7a4d-9c1e-3f5a6b7c8d9e", "\"0190f3c1-7b2e-7a4d-9c1e-3f5a6b7c8d9e\"")]
    [InlineData("json-embedded", "json", "{\"a\":[1,2],\"b\":\"x\"}", "{\"a\":[1,2],\"b\":\"x\"}")]
    [InlineData("enum-value", "enum", "in-review", "\"in-review\"")]
    [InlineData("boolean", "boolean", "true", "true")]
    [InlineData("string-utf8", "string", "Ñandú \"x\"", "\"Ñandú \\\"x\\\"\"")]
    public void Salida(string id, string type, string value, string json) =>
        Assert.True(json == WriteTyped(type, value), $"{id}: {type} {value} se escribe {WriteTyped(type, value)}, no {json}");

    [Theory]
    [InlineData("decimal-scale-kept", "decimal", "2.50", "2.50")]
    [InlineData("decimal-from-string", "decimal", "\"2.50\"", "2.50")]
    [InlineData("decimal-exponent", "decimal", "1E-7", "0.0000001")]
    [InlineData("long-exact", "long", "9007199254740993", "9007199254740993")]
    [InlineData("long-from-string", "long", "\"9007199254740993\"", "9007199254740993")]
    [InlineData("timestamp-offset", "timestamp", "\"2026-03-14T10:21:07.482+01:00\"", "\"2026-03-14T09:21:07.482Z\"")]
    [InlineData("timestamp-micros", "timestamp", "\"2026-03-14T09:21:07.482917Z\"", "\"2026-03-14T09:21:07.482Z\"")]
    [InlineData("date", "date", "\"2026-03-14\"", "\"2026-03-14\"")]
    [InlineData("json-object", "json", "{\"a\":1}", "{\"a\":1}")]
    [InlineData("json-as-string", "json", "\"{\\\"a\\\":1}\"", "{\"a\":1}")]
    public void EntradaIdaYVuelta(string id, string type, string json, string roundTrip) =>
        Assert.True(roundTrip == RoundTrip(type, json), $"{id}: {json} vuelve como {RoundTrip(type, json)}, no {roundTrip}");

    [Theory]
    [InlineData("decimal-not-number", "decimal", "\"dos\"")]
    [InlineData("long-not-number", "long", "\"x1\"")]
    [InlineData("timestamp-not-iso", "timestamp", "\"ayer\"")]
    [InlineData("date-impossible", "date", "\"2026-02-30\"")]
    public void EntradaRechazada(string id, string type, string json)
    {
        var error = Record.Exception(() => RoundTrip(type, json));
        Assert.True(error is JsonException, $"{id}: {type} {json} no se rechaza ({error?.GetType().Name ?? "se acepta"})");
    }

    public sealed record Sample(long A, string? B);

    [OmitNulls]
    public sealed record OmittingSample(long A, string? B, int? C);

    [Fact]
    public void PorDefectoUnCampoSinValorViajaComoNull() =>
        Assert.Equal("""{"a":1,"b":null}""", Write(new Sample(1, null)));

    [Fact]
    public void UnTipoMarcadoConOmitNullsNoLosEscribe() =>
        Assert.Equal("""{"a":1}""", Write(new OmittingSample(1, null, null)));

    [Fact]
    public void UnaPropiedadDesconocidaSeIgnora() =>
        Assert.Equal(new Sample(1, "x"), Read<Sample>("""{"a":1,"b":"x","otra":true}"""));

    [Fact]
    public void UnEnumNoSeLeePorSuNumero() =>
        Assert.Throws<JsonException>(() => Read<WireSampleEnum>("0"));

    [Theory]
    [InlineData("1E-40")]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("123456789012345678901234567890")]
    public void UnDecimalQueNoCabeExactoSeRechazaEnVezDeRedondearse(string json) =>
        Assert.Throws<JsonException>(() => Read<decimal>(json));

    [Theory]
    [InlineData("1.0000000000000000000000000001", "1.0000000000000000000000000001")]
    [InlineData("2.50E1", "25.0")]
    [InlineData("1E+3", "1000")]
    public void UnDecimalQueCabeSeLeeConLaEscalaQueEscribio(string json, string expected) =>
        Assert.Equal(expected, Write(Read<decimal>(json)));

    [Fact]
    public void UnTimestampSinZonaSeRechaza() =>
        Assert.Throws<JsonException>(() => Read<DateTimeOffset>("\"2026-03-14T09:21:07.482\""));
}
