namespace ProductCatalog.Api.Rest;

using ProductCatalog.Application.Support;

/// <summary>
/// Lleva la cabecera Idempotency-Key de la petición al <see cref="IdempotencyContext"/>, de donde la lee el handler
/// de una operación idempotente. Va detrás de la correlación: un rechazo posterior sigue llevando su correlationId.
/// </summary>
public sealed class IdempotencyKeyMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var key = context.Request.Headers.TryGetValue(IdempotencyContext.Header, out var values) ? values.FirstOrDefault() : null;
        return IdempotencyContext.RunWith(key, () => next(context));
    }
}
