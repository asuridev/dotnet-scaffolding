namespace ProductCatalog.UnitTests;

using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Persistence;
using Xunit;

public sealed class PersistenceModelTests
{
    private static readonly Lazy<string> Ddl = new(() =>
    {
        // Sin conectar: el DDL se genera del modelo, y para construirlo no hace falta ninguna base.
        var options = new DbContextOptionsBuilder<ProductCatalogDbContext>().UseNpgsql("Host=unused").Options;
        using var db = new ProductCatalogDbContext(options);
        return db.Database.GenerateCreateScript();
    });

    [Fact]
    public void ElModeloSeConstruyeYGeneraSuEsquema() => Assert.Contains("CREATE TABLE", Ddl.Value, StringComparison.Ordinal);

    /// <summary>
    /// Ningún nombre puesto por EF por su cuenta: una clave, una FK o un índice de convención no casaría con el
    /// traductor de errores ni con el baseline de los otros dos servidores.
    /// </summary>
    [Fact]
    public void NingunNombreLoPoneEfPorSuCuenta() => Assert.DoesNotMatch(@"\b(PK|FK|IX|AK)_\w+", Ddl.Value);

    [Theory]
    [InlineData("idempotency_record")]
    [InlineData("idempotency_record_pkey")]
    [InlineData("idx_products_status")]
    [InlineData("ix_idempotency_record_expires_at")]
    [InlineData("products")]
    [InlineData("uk_products_natural")]
    public void ElEsquemaLlevaCadaNombreDelDiseño(string name) => Assert.Matches($@"\b{name}\b", Ddl.Value);
}
