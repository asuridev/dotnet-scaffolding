namespace ProductCatalog.Api.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProductCatalog.Infrastructure.Correlation;

/// <summary>
/// La correlación de cada petición: la recibida en <c>X-Correlation-Id</c> si cumple el formato, o una nueva. Vuelve
/// en la cabecera de la respuesta, va en el cuerpo de error y en el ámbito de cada log de la petición.
/// </summary>
public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = CorrelationContext.Accept(context.Request.Headers[CorrelationContext.Header].FirstOrDefault());
        context.Response.Headers[CorrelationContext.Header] = correlationId;
        using (CorrelationContext.Open(correlationId))
        using (logger.BeginScope(new Dictionary<string, object> { ["correlationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
