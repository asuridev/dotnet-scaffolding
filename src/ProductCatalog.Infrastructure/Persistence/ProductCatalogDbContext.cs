namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Persistence.Rows;

/// <summary>
/// El esquema del servicio en EF Core: una tabla por entidad persistida y por lista, con los nombres de
/// keel-core/gen/relational.js (los mismos que keel-spring y keel-nest). Se registra con un ciclo de vida por
/// ÁMBITO —uno por petición, por mensaje o por pasada del reloj—, y es la unidad de trabajo de la transacción del
/// caso de uso: el mediator la abre y los adaptadores la comparten sin verla.
/// </summary>
public sealed class ProductCatalogDbContext(DbContextOptions<ProductCatalogDbContext> options) : DbContext(options)
{
    public DbSet<ProductRow> Products => Set<ProductRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new ProductRowConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyRecordRowConfiguration());
    }
}
