namespace ProductCatalog.Infrastructure.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>El cuerpo de las dos sondas: <c>{"status":"UP"}</c> u <c>{"status":"OUT_OF_SERVICE"}</c>.</summary>
public sealed record HealthStatus(string Status);

/// <summary>Las sondas del servicio, con el contrato de los otros dos servidores del mismo diseño.</summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // El proceso está vivo. No consulta dependencias: si fallara por ellas, el orquestador lo reiniciaría sin motivo.
        endpoints.MapGet("/livez", () => Results.Json(new HealthStatus("UP")));
        // El proceso acepta tráfico. Deja de hacerlo en cuanto empieza el apagado ordenado.
        endpoints.MapGet("/readyz", (GracefulShutdown shutdown) => shutdown.Draining
            ? Results.Json(new HealthStatus("OUT_OF_SERVICE"), statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Json(new HealthStatus("UP")));
        return endpoints;
    }
}
