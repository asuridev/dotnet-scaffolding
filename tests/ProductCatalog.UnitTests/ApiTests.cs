namespace ProductCatalog.UnitTests;

using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// El contrato HTTP del servidor arrancado en memoria: lo que fija el diseño sin depender de la lógica del agente.
/// Las peticiones salen del diseño (keel-core/gen/api-cases.js), las mismas que pide la prueba de keel-nest.
/// </summary>
public sealed class ApiTests(InMemoryServer server) : IClassFixture<InMemoryServer>
{
    /// <summary>El orden de las claves de ErrorResponse: el del cable (keel-core, WIRE_SHAPES.errorResponse).</summary>
    private static readonly string[] ErrorShape = ["timestamp", "status", "error", "code", "message", "details", "correlationId"];

    private async Task<HttpResponseMessage> Send(string method, string url, string? json = null, string? correlationId = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative));
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }
        using var client = server.CreateClient();
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    private static string Correlation(HttpResponseMessage response) => response.Headers.GetValues("X-Correlation-Id").Single();

    /// <summary>Un ErrorResponse con la forma del contrato y la correlación de la propia respuesta.</summary>
    private static async Task<JsonElement> ExpectError(HttpResponseMessage response, int status, string error, string? code, string message)
    {
        Assert.Equal(status, (int)response.StatusCode);
        var body = await Body(response);
        Assert.Equal(ErrorShape, body.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(error, body.GetProperty("error").GetString());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(message, body.GetProperty("message").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z\z", body.GetProperty("timestamp").GetString());
        Assert.Equal(Correlation(response), body.GetProperty("correlationId").GetString());
        return body;
    }

    [Fact]
    public async Task UnaRutaQueNoExisteEs404ConErrorResponse()
    {
        using var response = await Send("GET", "/api/v1/keel-ruta-que-no-existe");
        await ExpectError(response, 404, "Not Found", null, "Recurso no encontrado");
    }

    [Fact]
    public async Task LaCorrelacionRecibidaVuelveEnLaRespuestaYUnaQueNoCumpleElFormatoSeSustituye()
    {
        using var kept = await Send("GET", "/livez", correlationId: "pedido-42");
        Assert.Equal("pedido-42", Correlation(kept));
        using var replaced = await Send("GET", "/livez", correlationId: "con espacios");
        Assert.Matches("^[0-9a-f-]{36}\\z", Correlation(replaced));
        using var generated = await Send("GET", "/livez");
        Assert.Matches("^[0-9a-f-]{36}\\z", Correlation(generated));
    }

    [Fact]
    public async Task UnaOperacionLlegaASuHandlerNiElDe404NiElDe405()
    {
        using var response = await Send("GET", "/api/v1/products/0192f1d2-0000-7000-8000-000000000001");
        Assert.NotEqual(405, (int)response.StatusCode);
        if ((int)response.StatusCode >= 400)
        {
            var body = await Body(response);
            Assert.Equal(ErrorShape, body.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.NotEqual("Recurso no encontrado", body.GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task UnCuerpoQueNoEsJsonEs400PeticionMalformadaSinDetails()
    {
        using var response = await Send("POST", "/api/v1/products", json: "{\"roto\":");
        var body = await ExpectError(response, 400, "Bad Request", "VALIDATION_ERROR", "Petición malformada");
        Assert.Equal(JsonValueKind.Null, body.GetProperty("details").ValueKind);
    }

    [Fact]
    public async Task UnCuerpoQueIncumpleLoDeclaradoEs400ValidationErrorConElCampoEnDetails()
    {
        using var response = await Send("POST", "/api/v1/products", json: "{\"sku\":\"  \"}");
        var body = await ExpectError(response, 400, "Validation Error", "VALIDATION_ERROR", "La petición no supera las validaciones");
        Assert.Contains("sku must not be blank", body.GetProperty("details").EnumerateArray().Select(detail => detail.GetString()));
    }

    [Fact]
    public async Task UnUuidDeRutaMalFormadoEs400PeticionMalformada()
    {
        using var response = await Send("GET", "/api/v1/products/no-es-un-uuid", json: "{}");
        await ExpectError(response, 400, "Bad Request", "VALIDATION_ERROR", "Petición malformada");
    }

    [Fact]
    public async Task UnCaminoQueExisteConOtroMetodoEs405()
    {
        using var response = await Send("DELETE", "/api/v1/products");
        await ExpectError(response, 405, "Method Not Allowed", null, "Método HTTP no soportado");
    }
}
