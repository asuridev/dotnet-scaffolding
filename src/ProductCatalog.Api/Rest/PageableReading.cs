namespace ProductCatalog.Api.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using ProductCatalog.Domain.Repository;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary><c>?page=&amp;size=&amp;sort=prop,dir</c> → Pageable, como lo lee Spring Data.</summary>
public static partial class PageableReading
{
    public static Pageable Read(IQueryCollection query, int defaultSize, int maxSize, IReadOnlyList<SortOrder> defaultSort)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Bounded(First(query["page"]), int.MaxValue) ?? 0;
        var size = Bounded(First(query["size"]), maxSize) ?? defaultSize;
        if (size < 1)
        {
            size = defaultSize;
        }
        if (size > maxSize)
        {
            size = maxSize;
        }
        var sort = ReadSort(query["sort"]);
        return new Pageable(page, size, sort.Count > 0 ? sort : defaultSort);
    }

    private static string? First(StringValues values) => values.Count == 0 ? null : values[0];

    /// <summary>
    /// Un entero con los límites de Spring Data: sin texto no hay valor (se usa el default); lo que no es un entero de
    /// 32 bits se lee como 0; negativo, 0; por encima del tope, el tope.
    /// </summary>
    private static int? Bounded(string? text, int upper)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (!Integer().IsMatch(text) || !long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) || parsed is > int.MaxValue or < int.MinValue)
        {
            return 0;
        }
        return parsed < 0 ? 0 : parsed > upper ? upper : (int)parsed;
    }

    /// <summary>
    /// <c>sort</c> repetido o con varias propiedades: <c>name,desc</c>, <c>status,name</c>. La última pieza es la
    /// dirección si lo es (sin distinguir mayúsculas) y vale para todas las propiedades de ese valor.
    /// </summary>
    private static List<SortOrder> ReadSort(StringValues values)
    {
        var orders = new List<SortOrder>();
        foreach (var value in values)
        {
            var parts = (value ?? string.Empty).Split(',').Where(part => part.Trim().Length > 0).ToList();
            if (parts.Count == 0)
            {
                continue;
            }
            var last = parts[^1].Trim().ToLowerInvariant();
            SortDirection? direction = last switch
            {
                "asc" => SortDirection.Asc,
                "desc" => SortDirection.Desc,
                _ => null
            };
            var properties = direction is null ? parts : parts[..^1];
            orders.AddRange(properties.Select(property => new SortOrder(property, direction ?? SortDirection.Asc)));
        }
        return orders;
    }

    [GeneratedRegex(@"^[+-]?\d+\z")]
    private static partial Regex Integer();
}
