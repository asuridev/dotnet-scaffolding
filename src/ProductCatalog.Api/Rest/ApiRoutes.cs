namespace ProductCatalog.Api.Rest;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ProductCatalog.Api.Endpoints;

/// <summary>Una fila del contrato HTTP del servicio: la operación, su ruta, su status y cómo viaja su entrada.</summary>
public sealed record ApiRoute(string Operation, string Method, string Path, int Status, bool Location, IReadOnlyList<string> Query, string? Body, RequestDelegate Handler);

/// <summary>
/// El contrato HTTP del servicio como DATOS: una fila por operación con ruta. Es el único mapa de rutas (de aquí se
/// registran todos los endpoints) y lo compara con keel-spring el test de paridad del contrato.
/// </summary>
public static class ApiRoutes
{
    public static IReadOnlyList<ApiRoute> All { get; } =
    [
        new("createProduct", "POST", "/api/v1/products", 201, Location: true, Query: [], Body: "required", Handler: ProductV1Endpoints.CreateProduct),
        new("getProduct", "GET", "/api/v1/products/{id}", 200, Location: false, Query: [], Body: null, Handler: ProductV1Endpoints.GetProduct),
        new("listProducts", "GET", "/api/v1/products", 200, Location: false, Query: ["page", "size", "sort"], Body: null, Handler: ProductV1Endpoints.ListProducts),
        new("activateProduct", "POST", "/api/v1/products/{id}/activate", 204, Location: false, Query: [], Body: null, Handler: ProductV1Endpoints.ActivateProduct),
        new("retireProduct", "POST", "/api/v1/products/{id}/retire", 204, Location: false, Query: [], Body: null, Handler: ProductV1Endpoints.RetireProduct),
    ];

    public static IEndpointRouteBuilder MapApiRoutes(this IEndpointRouteBuilder endpoints)
    {
        foreach (var route in All)
        {
            endpoints.MapMethods(route.Path, [route.Method], route.Handler);
        }
        return endpoints;
    }
}
