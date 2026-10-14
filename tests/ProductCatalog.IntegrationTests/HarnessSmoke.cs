// Humo del arnés: la fontanería de la que dependen TODOS los flujos (servidor vivo, reset, base de prueba, API).
// infra/score-scenarios.sh lo ejecuta ANTES que la suite: correrla sobre una fontanería rota produce decenas de
// fallos que parecen de negocio y no lo son. Lo generó keel-dotnet build: no se edita.

namespace ProductCatalog.IntegrationTests;

using ProductCatalog.IntegrationTests.Support;
using Xunit;

public sealed class HarnessSmoke : FlowTest
{
    [Scenario("SMOKE-1: el reset de estado se ejecuta sin error")]
    public void ElResetSeEjecuta() =>
        // Sin él, ningún flujo arranca con el Given que declara.
        ResetState();

    [Scenario("SMOKE-2: el servidor responde")]
    public async Task ElServidorResponde()
    {
        var response = await Get("/livez");
        Assert.True(response.Status == 200, response.ToString());
    }

    [Scenario("SMOKE-3: la base de prueba responde a una sentencia")]
    public void LaBaseResponde() =>
        // Es la vía de los Then que miran el almacén: si no responde, fallarían por la fontanería.
        Assert.Equal("1", Db("SELECT 1").Trim());

    [Scenario("SMOKE-4: la API responde con ErrorResponse y correlación")]
    public async Task LaApiRespondeConErrorResponse()
    {
        var response = await Get($"{RouteBase}/keel-smoke-ruta-inexistente", new() { ["X-Correlation-Id"] = "keel-smoke" });
        Assert.True(response.Status == 404, response.ToString());
        Assert.Equal(404, response.Json().GetProperty("status").GetInt32());
        Assert.Equal("keel-smoke", response.Header("x-correlation-id"));
    }
}
