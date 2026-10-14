namespace ProductCatalog.Domain.Support;

using System.Collections;
using System.Text.Json;

/// <summary>
/// Igualdad de VALOR para los campos de un value object: lo que en Java da el equals de un record.
/// </summary>
/// <remarks>
/// Un decimal compara cantidad Y escala, como BigDecimal.equals: 12.5 y 12.50 son valores distintos, y por
/// eso el constructor del value object normaliza la escala antes de guardar. Un <c>json</c> se compara por
/// contenido; una lista, elemento a elemento; un value object anidado, con su propio Equals.
/// </remarks>
public static class ValueEquality
{
    public static bool Same(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }
        if (a is null || b is null)
        {
            return false;
        }
        return (a, b) switch
        {
            (decimal x, decimal y) => x == y && x.Scale == y.Scale,
            (JsonElement x, JsonElement y) => JsonElement.DeepEquals(x, y),
            (string x, string y) => string.Equals(x, y, StringComparison.Ordinal),
            (IEnumerable x, IEnumerable y) => SameItems(x, y),
            _ => a.Equals(b)
        };
    }

    /// <summary>Un hash coherente con <see cref="Same"/>: dos valores iguales dan el mismo.</summary>
    public static int Hash(params object?[] values)
    {
        var hash = new HashCode();
        foreach (var value in values)
        {
            hash.Add(HashOf(value));
        }
        return hash.ToHashCode();
    }

    private static int HashOf(object? value) => value switch
    {
        null => 0,
        JsonElement element => element.ValueKind.GetHashCode(),
        string text => text.GetHashCode(StringComparison.Ordinal),
        IEnumerable items => items.Cast<object?>().Aggregate(17, (hash, item) => unchecked((hash * 31) + HashOf(item))),
        _ => value.GetHashCode()
    };

    private static bool SameItems(IEnumerable a, IEnumerable b)
    {
        var left = a.GetEnumerator();
        var right = b.GetEnumerator();
        while (true)
        {
            var hasLeft = left.MoveNext();
            if (hasLeft != right.MoveNext())
            {
                return false;
            }
            if (!hasLeft)
            {
                return true;
            }
            if (!Same(left.Current, right.Current))
            {
                return false;
            }
        }
    }
}
