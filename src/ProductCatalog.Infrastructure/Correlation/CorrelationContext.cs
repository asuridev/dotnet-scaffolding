namespace ProductCatalog.Infrastructure.Correlation;

using System.Text.RegularExpressions;

/// <summary>
/// Contexto de correlación de la petición o del mensaje en curso: el hilo que une petición HTTP → caso de uso →
/// evento → mensaje saliente, y el valor que lleva ErrorResponse.
/// </summary>
/// <remarks>
/// Vive en un <see cref="AsyncLocal{T}"/>, que es lo que en .NET sigue a una petición a través de sus await (en
/// keel-spring, el ThreadLocal + MDC de CorrelationContext; en keel-nest, un AsyncLocalStorage). Lo abre el
/// middleware de entrada HTTP con <see cref="Open"/>, que devuelve lo que lo cierra.
/// </remarks>
public static partial class CorrelationContext
{
    public const string Header = "X-Correlation-Id";

    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>
    /// Lo que se acepta como correlationId. El valor llega de FUERA y acaba en logs, cabeceras y eventos: sin
    /// cota, un cliente podría meter saltos de línea o kilobytes en cada registro.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Accepted();

    /// <summary>
    /// El correlationId EFECTIVO para un valor recibido: el mismo si cumple el formato; uno nuevo si no llegó o
    /// no lo cumple. No se trunca: produciría ids que se confunden entre sí.
    /// </summary>
    public static string Accept(string? received) => received is not null && Accepted().IsMatch(received) ? received : Guid.NewGuid().ToString();

    /// <summary>La correlación en curso, o null si no hay ninguna abierta.</summary>
    public static string? Get() => Current.Value;

    /// <summary>Abre la correlación indicada hasta que se libera lo que devuelve.</summary>
    public static IDisposable Open(string correlationId)
    {
        var previous = Current.Value;
        Current.Value = Accept(correlationId);
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
