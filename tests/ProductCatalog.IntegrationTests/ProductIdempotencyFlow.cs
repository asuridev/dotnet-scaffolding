namespace ProductCatalog.IntegrationTests;

using ProductCatalog.IntegrationTests.Support;
using Xunit;

/// <summary>FL-PRD-002 y FL-PRD-003 · idempotencia del alta.</summary>
public sealed class ProductIdempotencyFlow : FlowTest
{
    private static readonly string Key2 = Guid.NewGuid().ToString();
    private const string Body2 = """{"sku":"BRD-0001","name":"Broca de 8 mm","price":{"amount":3.20,"currency":"EUR"}}""";
    private const string Body3 = """{"sku":"CLV-0001","name":"Clavo de 40 mm","price":{"amount":0.05,"currency":"EUR"}}""";
    private static string firstId = "";
    private static string firstLocation = "";

    [Scenario("FL-PRD-002: el cliente reintenta el alta con la misma clave")]
    public async Task PrimerAlta()
    {
        var response = await Post($"{RouteBase}/products", Body2, new() { ["Idempotency-Key"] = Key2 });
        Assert.True(response.Status == 201, response.ToString());
        AssertJson("""{"id":"<uuid>","sku":"BRD-0001","name":"Broca de 8 mm","notes":null,"price":{"amount":3.20,"currency":"EUR"},"status":"draft"}""", response);
        firstId = response.Json().GetProperty("id").GetString()!;
        firstLocation = response.Header("location")!;
        Assert.EndsWith($"/api/v1/products/{firstId}", firstLocation);
    }

    [Scenario("FL-PRD-002-B: la misma petición con la misma clave reproduce la respuesta")]
    public async Task Reproduce()
    {
        var response = await Post($"{RouteBase}/products", Body2, new() { ["Idempotency-Key"] = Key2 });
        Assert.True(response.Status == 201, response.ToString());
        AssertJson($$"""{"id":"{{firstId}}","sku":"BRD-0001","name":"Broca de 8 mm","notes":null,"price":{"amount":3.20,"currency":"EUR"},"status":"draft"}""", response);
        Assert.Equal(firstLocation, response.Header("location"));

        var list = await Get($"{RouteBase}/products?size=20");
        Assert.True(list.Status == 200, list.ToString());
        Assert.Equal("1", list.Json().GetProperty("totalElements").GetRawText());
    }

    [Scenario("FL-PRD-002-C: el mismo contenido con otra clave es un alta nueva, y choca")]
    public async Task OtraClave()
    {
        var response = await Post($"{RouteBase}/products", Body2, new() { ["Idempotency-Key"] = Guid.NewGuid().ToString() });
        Assert.True(response.Status == 409, response.ToString());
        Assert.Equal("SKU_ALREADY_EXISTS", response.Json().GetProperty("code").GetString());
    }

    [Scenario("FL-PRD-003: dos altas con la misma clave, a la vez")]
    public async Task Concurrentes()
    {
        var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(
            Post($"{RouteBase}/products", Body3, new() { ["Idempotency-Key"] = key }),
            Post($"{RouteBase}/products", Body3, new() { ["Idempotency-Key"] = key }));

        var created = new List<string>();
        foreach (var r in responses)
        {
            Assert.True(r.Status == 201 || r.Status == 409, r.ToString());
            if (r.Status == 201)
            {
                AssertJson("""{"id":"<uuid>","sku":"CLV-0001","name":"Clavo de 40 mm","notes":null,"price":{"amount":0.05,"currency":"EUR"},"status":"draft"}""", r);
                created.Add(r.Json().GetProperty("id").GetString()!);
            }
            else
            {
                var code = r.Json().GetProperty("code").GetString();
                Assert.True(code is "IDEMPOTENCY_KEY_IN_PROGRESS" or "SKU_ALREADY_EXISTS", r.ToString());
            }
        }
        Assert.NotEmpty(created);
        Assert.Single(created.Distinct());

        var list = await Get($"{RouteBase}/products?size=20");
        Assert.True(list.Status == 200, list.ToString());
        var clv = list.Json().GetProperty("items").EnumerateArray().Count(i => i.GetProperty("sku").GetString() == "CLV-0001");
        Assert.Equal(1, clv);
    }
}
