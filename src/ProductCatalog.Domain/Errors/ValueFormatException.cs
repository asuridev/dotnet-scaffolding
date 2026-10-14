namespace ProductCatalog.Domain.Errors;

/// <summary>
/// Un valor no cumple el formato de su value type. La lanza la clase &lt;Tipo&gt;Format del dominio, donde vive
/// la expresión regular del diseño. Sobre una petición HTTP el formato ya lo rechaza el lector de entrada:
/// esto es la guarda del dominio para lo que llega por otra puerta (un evento, una operación interna).
/// </summary>
public sealed class ValueFormatException(string message)
    : BadRequestException(message, code: "VALIDATION_ERROR", httpStatus: 400);
