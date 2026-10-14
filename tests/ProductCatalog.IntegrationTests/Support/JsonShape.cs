namespace ProductCatalog.IntegrationTests.Support;

using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// La comparación estricta de un cuerpo JSON con el esperado, el <c>toStrictEqual</c> de keel-nest: las mismas
/// claves (ni una de más ni de menos), los números por su TEXTO (la escala de un decimal es contrato observable), las
/// listas en orden, y tres comodines de forma para lo que genera el servidor.
/// </summary>
internal static partial class JsonShape
{
    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z")]
    private static partial Regex Uuid();

    // ISO-8601 en UTC con exactamente tres decimales y Z: el instante del contrato del cable.
    [GeneratedRegex("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z\\z")]
    private static partial Regex Instant();

    public static void AssertMatches(string expected, JsonElement actual)
    {
        using var document = JsonDocument.Parse(expected);
        var problems = new List<string>();
        Compare(document.RootElement, actual, "$", problems);
        if (problems.Count > 0) Assert.Fail($"El JSON no casa con el esperado:\n  {string.Join("\n  ", problems)}\nrecibido: {actual.GetRawText()}");
    }

    private static void Compare(JsonElement expected, JsonElement actual, string path, List<string> problems)
    {
        if (expected.ValueKind == JsonValueKind.String && expected.GetString() is "<uuid>" or "<instant>" or "<any>")
        {
            var shape = expected.GetString();
            if (shape == "<any>") return;
            var ok = actual.ValueKind == JsonValueKind.String && (shape == "<uuid>" ? Uuid() : Instant()).IsMatch(actual.GetString()!);
            if (!ok) problems.Add($"{path}: se esperaba la forma {shape} y llegó {actual.GetRawText()}");
            return;
        }
        if (expected.ValueKind != actual.ValueKind)
        {
            problems.Add($"{path}: se esperaba {expected.GetRawText()} y llegó {actual.GetRawText()}");
            return;
        }
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
                foreach (var property in actual.EnumerateObject())
                {
                    if (!expectedNames.Contains(property.Name)) problems.Add($"{path}.{property.Name}: sobra (llegó {property.Value.GetRawText()})");
                }
                foreach (var property in expected.EnumerateObject())
                {
                    if (actual.TryGetProperty(property.Name, out var value)) Compare(property.Value, value, $"{path}.{property.Name}", problems);
                    else problems.Add($"{path}.{property.Name}: falta");
                }
                break;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                if (expectedItems.Count != actualItems.Count) problems.Add($"{path}: se esperaban {expectedItems.Count} elemento(s) y llegaron {actualItems.Count}");
                for (var i = 0; i < Math.Min(expectedItems.Count, actualItems.Count); i++) Compare(expectedItems[i], actualItems[i], $"{path}[{i}]", problems);
                break;
            default:
                // Números por su texto exacto (2.50 no es 2.5); cadenas, booleanos y null, por su valor.
                if (expected.GetRawText() != actual.GetRawText()) problems.Add($"{path}: se esperaba {expected.GetRawText()} y llegó {actual.GetRawText()}");
                break;
        }
    }
}
