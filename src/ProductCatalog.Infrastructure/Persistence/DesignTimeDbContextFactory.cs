namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// El ProductCatalogDbContext para las herramientas de diseño (<c>dotnet ef migrations add</c>), sin pasar por Program.cs: no hace
/// falta configuración del perfil ni base de datos para comparar el modelo con la última migración. El baseline lo
/// crea el pase de calidad: <c>dotnet ef migrations add Baseline --project src/ProductCatalog.Infrastructure</c>.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductCatalogDbContext>
{
    public ProductCatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time", npgsql => npgsql.MigrationsHistoryTable("ef_migrations_history"))
            .Options;
        return new ProductCatalogDbContext(options);
    }
}
