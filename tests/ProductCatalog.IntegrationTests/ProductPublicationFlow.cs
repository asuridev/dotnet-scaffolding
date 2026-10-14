namespace ProductCatalog.IntegrationTests;

using ProductCatalog.IntegrationTests.Support;
using Xunit;

/// <summary>FL-PRD-007 · publicación de productos.</summary>
public sealed class ProductPublicationFlow : FlowTest
{
    private static string pubId = "";
    private static string zeroId = "";

    private static async Task<string> Seed(string sku, string amount)
    {
        var response = await Post($"{RouteBase}/products",
            $$$"""{"sku":"{{{sku}}}","name":"Producto {{{sku}}}","price":{"amount":{{{amount}}},"currency":"EUR"}}""",
            new() { ["Idempotency-Key"] = Guid.NewGuid().ToString() });
        Assert.True(response.Status == 201, response.ToString());
        return response.Json().GetProperty("id").GetString()!;
    }

    protected override async Task GivenAsync()
    {
        // PUB-0001 (10.00) y PUB-0002 (0.00), ambos recién creados en draft.
        pubId = await Seed("PUB-0001", "10.00");
        zeroId = await Seed("PUB-0002", "0.00");
    }

    private static string Expected(string id, string sku, string amount, string status) =>
        $$"""{"id":"{{id}}","sku":"{{sku}}","name":"Producto {{sku}}","notes":null,"price":{"amount":{{amount}},"currency":"EUR"},"status":"{{status}}"}""";

    private static async Task AssertState(string id, string sku, string amount, string status)
    {
        var get = await Get($"{RouteBase}/products/{id}");
        Assert.True(get.Status == 200, get.ToString());
        AssertJson(Expected(id, sku, amount, status), get);
    }

    private static void AssertError(FlowResponse response, int status, string code)
    {
        Assert.True(response.Status == status, response.ToString());
        Assert.Equal(code, response.Json().GetProperty("code").GetString());
    }

    [Scenario("FL-PRD-007: un producto en draft con precio se publica")]
    public async Task Publica()
    {
        var response = await Post($"{RouteBase}/products/{pubId}/activate");
        Assert.True(response.Status == 204, response.ToString());
        Assert.Equal("", response.Body ?? "");
        await AssertState(pubId, "PUB-0001", "10.00", "active");
    }

    [Scenario("FL-PRD-007-B: publicar uno que ya está activo es 409")]
    public async Task YaActivo()
    {
        var response = await Post($"{RouteBase}/products/{pubId}/activate");
        AssertError(response, 409, "PRODUCT_NOT_DRAFT");
        await AssertState(pubId, "PUB-0001", "10.00", "active");
    }

    [Scenario("FL-PRD-007-C: un producto activo se retira")]
    public async Task ActivoSeRetira()
    {
        var response = await Post($"{RouteBase}/products/{pubId}/retire");
        Assert.True(response.Status == 204, response.ToString());
        await AssertState(pubId, "PUB-0001", "10.00", "retired");
    }

    [Scenario("FL-PRD-007-D: un producto con precio cero no se publica")]
    public async Task PrecioCero()
    {
        var response = await Post($"{RouteBase}/products/{zeroId}/activate");
        AssertError(response, 422, "PRODUCT_PRICE_NOT_POSITIVE");
        await AssertState(zeroId, "PUB-0002", "0.00", "draft");
    }

    [Scenario("FL-PRD-007-E: el estado se comprueba antes que el precio")]
    public async Task EstadoAntesQuePrecio()
    {
        var retire = await Post($"{RouteBase}/products/{zeroId}/retire");
        Assert.True(retire.Status == 204, retire.ToString());
        var response = await Post($"{RouteBase}/products/{zeroId}/activate");
        AssertError(response, 409, "PRODUCT_NOT_DRAFT");
    }

    [Scenario("FL-PRD-007-F: publicar uno que no existe es 404")]
    public async Task NoExiste()
    {
        var response = await Post($"{RouteBase}/products/0192f1d2-0000-7000-8000-000000000003/activate");
        AssertError(response, 404, "PRODUCT_NOT_FOUND");
    }
}
