namespace ProductCatalog.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using ProductCatalog.Domain.Aggregate;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Domain.ValueObject;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.Persistence.Rows;
using ProductCatalog.Infrastructure.UseCase;
using System.Linq.Expressions;

/// <summary>
/// Adaptador del puerto IProductRepository sobre EF Core. Usa el DbContext del ámbito, el de la transacción que abrió
/// el UseCaseMediator; sin ella, cada lectura va sola y cada escritura abre la suya.
/// </summary>
public sealed class ProductRepository(ProductCatalogDbContext db, PersistenceTransactions transactions) : IProductRepository
{
    // El agregado entero: lo que se carga con la raíz y se borra con ella. Las colecciones van en consultas
    // aparte (QuerySplittingBehavior.SplitQuery): con un JOIN, cada hija multiplicaría las filas de la página.
    private IQueryable<ProductRow> Graph() => db.Set<ProductRow>();

    /// <summary>
    /// Las propiedades por las que se puede ordenar, con el nombre del contrato: las de la fila, como las de la entidad
    /// JPA que ordena keel-spring. Una que no existe es un error, como la PropertyReferenceException de Spring Data.
    /// </summary>
    private static readonly Dictionary<string, Expression<Func<ProductRow, object?>>> Sortable = new(StringComparer.Ordinal)
    {
        ["id"] = r => r.Id,
        ["sku"] = r => r.Sku,
        ["name"] = r => r.Name,
        ["notes"] = r => r.Notes,
        ["priceAmount"] = r => r.PriceAmount,
        ["priceCurrency"] = r => r.PriceCurrency,
        ["apiToken"] = r => r.ApiToken,
        ["status"] = r => r.Status,
        ["createdAt"] = r => r.CreatedAt,
        ["updatedAt"] = r => r.UpdatedAt,
    };

    /// <summary>
    /// El orden pedido más el id como último criterio si no está ya: sin desempate, dos páginas consecutivas pueden
    /// repetir una fila y omitir otra, porque cuando el ORDER BY empata el motor no promete un orden estable.
    /// </summary>
    private static IQueryable<ProductRow> Ordered(IQueryable<ProductRow> query, IReadOnlyList<SortOrder> sort)
    {
        IOrderedQueryable<ProductRow>? ordered = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var order in sort)
        {
            if (!Sortable.TryGetValue(order.Property, out var key))
            {
                throw new InvalidOperationException($"No existe la propiedad '{order.Property}' para ordenar Product");
            }
            if (!seen.Add(order.Property))
            {
                continue;
            }
            var descending = order.Direction == SortDirection.Desc;
            ordered = ordered is null
                ? (descending ? query.OrderByDescending(key) : query.OrderBy(key))
                : (descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
        }
        if (seen.Contains("id"))
        {
            return ordered ?? query;
        }
        return ordered is null ? query.OrderBy(r => r.Id) : ordered.ThenBy(r => r.Id);
    }

    public async Task<Product?> FindById(Guid id, CancellationToken cancellationToken = default)
    {
        var found = await Graph().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return found is null ? null : ToDomain(found);
    }

    public async Task<Product?> FindBySku(string sku, CancellationToken cancellationToken = default)
    {
        var found = await Graph().AsNoTracking().SingleOrDefaultAsync(r => r.Sku == sku, cancellationToken);
        return found is null ? null : ToDomain(found);
    }

    public async Task<Page<Product>> List(Pageable pageable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageable);
        var total = await db.Set<ProductRow>().LongCountAsync(cancellationToken);
        var offset = (int)Math.Min((long)pageable.Page * pageable.Size, int.MaxValue);
        var rows = await Ordered(Graph().AsNoTracking(), pageable.Sort).Skip(offset).Take(pageable.Size).ToListAsync(cancellationToken);
        var pages = pageable.Size == 0 ? 1 : (int)Math.Ceiling(total / (double)pageable.Size);
        return new Page<Product>([.. rows.Select(ToDomain)], pageable.Page, pageable.Size, total, pages);
    }

    public async Task<Product> Save(Product entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await transactions.InTransaction(
            async () =>
            {
                ProductRow row;
                var expected = entity.LockVersion;
                if (expected is null)
                {
                    // Agregado nuevo: nace con la versión 0, como el @Version de JPA al persistir.
                    row = new ProductRow { Id = entity.Id };
                    Apply(row, entity);
                    row.LockVersion = 0;
                    db.Add(row);
                }
                else
                {
                    // La versión que el agregado LEYÓ es el valor original del token: EF escribe
                    // UPDATE … WHERE lock_version = @leída, y cero filas es otra escritura que ganó (o la borró).
                    row = await Graph().SingleOrDefaultAsync(r => r.Id == entity.Id, cancellationToken)
                        ?? throw new OptimisticLockConflict("Product", entity.Id.ToString());
                    Apply(row, entity);
                    db.Entry(row).Property(r => r.LockVersion).OriginalValue = expected;
                    row.LockVersion = expected + 1;
                }
                var now = DateTimeOffset.UtcNow;
                if (row.CreatedAt == default) { row.CreatedAt = now; }
                row.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                return ToDomain(row);
            },
            readOnly: false,
            cancellationToken);
    }

    public async Task DeleteById(Guid id, CancellationToken cancellationToken = default)
    {
        await transactions.InTransaction(
            async () =>
            {
                // Se carga y se borra el GRAFO: las filas de las hijas y de las listas van antes que la raíz.
                var found = await Graph().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
                if (found is not null)
                {
                    db.Remove(found);
                    await db.SaveChangesAsync(cancellationToken);
                }
                return true;
            },
            readOnly: false,
            cancellationToken);
    }

    // ─── Mapeo dominio ↔ fila ───────────────────────────────────────────────────────────────────

    private static Product ToDomain(ProductRow row) =>
        new(new ProductState
        {
            Id = row.Id,
            Sku = row.Sku,
            Name = row.Name,
            Notes = row.Notes,
            Price = new Money(amount: row.PriceAmount, currency: row.PriceCurrency),
            ApiToken = row.ApiToken,
            Status = row.Status,
            LockVersion = row.LockVersion,
        });

    private void Apply(ProductRow row, Product entity)
    {
        row.Sku = entity.Sku;
        row.Name = entity.Name;
        row.Notes = entity.Notes;
        row.PriceAmount = entity.Price.Amount;
        row.PriceCurrency = entity.Price.Currency;
        row.ApiToken = entity.ApiToken;
        row.Status = entity.Status;
    }
}
