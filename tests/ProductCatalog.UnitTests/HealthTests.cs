namespace ProductCatalog.UnitTests;

using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Infrastructure.Health;
using System.Net;
using Xunit;

public sealed class HealthTests
{
    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(InMemoryServer server, string path)
    {
        using var client = server.CreateClient();
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArrancaYLasDosSondasRespondenUp()
    {
        await using var server = new InMemoryServer();
        Assert.Equal((HttpStatusCode.OK, """{"status":"UP"}"""), await GetAsync(server, "/livez"));
        Assert.Equal((HttpStatusCode.OK, """{"status":"UP"}"""), await GetAsync(server, "/readyz"));
    }

    [Fact]
    public async Task MientrasDrenaReadyzRespondeOutOfServiceYLivezSigueUp()
    {
        await using var server = new InMemoryServer();
        server.Services.GetRequiredService<GracefulShutdown>().StartDraining();
        Assert.Equal((HttpStatusCode.ServiceUnavailable, """{"status":"OUT_OF_SERVICE"}"""), await GetAsync(server, "/readyz"));
        Assert.Equal((HttpStatusCode.OK, """{"status":"UP"}"""), await GetAsync(server, "/livez"));
    }
}
