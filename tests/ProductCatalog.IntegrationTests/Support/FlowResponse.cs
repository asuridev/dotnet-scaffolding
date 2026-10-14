namespace ProductCatalog.IntegrationTests.Support;

using System.Text.Json;

/// <summary>Intercambio HTTP completo: lo que se afirma y lo que se vuelca al fallar.</summary>
public sealed class FlowResponse(int status, IReadOnlyDictionary<string, string> headers, string body)
{
    public int Status { get; } = status;

    /// <summary>Cabeceras de la respuesta (las del contenido incluidas), con el nombre en minúsculas.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; } = headers;

    public string Body { get; } = body;

    /// <summary>Una cabecera por nombre, sin distinguir mayúsculas; null si no vino.</summary>
    public string? Header(string name) => Headers.TryGetValue(name.ToLowerInvariant(), out var value) ? value : null;

    /// <summary>
    /// El cuerpo leído como JSON. Lanza nombrando el cuerpo si no lo es. Un número conserva su texto: para afirmar la
    /// escala de un decimal, <c>GetRawText()</c> (<c>"2.50"</c>), nunca <c>GetDecimal()</c>.
    /// </summary>
    public JsonElement Json()
    {
        try
        {
            using var document = JsonDocument.Parse(Body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"La respuesta no es JSON (status {Status}): {(Body.Length > 400 ? Body[..400] : Body)}");
        }
    }

    public override string ToString() => $"{Status} {Body}";
}
