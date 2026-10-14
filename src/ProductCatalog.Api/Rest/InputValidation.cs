namespace ProductCatalog.Api.Rest;

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Una regla de validación del diseño (keel-core/gen/constraints.js, validationRules): las mismas que keel-spring
/// escribe como anotaciones de Bean Validation, con sus mensajes por defecto.
/// </summary>
public sealed class InputRule
{
    private static readonly ConcurrentDictionary<string, Regex> Patterns = new(StringComparer.Ordinal);

    private InputRule(string kind)
    {
        Kind = kind;
    }

    public string Kind { get; }

    private int? MinSize { get; init; }

    private int? MaxSize { get; init; }

    private string? Regexp { get; init; }

    private string? Bound { get; init; }

    private int IntegerDigits { get; init; }

    private int FractionDigits { get; init; }

    public static InputRule NotNull { get; } = new("notNull");

    public static InputRule NotBlank { get; } = new("notBlank");

    public static InputRule NotEmpty { get; } = new("notEmpty");

    public static InputRule Size(int? min, int? max) => new("size") { MinSize = min, MaxSize = max };

    public static InputRule Pattern(string regexp) => new("pattern") { Regexp = regexp };

    public static InputRule Min(string value) => new("min") { Bound = value };

    public static InputRule Max(string value) => new("max") { Bound = value };

    public static InputRule Digits(int integer, int fraction) => new("digits") { IntegerDigits = integer, FractionDigits = fraction };

    /// <summary>El mensaje de Hibernate Validator si el valor la incumple; null si la cumple. Un valor ausente solo incumple las de presencia.</summary>
    public string? Violation(object? value) =>
        Kind switch
        {
            "notNull" => value is null ? "must not be null" : null,
            "notBlank" => value is null || (value is string text && string.IsNullOrWhiteSpace(text)) ? "must not be blank" : null,
            "notEmpty" => value is null || Length(value) == 0 ? "must not be empty" : null,
            "size" => SizeViolation(value),
            "pattern" => value is string text && !Matches(text) ? $"must match \"{Regexp}\"" : null,
            "min" => Compare(value) < 0 ? $"must be greater than or equal to {Bound}" : null,
            "max" => Compare(value) > 0 ? $"must be less than or equal to {Bound}" : null,
            "digits" => DigitsViolation(value),
            _ => null
        };

    /// <summary>La longitud de un texto o el número de elementos de una lista; null para lo demás.</summary>
    private static int? Length(object value) =>
        value switch
        {
            string text => text.Length,
            ICollection collection => collection.Count,
            IEnumerable items => items.Cast<object?>().Count(),
            _ => null
        };

    private string? SizeViolation(object? value)
    {
        if (value is null || Length(value) is not { } length)
        {
            return null;
        }
        var min = MinSize ?? 0;
        var max = MaxSize ?? int.MaxValue;
        return length < min || length > max ? $"size must be between {min} and {max}" : null;
    }

    /// <summary>Como el matches() de Java: la expresión entera, sin el salto de línea final que admite el $ de .NET.</summary>
    private bool Matches(string text) =>
        Patterns.GetOrAdd(Regexp!, regexp => new Regex($@"\A(?:{regexp})\z", RegexOptions.CultureInvariant)).IsMatch(text);

    private int? Compare(object? value)
    {
        decimal? number = value switch
        {
            decimal amount => amount,
            int integer => integer,
            long integer => integer,
            _ => null
        };
        return number is { } present ? decimal.Compare(present, decimal.Parse(Bound!, NumberStyles.Float, CultureInfo.InvariantCulture)) : null;
    }

    private string? DigitsViolation(object? value)
    {
        if (value is not decimal amount)
        {
            return null;
        }
        var parts = amount.ToString(CultureInfo.InvariantCulture).TrimStart('-').Split('.');
        var integer = parts[0].TrimStart('0');
        var fraction = parts.Length > 1 ? parts[1].TrimEnd('0') : "";
        return integer.Length > IntegerDigits || fraction.Length > FractionDigits
            ? $"numeric value out of bounds (<{IntegerDigits} digits>.<{FractionDigits} digits> expected)"
            : null;
    }
}

/// <summary>
/// Acumula las violaciones de una petición y lanza UNA vez con todas, como Bean Validation. Cada una es «campo
/// mensaje».
/// </summary>
public sealed class InputViolations(ViolationSource source)
{
    private readonly List<string> details = [];

    public void Check(string path, object? value, params InputRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            if (rule.Violation(value) is { } message)
            {
                details.Add($"{path} {message}");
            }
        }
    }

    /// <summary>Las reglas de CADA ELEMENTO de una lista (<c>labels[3] size must be between 0 and 40</c>), como Bean Validation.</summary>
    public void CheckEach<T>(string path, IEnumerable<T>? values, params InputRule[] rules)
    {
        var index = 0;
        foreach (var value in values ?? [])
        {
            Check($"{path}[{index++}]", value, rules);
        }
    }

    public void ThrowIfAny()
    {
        if (details.Count > 0)
        {
            throw new RequestValidationException([.. details], source);
        }
    }
}
