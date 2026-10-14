namespace ProductCatalog.IntegrationTests;

using System.Text.Json;
using ProductCatalog.IntegrationTests.Support;
using Xunit;

/// <summary>FL-PRD-005 · el catálogo se lista por páginas.</summary>
public sealed class ProductPaginationFlow : FlowTest
{
    private static readonly string[] Skus = ["PAG-0001", "PAG-0002", "PAG-0003"];
    private static readonly List<string> Seen = new();

    protected override async Task GivenAsync()
    {
        foreach (var sku in Skus)
        {
            var response = await Post($"{RouteBase}/products",
                $$$"""{"sku":"{{{sku}}}","name":"Producto {{{sku}}}","price":{"amount":1.00,"currency":"EUR"}}""",
                new() { ["Idempotency-Key"] = Guid.NewGuid().ToString() });
            Assert.True(response.Status == 201, response.ToString());
        }
    }

    private static void AssertItems(JsonElement body, int expectedCount)
    {
        var items = body.GetProperty("items");
        Assert.Equal(expectedCount, items.GetArrayLength());
        foreach (var item in items.EnumerateArray())
        {
            var sku = item.GetProperty("sku").GetString()!;
            Assert.Contains(sku, Skus);
            JsonShape.AssertMatches(
                $$"""{"id":"<uuid>","sku":"{{sku}}","name":"Producto {{sku}}","notes":null,"price":{"amount":1.00,"currency":"EUR"},"status":"draft"}""",
                item);
        }
    }

    private static void AssertMeta(JsonElement body, int page, int size, int total, int pages)
    {
        Assert.Equal(page.ToString(), body.GetProperty("page").GetRawText());
        Assert.Equal(size.ToString(), body.GetProperty("size").GetRawText());
        Assert.Equal(total.ToString(), body.GetProperty("totalElements").GetRawText());
        Assert.Equal(pages.ToString(), body.GetProperty("totalPages").GetRawText());
    }

    [Scenario("FL-PRD-005: el catálogo se lista por páginas")]
    public async Task PrimeraPagina()
    {
        var response = await Get($"{RouteBase}/products?page=0&size=2");
        Assert.True(response.Status == 200, response.ToString());
        var body = response.Json();
        AssertMeta(body, 0, 2, 3, 2);
        AssertItems(body, 2);
        Seen.AddRange(body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()!));
    }

    [Scenario("FL-PRD-005-B: la segunda página trae el resto")]
    public async Task SegundaPagina()
    {
        var response = await Get($"{RouteBase}/products?page=1&size=2");
        Assert.True(response.Status == 200, response.ToString());
        var body = response.Json();
        AssertMeta(body, 1, 2, 3, 2);
        AssertItems(body, 1);
        var all = Seen.Concat(body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()!)).OrderBy(s => s).ToArray();
        Assert.Equal(Skus, all);
    }

    [Scenario("FL-PRD-005-C: sin tamaño, la página es la del diseño")]
    public async Task PaginaPorDefecto()
    {
        var response = await Get($"{RouteBase}/products");
        Assert.True(response.Status == 200, response.ToString());
        var body = response.Json();
        AssertMeta(body, 0, 20, 3, 1);
        AssertItems(body, 3);
        var skus = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()!).OrderBy(s => s).ToArray();
        Assert.Equal(Skus, skus);
    }
}
