namespace ProductCatalog.Domain.Aggregate;

using ProductCatalog.Domain.Enums;
using ProductCatalog.Domain.Errors;
using ProductCatalog.Domain.ValueObject;

/// <summary>El estado completo de Product, tal como lo rehidrata la persistencia.</summary>
public sealed class ProductState
{
    public required Guid Id { get; init; }

    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required string? Notes { get; init; }

    public required Money Price { get; init; }

    /// <summary>Token de sincronización con el proveedor; nunca se expone.</summary>
    public required string? ApiToken { get; init; }

    public required ProductStatus Status { get; init; }

    /// <summary>Versión de concurrencia optimista; la gestiona la persistencia. Null hasta la primera escritura.</summary>
    public required long? LockVersion { get; init; }
}

// Invariante (guardada en el factory y en cada método mutador): Un producto active siempre tiene price.amount mayor que cero.
/// <summary>Producto publicable del catálogo.</summary>
public class Product
{
    // Transiciones válidas del lifecycle del diseño; un estado sin destinos es terminal.
    private static readonly IReadOnlyDictionary<ProductStatus, ProductStatus[]> Transitions = new Dictionary<ProductStatus, ProductStatus[]>
    {
        [ProductStatus.Draft] = [ProductStatus.Active, ProductStatus.Retired],
        [ProductStatus.Active] = [ProductStatus.Retired],
        [ProductStatus.Retired] = [],
    };

    /// <summary>
    /// Alta de producto: nace en draft. El id lo decide quien llama (la idempotencia lo reclama antes).
    /// </summary>
    public static Product Create(Guid id, string sku, string name, string? notes, Money price, string? apiToken)
    {
        ArgumentNullException.ThrowIfNull(price);
        SKUFormat.Validate(sku);
        var product = new Product(new ProductState
        {
            Id = id,
            Sku = sku,
            Name = name,
            Notes = notes,
            Price = price,
            ApiToken = apiToken,
            Status = ProductStatus.Draft,
            LockVersion = null
        });
        product.RequireActivePrice();
        return product;
    }

    /// <summary>Publica el producto: solo desde draft y con precio mayor que cero (el estado se evalúa primero).</summary>
    public void Activate()
    {
        if (Status != ProductStatus.Draft)
        {
            throw new ProductNotDraftError("El producto no está en draft");
        }
        if (Price.Amount <= 0m)
        {
            throw new ProductPriceNotPositiveError("El precio del producto debe ser mayor que cero");
        }
        TransitionTo(ProductStatus.Active);
    }

    /// <summary>Retira el producto (desde draft o active); uno ya retirado responde PRODUCT_ALREADY_RETIRED.</summary>
    public void Retire()
    {
        if (Status == ProductStatus.Retired)
        {
            throw new ProductAlreadyRetiredError("El producto ya está retirado");
        }
        TransitionTo(ProductStatus.Retired);
    }

    // Invariante: un producto active siempre tiene price.amount mayor que cero.
    private void RequireActivePrice()
    {
        if (Status == ProductStatus.Active && Price.Amount <= 0m)
        {
            throw new ProductPriceNotPositiveError("Un producto active requiere un precio mayor que cero");
        }
    }

    /// <summary>
    /// Rehidratación desde persistencia: el estado ya es válido y no se revalida. La creación de negocio va
    /// por el factory. Las colecciones se copian: quien entrega el estado no puede cambiarlas después por la
    /// espalda del agregado.
    /// </summary>
    public Product(ProductState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Id = state.Id;
        Sku = state.Sku;
        Name = state.Name;
        Notes = state.Notes;
        Price = state.Price;
        ApiToken = state.ApiToken;
        Status = state.Status;
        LockVersion = state.LockVersion;
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; }

    public string Name { get; private set; }

    public string? Notes { get; private set; }

    public Money Price { get; private set; }

    /// <summary>Token de sincronización con el proveedor; nunca se expone.</summary>
    public string? ApiToken { get; private set; }

    public ProductStatus Status { get; private set; }

    /// <summary>Versión de concurrencia optimista; la gestiona la persistencia. Null hasta la primera escritura.</summary>
    public long? LockVersion { get; private set; }

    // Guarda interna del lifecycle: la llaman los métodos semánticos, nunca un handler.
    private void TransitionTo(ProductStatus target)
    {
        var current = Status;
        if (!Transitions.TryGetValue(current, out var allowed) || !allowed.Contains(target))
        {
            throw new InvalidStateTransitionException(current.ToString(), target.ToString());
        }
        Status = target;
    }
}
