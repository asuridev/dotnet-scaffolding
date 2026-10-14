namespace ProductCatalog.IntegrationTests;

using ProductCatalog.IntegrationTests.Support;
using Xunit;

/// <summary>FL-PRD-004 · consultas de un producto inexistente o con id mal formado.</summary>
public sealed class ProductQueryFlow : FlowTest
{
    [Scenario("FL-PRD-004: un producto que no existe")]
    public async Task NoExiste()
    {
        var response = await Get($"{RouteBase}/products/0192f1d2-0000-7000-8000-000000000001");
        Assert.True(response.Status == 404, response.ToString());
        Assert.Equal("PRODUCT_NOT_FOUND", response.Json().GetProperty("code").GetString());
    }

    [Scenario("FL-PRD-004-B: un id que no es un uuid")]
    public async Task IdNoUuid()
    {
        var response = await Get($"{RouteBase}/products/no-es-un-uuid");
        Assert.True(response.Status == 400, response.ToString());
        Assert.Equal("VALIDATION_ERROR", response.Json().GetProperty("code").GetString());
    }
}
