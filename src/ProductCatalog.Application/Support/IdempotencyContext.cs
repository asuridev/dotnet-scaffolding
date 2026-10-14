namespace ProductCatalog.Application.Support;

/// <summary>
/// La clave de idempotencia de la petición en curso (cabecera Idempotency-Key, keySource: client-key). Es
/// transporte, así que no viaja dentro del comando: la abre el middleware de entrada HTTP (Api/Rest) y la lee el
/// handler. Vive en un AsyncLocal, que sigue a la petición por sus awaits.
/// </summary>
/// <remarks>
/// La cabecera es OPCIONAL: sin ella Get() devuelve null y la operación se ejecuta sin deduplicar — no se rechaza, salvo
/// que el diseño declare el error que la exige.
/// </remarks>
public static class IdempotencyContext
{
    /// <summary>La cabecera HTTP de la clave.</summary>
    public const string Header = "Idempotency-Key";

    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>La clave de la petición en curso, o null si el cliente no la envió.</summary>
    public static string? Get() => Current.Value;

    /// <summary>Ejecuta la acción con la clave recibida (sin clave, o en blanco, con ninguna).</summary>
    public static async Task RunWith(string? idempotencyKey, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var previous = Current.Value;
        Current.Value = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        try
        {
            await action();
        }
        finally
        {
            Current.Value = previous;
        }
    }
}
