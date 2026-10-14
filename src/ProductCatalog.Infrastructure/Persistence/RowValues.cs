namespace ProductCatalog.Infrastructure.Persistence;

using System.Text.Json;

/// <summary>
/// Lo que el mapeo fila → dominio necesita y no es una asignación: una columna que el esquema admite nula (un campo
/// <c>generated</c>, lo decide el diseño) en un miembro que el dominio no admite nulo, y el <c>json</c> embebido, que
/// se guarda como su texto. Un null donde el dominio no lo admite es un dato roto: se dice qué columna.
/// </summary>
public static class RowValues
{
    public static T Required<T>(T? value, string column)
        where T : struct =>
        value ?? throw new InvalidOperationException($"La columna {column} es null y el dominio no lo admite");

    public static string Required(string? value, string column) =>
        value ?? throw new InvalidOperationException($"La columna {column} es null y el dominio no lo admite");

    /// <summary>El json embebido desde su texto, tal cual se guardó.</summary>
    public static JsonElement Json(string? text, string column)
    {
        using var document = JsonDocument.Parse(Required(text, column));
        return document.RootElement.Clone();
    }

    public static JsonElement? JsonOrNull(string? text)
    {
        if (text is null)
        {
            return null;
        }
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Lo que build no sabe mapear (un value object anidado): compila y falla al usarse, nombrándolo.</summary>
    public static T Unmapped<T>(string what) =>
        throw new NotImplementedException($"TODO (agente): mapear {what} (skill keel-dotnet-database)");
}

/// <summary>Una fila de la tabla de elementos de una lista: su posición es parte de la clave.</summary>
public interface IElementRow
{
    int Position { get; }
}
