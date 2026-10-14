namespace ProductCatalog.Api.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using ProductCatalog.Application.Support;
using ProductCatalog.Domain.Errors;
using ProductCatalog.Infrastructure.Persistence;

/// <summary>
/// Traduce CUALQUIER fallo a ErrorResponse, con el status, el <c>error</c> y el <c>code</c> que da el
/// ApiExceptionHandler de keel-spring para el mismo caso; y pone cuerpo a lo que el enrutado responde vacío: una
/// ruta que no existe (404) y un camino que existe con otro método (405).
/// </summary>
/// <remarks>
/// Orden: los fallos de lectura de la petición, la jerarquía de DomainException (cada clase base con su status
/// fijo; un error con status propio lo lleva en su metadata) y el catch-all 500, que es el único que se escribe
/// en el log con su pila: el mediator ya dejó el resultado de cada caso de uso, sin ella.
/// </remarks>
public sealed partial class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    private const string ValidationCode = "VALIDATION_ERROR";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente se fue: no queda a quién responder.
            return;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await Write(context, ToResponse(exception));
            return;
        }
        if (!context.Response.HasStarted && context.Response.StatusCode is StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed)
        {
            await Write(
                context,
                context.Response.StatusCode == StatusCodes.Status405MethodNotAllowed
                    ? ErrorResponse.Of(405, "Method Not Allowed", null, "Método HTTP no soportado")
                    : ErrorResponse.Of(404, "Not Found", null, "Recurso no encontrado"));
        }
    }

    private static Task Write(HttpContext context, ErrorResponse body)
    {
        context.Response.StatusCode = body.Status;
        // El tope de transacción es transitorio: el cliente puede reintentar, y se lo dice la cabecera.
        if (body.Status == TransactionTimeoutException.Status && body.Code == TransactionTimeoutException.Code)
        {
            context.Response.Headers.RetryAfter = "1";
        }
        return context.Response.WriteAsJsonAsync(body, WireJson.Options, context.RequestAborted);
    }

    private ErrorResponse ToResponse(Exception exception)
    {
        switch (exception)
        {
            // ── Validación: errores de FORMA de la petición, 400. El 422 queda para las reglas de negocio.
            case RequestValidationException invalid:
                return ErrorResponse.Of(
                    400,
                    "Validation Error",
                    ValidationCode,
                    invalid.Origin == ViolationSource.Body ? "La petición no supera las validaciones" : "La petición viola restricciones declaradas",
                    invalid.Details);
            // ── Errores de framework
            case MissingParameterException missing:
                return ErrorResponse.Of(400, "Bad Request", ValidationCode, missing.Message);
            case MalformedRequestException:
            case BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest }:
                return ErrorResponse.Of(400, "Bad Request", ValidationCode, "Petición malformada");
            // ── Persistencia
            case TransactionTimeoutException timeout:
                // Sin pila: el motivo va en el mensaje, y una ráfaga de esperas de bloqueo llenaría el log.
                LogTimeout(timeout.InnerException?.Message ?? timeout.Message);
                return ErrorResponse.Of(TransactionTimeoutException.Status, "Service Unavailable", TransactionTimeoutException.Code, TransactionTimeoutException.DefaultMessage, []);
            case IntegrityViolationException integrity:
                LogIntegrity(integrity);
                return ErrorResponse.Of(StatusCodes.Status409Conflict, "Conflict", null, IntegrityViolationException.DefaultMessage);
            // ── Errores de dominio (jerarquía DomainException)
            case DomainException domain:
                return FromDomain(domain);
            // ── Catch-all
            default:
                LogUnhandled(exception);
                return ErrorResponse.Of(500, "Internal Server Error", null, "Ocurrió un error inesperado");
        }
    }

    private static ErrorResponse FromDomain(DomainException exception) =>
        exception switch
        {
            BadRequestException => Domain(400, "Bad Request", exception, "Petición inválida"),
            UnauthorizedException => Domain(401, "Unauthorized", exception, "Autenticación requerida"),
            ForbiddenException => Domain(403, "Forbidden", exception, "Acceso denegado"),
            NotFoundException => Domain(404, "Not Found", exception, "Recurso no encontrado"),
            ConflictException => Domain(409, "Conflict", exception, "Conflicto con el estado actual del recurso"),
            PayloadTooLargeException => Domain(413, "Payload Too Large", exception, "El contenido enviado supera el tamaño permitido"),
            BusinessException => Domain(422, "Business Rule Violation", exception, "Se violó una regla de negocio"),
            _ => Extended(exception)
        };

    private static ErrorResponse Domain(int status, string error, DomainException exception, string fallback) =>
        ErrorResponse.Of(status, error, exception.Code, string.IsNullOrEmpty(exception.GivenMessage) ? fallback : exception.GivenMessage, exception.Details);

    /// <summary>Errores con status extendido (402, 429, 503…): extienden DomainException y lo llevan en la metadata.</summary>
    private static ErrorResponse Extended(DomainException exception)
    {
        var status = exception.HttpStatus ?? StatusCodes.Status422UnprocessableEntity;
        var phrase = ReasonPhrases.GetReasonPhrase(status);
        if (phrase.Length == 0)
        {
            phrase = "Error";
        }
        return ErrorResponse.Of(status, phrase, exception.Code, string.IsNullOrEmpty(exception.GivenMessage) ? phrase : exception.GivenMessage, exception.Details);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Excepción no controlada")]
    private partial void LogUnhandled(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Transacción cancelada por tiempo: {Reason}")]
    private partial void LogTimeout(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Violación de integridad no asociada a ninguna constraint conocida")]
    private partial void LogIntegrity(Exception exception);
}
