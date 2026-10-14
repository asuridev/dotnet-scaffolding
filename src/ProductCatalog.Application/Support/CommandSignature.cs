namespace ProductCatalog.Application.Support;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Firma determinista del contenido de un comando: la que se guarda junto a la clave (client-key) para distinguir el
/// reintento de la clave reutilizada, o la CLAVE misma (payload-hash).
/// </summary>
/// <remarks>
/// Generada y no escrita en cada handler: se compara contra una firma guardada en OTRO despliegue, y dos formas de
/// calcularla —o un refactor que cambie una— dejarían de deduplicar sin que nada lo delate.
///
/// Canónica: propiedades públicas ordenadas por nombre (los métodos, como IdempotencyScope, no entran), cada escalar
/// precedido de su longitud (ningún contenido puede imitar un separador), el orden de las listas conservado, null y
/// ausente como marca propia, un decimal sin ceros finales (1.50 y 1.5 son el mismo importe) y un binario por su digest.
/// No usa System.Text.Json: las opciones del cable no son contrato de la firma, y cambiarlas movería en silencio firmas
/// ya guardadas.
/// </remarks>
public static class CommandSignature
{
    /// <summary>SHA-256 en hexadecimal de la forma canónica del comando.</summary>
    public static string Of(object command) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(command))));

    /// <summary>La forma canónica: visible para poder verificarla en una prueba.</summary>
    public static string Canonical(object? value)
    {
        switch (value)
        {
            case null:
                return "~";
            case string text:
                return Scalar(text);
            case decimal number:
                return Scalar(WithoutTrailingZeros(number.ToString(CultureInfo.InvariantCulture)));
            case bool flag:
                return Scalar(flag ? "true" : "false");
            case DateTimeOffset instant:
                return Scalar(instant.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            case DateOnly date:
                return Scalar(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            case Guid id:
                return Scalar(id.ToString("D"));
            case JsonElement json:
                return Scalar(json.GetRawText());
            // Un binario (el contenido de una subida) puede pesar megas: entra por su digest, como en keel-spring.
            case byte[] bytes:
                return Scalar(Convert.ToHexStringLower(SHA256.HashData(bytes)));
            case Enum member:
                return Scalar(member.ToString());
            case IFormattable formattable:
                return Scalar(formattable.ToString(null, CultureInfo.InvariantCulture));
            case IEnumerable items:
                return "[" + string.Join(",", items.Cast<object?>().Select(Canonical)) + "]";
        }
        var type = value.GetType();
        // El Optional de un PATCH: ausente y null son la misma marca, como el Optional vacío de keel-spring.
        if (type.IsGenericType && type.Name == "Optional`1")
        {
            var present = (bool)type.GetProperty("IsPresent")!.GetValue(value)!;
            return present ? Canonical(type.GetProperty("Value")!.GetValue(value)) : "~";
        }
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal);
        return "{" + string.Join(",", properties.Select(property => Scalar(property.Name) + Canonical(property.GetValue(value)))) + "}";
    }

    /// <summary>Prefijo de longitud: hace imposible que un contenido imite un separador.</summary>
    private static string Scalar(string raw) => string.Create(CultureInfo.InvariantCulture, $"{raw.Length}:{raw}");

    private static string WithoutTrailingZeros(string text) => text.Contains('.', StringComparison.Ordinal) ? text.TrimEnd('0').TrimEnd('.') : text;
}
