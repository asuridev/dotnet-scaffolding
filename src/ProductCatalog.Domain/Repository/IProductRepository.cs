namespace ProductCatalog.Domain.Repository;

using ProductCatalog.Domain.Aggregate;

/// <summary>
/// Puerto de persistencia del agregado Product. El dominio solo ve esto; el adaptador vive en
/// Infrastructure/Persistence/Repositories.
/// </summary>
public interface IProductRepository
{
    Task<Product?> FindById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>El agregado por su clave natural (sku), o null.</summary>
    Task<Product?> FindBySku(string sku, CancellationToken cancellationToken = default);

    /// <summary>Una página de agregados, en el orden pedido y con el id como desempate.</summary>
    Task<Page<Product>> List(Pageable pageable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el agregado entero (sus entidades internas y sus listas) y devuelve lo guardado. Con bloqueo
    /// optimista, una versión obsoleta sale como conflicto de concurrencia.
    /// </summary>
    Task<Product> Save(Product entity, CancellationToken cancellationToken = default);

    Task DeleteById(Guid id, CancellationToken cancellationToken = default);
}
