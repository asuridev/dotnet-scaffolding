namespace ProductCatalog.Domain.Errors;

/// <summary>
/// Base de todos los errores de dominio: violaciones intencionales de reglas de negocio que no deben
/// reintentarse. Los errores de infraestructura (un timeout de la base, una conexión caída) NO extienden
/// esta clase, para que puedan tratarse aparte (reintentos).
/// </summary>
/// <remarks>
/// Lleva metadata estructurada opcional (código, status, argumentos, detalles) con la que la API construye
/// el cuerpo de error con códigos estables.
/// </remarks>
public abstract class DomainException : Exception
{
    protected DomainException(
        string? message,
        string? code = null,
        int? httpStatus = null,
        IReadOnlyList<object?>? args = null,
        IReadOnlyList<string>? details = null,
        Exception? cause = null)
        : base(message, cause)
    {
        GivenMessage = message;
        Code = code;
        HttpStatus = httpStatus;
        Args = args;
        Details = details is null ? null : [.. details];
    }

    /// <summary>
    /// El mensaje con el que se construyó, o null. <see cref="Exception.Message"/> nunca es null (sin mensaje,
    /// .NET inventa uno en inglés), y la API necesita saber si responder con el de la clase base.
    /// </summary>
    public string? GivenMessage { get; }

    /// <summary>El código estable del error (contrato público), o null si el diseño no lo nombra.</summary>
    public string? Code { get; }

    /// <summary>El status HTTP con el que responde la API, o null para el de su clase base.</summary>
    public int? HttpStatus { get; }

    public IReadOnlyList<object?>? Args { get; }

    public IReadOnlyList<string>? Details { get; }
}
