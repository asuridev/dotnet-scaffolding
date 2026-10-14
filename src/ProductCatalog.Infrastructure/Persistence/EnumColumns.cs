namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProductCatalog.Domain.Enums;

/// <summary>
/// Cada enum persistido se guarda por el NOMBRE de su constante (<c>ACTIVE</c>), como el @Enumerated(STRING) de
/// keel-spring: el miembro de C# (<c>Active</c>) y el valor del cable (<c>active</c>) son otra cosa. Un nombre que el
/// enum no tiene es un dato que no casa con el diseño, y se dice en vez de leer un valor por defecto.
/// </summary>
public static class EnumColumns
{
    /// <summary>ProductStatus por el NOMBRE de su constante, en las dos direcciones.</summary>
    public static readonly ValueConverter<ProductStatus, string> ProductStatusColumn = new(value => ProductStatusConstant(value), constant => ProductStatusOf(constant));

    public static string ProductStatusConstant(ProductStatus value) =>
        value switch
        {
            ProductStatus.Draft => "DRAFT",
            ProductStatus.Active => "ACTIVE",
            ProductStatus.Retired => "RETIRED",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Valor de ProductStatus sin constante")
        };

    public static ProductStatus ProductStatusOf(string constant) =>
        constant switch
        {
            "DRAFT" => ProductStatus.Draft,
            "ACTIVE" => ProductStatus.Active,
            "RETIRED" => ProductStatus.Retired,
            _ => throw new InvalidOperationException($"Valor de enum desconocido en la base para ProductStatus: '{constant}'")
        };
}
