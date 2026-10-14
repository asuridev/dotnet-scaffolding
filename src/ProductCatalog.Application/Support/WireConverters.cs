namespace ProductCatalog.Application.Support;

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

/// <summary>
/// decimal-scale, decimal-plain y decimal-input: un <c>decimal</c> sale con la escala que lleva y en notación
/// plana (lo que hace <c>decimal.ToString</c>), y se lee EXACTO del texto del número, también desde una cadena
/// numérica o en exponencial (<c>1E-7</c> se lee 0.0000001, con su escala).
/// <para>
/// El <c>decimal</c> de .NET tiene 28-29 dígitos significativos. Por encima del máximo, <c>decimal.Parse</c>
/// lanza; pero con más de 28 decimales REDONDEA en silencio (<c>1E-40</c> se lee 0). Leer exacto exige
/// rechazarlo: un 400 que se ve, en vez de un valor distinto del que mandó el cliente.
/// </para>
/// </summary>
public sealed class DecimalWireConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType switch
        {
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan),
            JsonTokenType.String => reader.GetString()!,
            _ => throw new JsonException("se esperaba un decimal")
        };
        return Parse(text);
    }

    /// <summary>El decimal EXACTO de un texto (también de la ruta o de la query); lanza JsonException si no lo es.</summary>
    public static decimal Parse(string text)
    {
        decimal value;
        int scale;
        try
        {
            value = decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            scale = WrittenScale(text.Trim());
        }
        catch (Exception error) when (error is FormatException or OverflowException)
        {
            throw new JsonException("se esperaba un decimal", error);
        }
        if (value.Scale != scale)
        {
            throw new JsonException("el decimal no cabe exacto (más de 28 decimales)");
        }
        return value;
    }

    /// <summary>La escala que pide el literal: decimales de la mantisa menos el exponente, nunca negativa.</summary>
    private static int WrittenScale(string text)
    {
        var exponentAt = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentAt < 0 ? text : text[..exponentAt];
        var exponent = exponentAt < 0 ? 0 : int.Parse(text[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        return (int)Math.Max(0L, (dot < 0 ? 0L : mantissa.Length - dot - 1) - (long)exponent);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString(CultureInfo.InvariantCulture), skipInputValidation: true);
}

/// <summary>
/// timestamp-utc-millis y timestamp-input: un instante sale en UTC con exactamente tres decimales y <c>Z</c>,
/// truncando; se lee de ISO-8601 CON zona (<c>Z</c> o <c>±hh:mm</c>), se normaliza a UTC y se descarta lo que
/// hay por debajo del milisegundo.
/// </summary>
public sealed partial class TimestampWireConverter : JsonConverter<DateTimeOffset>
{
    private const string WireFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})(?::(\d{2})(?:\.(\d+))?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Iso8601WithZone();

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("se esperaba un instante ISO-8601 con zona");
        }
        return Parse(reader.GetString()!);
    }

    /// <summary>El instante de un texto ISO-8601 con zona (también de la ruta o de la query); lanza JsonException si no lo es.</summary>
    public static DateTimeOffset Parse(string text)
    {
        var match = Iso8601WithZone().Match(text);
        if (!match.Success)
        {
            throw new JsonException("se esperaba un instante ISO-8601 con zona");
        }
        var seconds = match.Groups[3].Success ? match.Groups[3].Value : "00";
        var millis = (match.Groups[4].Value + "000")[..3];
        var normalized = $"{match.Groups[1].Value}T{match.Groups[2].Value}:{seconds}.{millis}{match.Groups[5].Value}";
        if (!DateTimeOffset.TryParseExact(normalized, "yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new JsonException("el instante no existe");
        }
        return value.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.UtcDateTime.ToString(WireFormat, CultureInfo.InvariantCulture));
}

/// <summary>date-iso: una fecha viaja como <c>YYYY-MM-DD</c>, y una que no existe (<c>2026-02-30</c>) se rechaza.</summary>
public sealed class DateWireConverter : JsonConverter<DateOnly>
{
    private const string WireFormat = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("se esperaba una fecha YYYY-MM-DD que exista");
        }
        return Parse(reader.GetString()!);
    }

    /// <summary>La fecha de un texto <c>YYYY-MM-DD</c> (también de la ruta o de la query); lanza JsonException si no lo es.</summary>
    public static DateOnly Parse(string text) =>
        DateOnly.TryParseExact(text, WireFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new JsonException("se esperaba una fecha YYYY-MM-DD que exista");

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(WireFormat, CultureInfo.InvariantCulture));
}

/// <summary>
/// json-embedded: un campo <c>json</c> viaja EMBEBIDO como valor JSON, con sus números tal como llegaron; al
/// leer se acepta también una cadena con el documento ya serializado.
/// </summary>
public sealed class JsonWireConverter : JsonConverter<JsonElement>
{
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return JsonElement.ParseValue(ref reader);
        }
        try
        {
            using var document = JsonDocument.Parse(reader.GetString()!);
            return document.RootElement.Clone();
        }
        catch (JsonException error)
        {
            throw new JsonException("la cadena no contiene un documento JSON", error);
        }
    }

    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            writer.WriteNullValue();
            return;
        }
        value.WriteTo(writer);
    }
}
