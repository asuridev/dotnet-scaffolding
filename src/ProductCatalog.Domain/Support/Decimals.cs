namespace ProductCatalog.Domain.Support;

/// <summary>La escala de un decimal con la semántica de BigDecimal.setScale.</summary>
public static class Decimals
{
    /// <summary>
    /// La misma cantidad con exactamente <paramref name="scale"/> decimales. Si sobran, se redondea con
    /// <paramref name="rounding"/> (por defecto, la mitad hacia arriba en magnitud: el HALF_UP de Java); si
    /// faltan, se completan con ceros. <c>decimal.Round</c> solo hace lo primero: 2.5 redondeado a dos
    /// decimales sigue siendo 2.5, y la escala es contrato del cable.
    /// </summary>
    public static decimal SetScale(decimal value, int scale, MidpointRounding rounding = MidpointRounding.AwayFromZero)
    {
        var rounded = decimal.Round(value, scale, rounding);
        // Sumar un cero con la escala pedida sube la escala del resultado sin cambiar la cantidad.
        return rounded + new decimal(0, 0, 0, false, (byte)scale);
    }
}
