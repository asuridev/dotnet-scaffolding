namespace ProductCatalog.Api.Rest;

// Los fallos de LECTURA de una petición, antes de que llegue a ningún caso de uso. Los traduce
// ApiErrorMiddleware; no son errores de dominio.

/// <summary>La petición no se puede leer: JSON roto, un tipo que no encaja, un value object inválido.</summary>
public sealed class MalformedRequestException(Exception? cause = null) : Exception("Petición malformada", cause);

/// <summary>Falta un parámetro obligatorio de la query.</summary>
public sealed class MissingParameterException(string parameter) : Exception("Falta el parámetro '" + parameter + "' en la petición")
{
    public string Parameter { get; } = parameter;
}

/// <summary>De dónde sale lo que no supera la validación: en keel-spring, el cuerpo y los parámetros sueltos responden con mensajes distintos.</summary>
public enum ViolationSource
{
    Body,
    Params
}

/// <summary>La petición se leyó pero incumple lo que el diseño declara.</summary>
public sealed class RequestValidationException(IReadOnlyList<string> details, ViolationSource source) : Exception("La petición no supera las validaciones")
{
    public IReadOnlyList<string> Details { get; } = details;

    public ViolationSource Origin { get; } = source;
}
