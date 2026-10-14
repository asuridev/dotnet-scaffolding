namespace ProductCatalog.Api.Endpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Api.Rest;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Application.Commands;
using ProductCatalog.Application.Queries;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Domain.ValueObject;

/// <summary>
/// Operaciones HTTP de Product. Solo traducen: leen la petición, despachan el caso de uso y escriben su resultado.
/// Las registra ApiRoutes.
/// </summary>
public static class ProductV1Endpoints
{
    /// <summary>
    /// POST /products (createProduct).
    /// Da de alta un producto en estado draft.
    /// </summary>
    public static async Task CreateProduct(HttpContext http)
    {
        var message = await ReadCreateProduct(http.Request);
        var response = await Mediator(http).Dispatch(message, http.RequestAborted);
        // 201 con la ruta que LEE el recurso creado (keel-core/gen/api-contract.js).
        http.Response.Headers.Location = HttpReply.Location(http.Request, "/api/v1/products/{id}", response.Id);
        await HttpReply.Json(http, 201, response);
    }

    /// <summary>Lee POST /products (createProduct) al mensaje de su caso de uso.</summary>
    private static async Task<CreateProductCommand> ReadCreateProduct(HttpRequest httpRequest)
    {
        var requestFields = await JsonBodyReader.ReadObject(httpRequest, required: true);
        var sku = JsonBodyReader.Value<string?>(requestFields, "sku");
        var name = JsonBodyReader.Value<string?>(requestFields, "name");
        var notes = JsonBodyReader.Value<string?>(requestFields, "notes");
        var price = ValueReaders.ReadMoney(JsonBodyReader.Property(requestFields, "price"));
        var apiToken = JsonBodyReader.Value<string?>(requestFields, "apiToken");
        var bodyChecks = new InputViolations(ViolationSource.Body);
        bodyChecks.Check("sku", sku, InputRule.NotBlank, InputRule.Size(null, 8), InputRule.Pattern("^[A-Z]{3}-[0-9]{4}$"));
        bodyChecks.Check("name", name, InputRule.NotBlank, InputRule.Size(null, 120));
        bodyChecks.Check("price", price, InputRule.NotNull);
        bodyChecks.ThrowIfAny();
        return new CreateProductCommand
        {
            Sku = JsonBodyReader.Present(sku),
            Name = JsonBodyReader.Present(name),
            Notes = notes,
            Price = JsonBodyReader.Present(price),
            ApiToken = apiToken,
        };
    }

    /// <summary>
    /// GET /products/{id} (getProduct).
    /// Recupera un producto por su identificador.
    /// </summary>
    public static async Task GetProduct(HttpContext http)
    {
        var message = ReadGetProduct(http.Request);
        var response = await Mediator(http).Dispatch(message, http.RequestAborted);
        await HttpReply.Json(http, 200, response);
    }

    /// <summary>Lee GET /products/{id} (getProduct) al mensaje de su caso de uso.</summary>
    private static GetProductQuery ReadGetProduct(HttpRequest httpRequest)
    {
        var id = TextValues.Uuid(TextValues.Route(httpRequest, "id"));
        return new GetProductQuery
        {
            Id = JsonBodyReader.PresentValue(id),
        };
    }

    /// <summary>
    /// GET /products (listProducts).
    /// Lista los productos del catálogo paginados.
    /// </summary>
    public static async Task ListProducts(HttpContext http)
    {
        var message = ReadListProducts(http.Request);
        var response = await Mediator(http).Dispatch(message, http.RequestAborted);
        await HttpReply.Json(http, 200, response);
    }

    /// <summary>Lee GET /products (listProducts) al mensaje de su caso de uso.</summary>
    private static ListProductsQuery ReadListProducts(HttpRequest httpRequest)
    {
        var pageable = PageableReading.Read(httpRequest.Query, defaultSize: 20, maxSize: 100, defaultSort: []);
        return new ListProductsQuery
        {
            Pageable = pageable,
        };
    }

    /// <summary>
    /// POST /products/{id}/activate (activateProduct).
    /// Publica un producto en borrador; solo con un precio mayor que cero.
    /// </summary>
    public static async Task ActivateProduct(HttpContext http)
    {
        var message = ReadActivateProduct(http.Request);
        await Mediator(http).Dispatch(message, http.RequestAborted);
        HttpReply.Status(http, 204);
    }

    /// <summary>Lee POST /products/{id}/activate (activateProduct) al mensaje de su caso de uso.</summary>
    private static ActivateProductCommand ReadActivateProduct(HttpRequest httpRequest)
    {
        var id = TextValues.Uuid(TextValues.Route(httpRequest, "id"));
        return new ActivateProductCommand
        {
            Id = JsonBodyReader.PresentValue(id),
        };
    }

    /// <summary>
    /// POST /products/{id}/retire (retireProduct).
    /// Retira un producto del catálogo de forma definitiva.
    /// </summary>
    public static async Task RetireProduct(HttpContext http)
    {
        var message = ReadRetireProduct(http.Request);
        await Mediator(http).Dispatch(message, http.RequestAborted);
        HttpReply.Status(http, 204);
    }

    /// <summary>Lee POST /products/{id}/retire (retireProduct) al mensaje de su caso de uso.</summary>
    private static RetireProductCommand ReadRetireProduct(HttpRequest httpRequest)
    {
        var id = TextValues.Uuid(TextValues.Route(httpRequest, "id"));
        return new RetireProductCommand
        {
            Id = JsonBodyReader.PresentValue(id),
        };
    }

    private static IUseCaseMediator Mediator(HttpContext http) => http.RequestServices.GetRequiredService<IUseCaseMediator>();
}
