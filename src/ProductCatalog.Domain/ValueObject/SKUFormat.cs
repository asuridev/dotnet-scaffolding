namespace ProductCatalog.Domain.ValueObject;

using ProductCatalog.Domain.Errors;
using System.Text.RegularExpressions;

/// <summary>Formato declarado por el value type SKU: Identificador de producto para el negocio.</summary>
public static partial class SKUFormat
{
    // La expresión del diseño, en un único sitio: quien la vuelva a escribir a mano crea una segunda
    // definición que nadie sincroniza.
    [GeneratedRegex(@"^(?:^[A-Z]{3}-[0-9]{4}$)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Format();

    /// <summary>
    /// Hace cumplir el formato sobre un valor YA NORMALIZADO. Se llama donde se normaliza (el factory o el
    /// método de negocio de la entidad, o el handler que normaliza antes de entregarlo), nunca sobre lo que
    /// llega del cable: el patrón describe el valor normalizado, y comprobarlo antes rechaza peticiones
    /// válidas. No aplica a null ni a vacío: la presencia la decide la regla de negocio.
    /// </summary>
    public static void Validate(string? value)
    {
        if (!Matches(value))
        {
            throw new ValueFormatException("El valor no cumple el formato declarado por SKU");
        }
    }

    /// <summary>El mismo juicio sin lanzar, para quien tenga que decidir en vez de rechazar.</summary>
    public static bool Matches(string? value) => string.IsNullOrWhiteSpace(value) || Format().IsMatch(value);
}
