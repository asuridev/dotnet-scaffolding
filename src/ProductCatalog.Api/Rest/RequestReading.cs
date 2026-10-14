namespace ProductCatalog.Api.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using ProductCatalog.Application.Support;
using ProductCatalog.Domain.Errors;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// Lectura de una petición: convertir lo que llega por el cable al tipo del diseño. Lo usan los lectores que
// genera build para cada operación (Endpoints/). Un valor ausente es null; lo que no se puede convertir es una
// petición malformada.

/// <summary>
/// Lectores de un cuerpo JSON. Los valores se convierten con el contrato del cable (WireJson): un decimal exacto,
/// un instante con zona, un uuid en su forma canónica de 36 caracteres.
/// </summary>
public static class JsonBodyReader
{
    /// <summary>El cuerpo como objeto JSON. Sin cuerpo (o con <c>null</c>): null si es opcional, malformada si es obligatorio.</summary>
    public static async Task<JsonElement?> ReadObject(HttpRequest request, bool required)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);
        JsonElement root;
        if (buffer.Length == 0)
        {
            root = default;
        }
        else
        {
            try
            {
                using var document = JsonDocument.Parse(buffer.ToArray());
                root = document.RootElement.Clone();
            }
            catch (JsonException error)
            {
                throw new MalformedRequestException(error);
            }
        }
        if (root.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            if (required)
            {
                throw new MalformedRequestException();
            }
            return null;
        }
        return root.ValueKind == JsonValueKind.Object ? root : throw new MalformedRequestException();
    }

    /// <summary>¿Viene la propiedad, aunque sea con null? Es lo que distingue «no tocar» de «vaciar» en un PATCH.</summary>
    public static bool Has(JsonElement? fields, string name) => fields is { } value && value.TryGetProperty(name, out _);

    /// <summary>La propiedad del objeto, o null si falta o vale null.</summary>
    public static JsonElement? Property(JsonElement? fields, string name) =>
        fields is { } value && value.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property : null;

    /// <summary>Un valor del objeto, convertido con el contrato del cable. Para un tipo de valor, pásalo nullable (<c>decimal?</c>).</summary>
    public static T? Value<T>(JsonElement? fields, string name) => Property(fields, name) is { } property ? Element<T>(property) : default;

    /// <summary>Un valor JSON presente, convertido con el contrato del cable: un null aquí es una petición malformada.</summary>
    public static T Element<T>(JsonElement element)
    {
        T? value;
        try
        {
            value = element.Deserialize<T>(WireJson.Options);
        }
        catch (Exception error) when (error is JsonException or NotSupportedException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new MalformedRequestException(error);
        }
        return value is null ? throw new MalformedRequestException() : value;
    }

    /// <summary>Un enum por el LITERAL del diseño, exacto: es lo que escribe el cable.</summary>
    public static T? EnumValue<T>(JsonElement? fields, string name)
        where T : struct, Enum => Property(fields, name) is { } property ? EnumElement<T>(property) : null;

    public static T EnumElement<T>(JsonElement element)
        where T : struct, Enum =>
        element.ValueKind == JsonValueKind.String && EnumLiterals<T>.ByLiteral.TryGetValue(element.GetString()!, out var value)
            ? value
            : throw new MalformedRequestException();

    /// <summary>Una lista: ausente o null es null; algo que no es un array, malformada.</summary>
    public static IReadOnlyList<T>? List<T>(JsonElement? fields, string name, Func<JsonElement, T> element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (Property(fields, name) is not { } property)
        {
            return null;
        }
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new MalformedRequestException();
        }
        return [.. property.EnumerateArray().Select(element)];
    }

    /// <summary>
    /// Un value object: un objeto JSON. Si su constructor lo rechaza, la petición es malformada: en keel-spring eso
    /// pasa dentro de Jackson y sale igual.
    /// </summary>
    public static T? ValueObject<T>(JsonElement? value, Func<JsonElement, T> build)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(build);
        if (value is not { } element)
        {
            return null;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new MalformedRequestException();
        }
        try
        {
            return build(element);
        }
        catch (DomainException error)
        {
            throw new MalformedRequestException(error);
        }
    }

    /// <summary>Lo que el diseño exige y no llegó: una petición que no se puede leer.</summary>
    public static T Present<T>(T? value)
        where T : class => value ?? throw new MalformedRequestException();

    /// <summary>Lo que el diseño exige y no llegó, para un tipo de valor.</summary>
    public static T PresentValue<T>(T? value)
        where T : struct => value ?? throw new MalformedRequestException();
}

/// <summary>
/// Lectores de un valor de TEXTO (ruta y query), con las conversiones de Spring: un booleano admite true/false,
/// yes/no, on/off y 1/0; un enum, su literal o el nombre de la constante sin distinguir mayúsculas ni guiones; un
/// uuid, lo que admite UUID.fromString de Java. Vacío es «sin valor».
/// </summary>
public static partial class TextValues
{
    [GeneratedRegex(@"^[+-]?[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Integer();

    [GeneratedRegex(@"^[0-9a-fA-F]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Hex();

    /// <summary>El valor de un segmento de la ruta.</summary>
    public static StringValues Route(HttpRequest request, string name)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new StringValues(request.RouteValues[name] as string);
    }

    /// <summary>Un parámetro obligatorio de la query tiene que venir; vacío cuenta como presente (lo juzga su regla).</summary>
    public static void Require(HttpRequest request, string name)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Query.ContainsKey(name))
        {
            throw new MissingParameterException(name);
        }
    }

    /// <summary>Un parámetro repetido donde se espera uno solo: Spring toma el primero.</summary>
    private static string? First(StringValues values) => values.Count == 0 ? null : values[0];

    private static string? NonBlank(StringValues values) => First(values) is { } raw && !string.IsNullOrWhiteSpace(raw) ? raw : null;

    public static string? String(StringValues values) => First(values);

    public static int? Int(StringValues values)
    {
        if (NonBlank(values) is not { } raw)
        {
            return null;
        }
        return Integer().IsMatch(raw) && int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new MalformedRequestException();
    }

    public static long? Long(StringValues values)
    {
        if (NonBlank(values) is not { } raw)
        {
            return null;
        }
        return Integer().IsMatch(raw) && long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new MalformedRequestException();
    }

    public static decimal? Decimal(StringValues values) => NonBlank(values) is { } raw ? Wire(() => DecimalWireConverter.Parse(raw)) : null;

    public static DateTimeOffset? Timestamp(StringValues values) => NonBlank(values) is { } raw ? Wire(() => TimestampWireConverter.Parse(raw)) : null;

    public static DateOnly? Date(StringValues values) => NonBlank(values) is { } raw ? Wire(() => DateWireConverter.Parse(raw)) : null;

    public static JsonElement? Json(StringValues values) =>
        NonBlank(values) is { } raw
            ? Wire(() =>
            {
                using var document = JsonDocument.Parse(raw);
                return document.RootElement.Clone();
            })
            : null;

    public static bool? Bool(StringValues values) =>
        NonBlank(values)?.ToLowerInvariant() switch
        {
            null => null,
            "true" or "on" or "yes" or "1" => true,
            "false" or "off" or "no" or "0" => false,
            _ => throw new MalformedRequestException()
        };

    /// <summary>UUID.fromString de Java: cinco grupos hexadecimales que caben en 8-4-4-4-12; sale canónico.</summary>
    public static Guid? Uuid(StringValues values)
    {
        if (NonBlank(values) is not { } raw)
        {
            return null;
        }
        var parts = raw.Split('-');
        int[] widths = [8, 4, 4, 4, 12];
        if (raw.Length > 36 || parts.Length != 5)
        {
            throw new MalformedRequestException();
        }
        for (var i = 0; i < parts.Length; i++)
        {
            if (!Hex().IsMatch(parts[i]) || parts[i].Length > widths[i])
            {
                throw new MalformedRequestException();
            }
            parts[i] = parts[i].PadLeft(widths[i], '0');
        }
        return Guid.ParseExact(string.Join('-', parts), "D");
    }

    /// <summary>
    /// Un enum por su literal, o por el nombre de la constante comparado como lo hace Spring Boot (sin distinguir
    /// mayúsculas e ignorando lo que no es letra ni dígito: <c>IN_PROGRESS</c>, <c>in-progress</c>).
    /// </summary>
    public static T? EnumValue<T>(StringValues values)
        where T : struct, Enum
    {
        var raw = NonBlank(values)?.Trim();
        if (raw is null)
        {
            return null;
        }
        if (EnumLiterals<T>.ByLiteral.TryGetValue(raw, out var exact))
        {
            return exact;
        }
        return EnumLiterals<T>.ByCanonicalName.TryGetValue(EnumLiterals<T>.Canonical(raw), out var lenient) ? lenient : throw new MalformedRequestException();
    }

    /// <summary>Un value object no se lee de un texto (ruta o query): si llega, la petición es malformada.</summary>
    public static T? Unreadable<T>(StringValues values)
        where T : class => First(values) is null ? null : throw new MalformedRequestException();

    /// <summary>Una lista en la query: el parámetro repetido o separado por comas.</summary>
    public static IReadOnlyList<T>? List<T>(StringValues values, Func<string, T> element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (values.Count == 0)
        {
            return null;
        }
        IEnumerable<string> items = values.Count > 1 ? values.Select(value => value ?? "") : (values[0] ?? "").Split(',');
        return [.. items.Select(element)];
    }

    private static T Wire<T>(Func<T> convert)
    {
        try
        {
            return convert();
        }
        catch (Exception error) when (error is JsonException or FormatException or OverflowException)
        {
            throw new MalformedRequestException(error);
        }
    }
}

/// <summary>Los literales del diseño de un enum (los de [JsonStringEnumMemberName]) y sus nombres, una vez por tipo.</summary>
internal static class EnumLiterals<T>
    where T : struct, Enum
{
    public static readonly IReadOnlyDictionary<string, T> ByLiteral = Build(literal: true);

    public static readonly IReadOnlyDictionary<string, T> ByCanonicalName = Build(literal: false);

    public static string Canonical(string name) => new([.. name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    private static Dictionary<string, T> Build(bool literal)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (T)field.GetValue(null)!;
            var name = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name;
            map[literal ? name : Canonical(field.Name)] = value;
        }
        return map;
    }
}
