namespace ProductCatalog.Infrastructure.Persistence.Rows;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductCatalog.Domain.Enums;
using ProductCatalog.Infrastructure.Persistence;

/// <summary>Tabla de Product (raíz del agregado). El dominio no la conoce: la mapea su adaptador.</summary>
public sealed class ProductRow
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Notes { get; set; }

    /// <summary>Money.amount aplanado.</summary>
    public decimal PriceAmount { get; set; }

    /// <summary>Money.currency aplanado.</summary>
    public string PriceCurrency { get; set; } = null!;

    /// <summary>Token de sincronización con el proveedor; nunca se expone.</summary>
    public string? ApiToken { get; set; }

    public ProductStatus Status { get; set; }

    /// <summary>Versión de concurrencia optimista: la sube el adaptador al guardar, nunca a mano.</summary>
    public long? LockVersion { get; set; }

    /// <summary>Auditoría (persistence.audit: all): la estampa el adaptador al guardar.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Auditoría (persistence.audit: all): la estampa el adaptador al guardar.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>El mapeo de ProductRow a la tabla products, con los nombres de keel-core/gen/relational.js.</summary>
public sealed class ProductRowConfiguration : IEntityTypeConfiguration<ProductRow>
{
    public void Configure(EntityTypeBuilder<ProductRow> row)
    {
        row.ToTable("products");
        row.HasKey(r => r.Id).HasName("products_pkey");
        row.Property(r => r.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        row.Property(r => r.Sku).HasColumnName("sku").HasMaxLength(8).IsRequired();
        row.Property(r => r.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        row.Property(r => r.Notes).HasColumnName("notes").HasColumnType("text").IsRequired(false);
        row.Property(r => r.PriceAmount).HasColumnName("price_amount").HasPrecision(19, 2).IsRequired();
        row.Property(r => r.PriceCurrency).HasColumnName("price_currency").HasMaxLength(3).IsRequired();
        row.Property(r => r.ApiToken).HasColumnName("api_token").HasMaxLength(255).IsRequired(false);
        row.Property(r => r.Status).HasColumnName("status").HasConversion(EnumColumns.ProductStatusColumn).HasMaxLength(255).IsRequired();
        row.Property(r => r.LockVersion).HasColumnName("lock_version").IsRequired(false).IsConcurrencyToken();
        row.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        row.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        row.HasIndex(r => r.Sku, "uk_products_natural").IsUnique().HasDatabaseName("uk_products_natural");
        row.HasIndex(r => r.Status, "idx_products_status").HasDatabaseName("idx_products_status");
    }
}
