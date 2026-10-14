namespace ProductCatalog.Application.Support;

/// <summary>
/// <c>conventions.nulls: omit</c>: las instancias de un tipo marcado no escriben sus propiedades sin valor.
/// Lo llevan las respuestas y los payloads de evento; el cuerpo de error, nunca (su forma es estable).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class OmitNullsAttribute : Attribute;
