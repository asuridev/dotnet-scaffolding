namespace ProductCatalog.IntegrationTests;

using ProductCatalog.IntegrationTests.Support;
using Xunit;

/// <summary>FL-PRD-006 · retirada de productos.</summary>
public sealed class ProductRetirementFlow : FlowTest
{
    private static string productId = "";

    protected override async Task GivenAsync()
    {
        // RET-0001 recién creado, en draft.
        var response = await Post($"{RouteBase}/products",
            """{"sku":"RET-0001","name":"Producto a retirar","price":{"amount":5.00,"currency":"EUR"}}""",
            new() { ["Idempotency-Key"] = Guid.NewGuid().ToString() });
        Assert.True(response.Status == 201, response.ToString());
        productId = response.Json().GetProperty("id").GetString()!;
    }

    private static string Expected(string status) =>
        $$"""{"id":"{{productId}}","sku":"RET-0001","name":"Producto a retirar","notes":null,"price":{"amount":5.00,"currency":"EUR"},"status":"{{status}}"}""";

    [Scenario("FL-PRD-006: un producto en draft se retira")]
    public async Task Retira()
    {
        var response = await Post($"{RouteBase}/products/{productId}/retire");
        Assert.True(response.Status == 204, response.ToString());
        Assert.Equal("", response.Body ?? "");
        var get = await Get($"{RouteBase}/products/{productId}");
        Assert.True(get.Status == 200, get.ToString());
        AssertJson(Expected("retired"), get);
    }

    [Scenario("FL-PRD-006-B: retirar uno ya retirado es 409")]
    public async Task YaRetirado()
    {
        var response = await Post($"{RouteBase}/products/{productId}/retire");
        Assert.True(response.Status == 409, response.ToString());
        Assert.Equal("PRODUCT_ALREADY_RETIRED", response.Json().GetProperty("code").GetString());
        var get = await Get($"{RouteBase}/products/{productId}");
        Assert.True(get.Status == 200, get.ToString());
        AssertJson(Expected("retired"), get);
    }

    [Scenario("FL-PRD-006-C: retirar uno que no existe es 404")]
    public async Task NoExiste()
    {
        var response = await Post($"{RouteBase}/products/0192f1d2-0000-7000-8000-000000000002/retire");
        Assert.True(response.Status == 404, response.ToString());
        Assert.Equal("PRODUCT_NOT_FOUND", response.Json().GetProperty("code").GetString());
    }
}
