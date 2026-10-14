namespace ProductCatalog.IntegrationTests;

using ProductCatalog.IntegrationTests.Support;
using Xunit;

/// <summary>FL-PRD-001 · alta de productos.</summary>
public sealed class ProductCreationFlow : FlowTest
{
    private static string productId = "";

    private static Dictionary<string, string> NewKey() => new() { ["Idempotency-Key"] = Guid.NewGuid().ToString() };

    private static void AssertError(FlowResponse response, int status, string code)
    {
        Assert.True(response.Status == status, response.ToString());
        Assert.Equal(code, response.Json().GetProperty("code").GetString());
    }

    [Scenario("FL-PRD-001: se da de alta un producto")]
    public async Task AltaCorrecta()
    {
        var response = await Post($"{RouteBase}/products",
            """{"sku":"ACM-0001","name":"Martillo de carpintero","price":{"amount":12.50,"currency":"EUR"},"apiToken":"tok-123"}""",
            NewKey());
        Assert.True(response.Status == 201, response.ToString());
        productId = response.Json().GetProperty("id").GetString()!;
        Assert.EndsWith($"/api/v1/products/{productId}", response.Header("location"));
        AssertJson("""{"id":"<uuid>","sku":"ACM-0001","name":"Martillo de carpintero","notes":null,"price":{"amount":12.50,"currency":"EUR"},"status":"draft"}""", response);
    }

    [Scenario("FL-PRD-001-B: el producto creado se puede leer")]
    public async Task SeLee()
    {
        var response = await Get($"{RouteBase}/products/{productId}");
        Assert.True(response.Status == 200, response.ToString());
        AssertJson($$"""{"id":"{{productId}}","sku":"ACM-0001","name":"Martillo de carpintero","notes":null,"price":{"amount":12.50,"currency":"EUR"},"status":"draft"}""", response);
    }

    [Scenario("FL-PRD-001-C: un sku que ya existe se rechaza")]
    public async Task SkuRepetido()
    {
        var response = await Post($"{RouteBase}/products",
            """{"sku":"ACM-0001","name":"Otro martillo","price":{"amount":9.99,"currency":"EUR"}}""", NewKey());
        AssertError(response, 409, "SKU_ALREADY_EXISTS");

        var list = await Get($"{RouteBase}/products?size=20");
        Assert.True(list.Status == 200, list.ToString());
        Assert.Equal("1", list.Json().GetProperty("totalElements").GetRawText());
        var items = list.Json().GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("ACM-0001", items[0].GetProperty("sku").GetString());
    }

    [Scenario("FL-PRD-001-D: el importe se redondea a la escala del tipo")]
    public async Task Redondeo()
    {
        var response = await Post($"{RouteBase}/products",
            """{"sku":"ACM-0002","name":"Destornillador","notes":"Punta plana","price":{"amount":12.345,"currency":"EUR"}}""", NewKey());
        Assert.True(response.Status == 201, response.ToString());
        AssertJson("""{"id":"<uuid>","sku":"ACM-0002","name":"Destornillador","notes":"Punta plana","price":{"amount":12.35,"currency":"EUR"},"status":"draft"}""", response);
    }

    [Scenario("FL-PRD-001-E: la entrada que incumple el contrato es 400")]
    public async Task EntradaInvalida()
    {
        var longName = new string('a', 121);
        var bodies = new[]
        {
            """{"sku":"acm-0003","name":"X","price":{"amount":1.00,"currency":"EUR"}}""",
            """{"sku":"ACME-00031","name":"X","price":{"amount":1.00,"currency":"EUR"}}""",
            """{"sku":"ACM-0003","name":"X","price":{"amount":-1.00,"currency":"EUR"}}""",
            """{"sku":"ACM-0003","name":"X","price":{"amount":1.00,"currency":"EURO"}}""",
            "{\"sku\":\"ACM-0003\",\"name\":\"" + longName + "\",\"price\":{\"amount\":1.00,\"currency\":\"EUR\"}}",
            """{"name":"X","price":{"amount":1.00,"currency":"EUR"}}""",
            """{"sku":"ACM-0003","name":"   ","price":{"amount":1.00,"currency":"EUR"}}""",
        };
        foreach (var body in bodies)
        {
            var response = await Post($"{RouteBase}/products", body, NewKey());
            AssertError(response, 400, "VALIDATION_ERROR");
        }

        var list = await Get($"{RouteBase}/products?size=20");
        Assert.True(list.Status == 200, list.ToString());
        Assert.Equal("2", list.Json().GetProperty("totalElements").GetRawText());
        var skus = list.Json().GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { "ACM-0001", "ACM-0002" }, skus);
    }
}
