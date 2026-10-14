namespace ProductCatalog.Domain.ValueObject;

using ProductCatalog.Domain.Errors;
using ProductCatalog.Domain.Support;

/// <summary>Importe monetario con su divisa.</summary>
public sealed class Money : IEquatable<Money>
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0m)
        {
            throw new InvalidValueException("Money.amount es menor que el mínimo declarado por su tipo (0)");
        }
        if (currency is null)
        {
            throw new InvalidValueException("Money.currency es obligatorio");
        }
        if (currency.Length < 3)
        {
            throw new InvalidValueException("Money.currency es más corto que el mínimo declarado por su tipo (3)");
        }
        if (currency.Length > 3)
        {
            throw new InvalidValueException("Money.currency es más largo que el máximo declarado por su tipo (3)");
        }
        Amount = Decimals.SetScale(amount, 2);
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    /// <summary>
    /// Igualdad de VALOR, campo a campo. Un decimal compara también su escala, como el equals de
    /// BigDecimal: por eso el constructor la normaliza.
    /// </summary>
    public bool Equals(Money? other) =>
        other is not null
            && ValueEquality.Same(Amount, other.Amount)
            && ValueEquality.Same(Currency, other.Currency);

    public override bool Equals(object? obj) => Equals(obj as Money);

    public override int GetHashCode() => ValueEquality.Hash(Amount, Currency);
}
