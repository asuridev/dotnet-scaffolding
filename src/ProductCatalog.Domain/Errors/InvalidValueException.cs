namespace ProductCatalog.Domain.Errors;

/// <summary>
/// Un valor no cumple lo que declara su value object: presencia, formato, longitud, cotas o escala.
/// Lo lanza el constructor del value object, el único punto por el que pasa cualquier valor de ese tipo,
/// venga del cable, de la base de datos o de otro punto del dominio.
/// </summary>
public sealed class InvalidValueException(string message)
    : BadRequestException(message, code: "VALIDATION_ERROR", httpStatus: 400);
