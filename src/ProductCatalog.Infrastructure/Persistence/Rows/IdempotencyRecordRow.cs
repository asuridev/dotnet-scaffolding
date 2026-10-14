namespace ProductCatalog.Infrastructure.Persistence.Rows;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Petición ya atendida: un par (ámbito, clave de idempotencia). La MISMA tabla que los servidores de keel-spring y
/// keel-nest del diseño (keel-core/gen/request-idempotency.js): la clave primaria compuesta arbitra la carrera entre dos
/// peticiones simultáneas, y expires_at se guarda calculada para que cada fila conserve la ventana con la que se
/// registró. El índice sobre expires_at es el de la purga.
/// </summary>
public sealed class IdempotencyRecordRow
{
    public string OperationScope { get; set; } = null!;

    public string IdempotencyKey { get; set; } = null!;

    public string Signature { get; set; } = null!;

    public string? ResourceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>El mapeo de IdempotencyRecordRow a la tabla idempotency_record.</summary>
public sealed class IdempotencyRecordRowConfiguration : IEntityTypeConfiguration<IdempotencyRecordRow>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecordRow> row)
    {
        row.ToTable("idempotency_record");
        row.HasKey(r => new { r.OperationScope, r.IdempotencyKey }).HasName("idempotency_record_pkey");
        row.Property(r => r.OperationScope).HasColumnName("operation_scope").HasMaxLength(128).IsRequired();
        row.Property(r => r.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255).IsRequired();
        row.Property(r => r.Signature).HasColumnName("signature").HasMaxLength(128).IsRequired();
        row.Property(r => r.ResourceId).HasColumnName("resource_id").HasMaxLength(255).IsRequired(false);
        row.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        row.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        row.HasIndex(r => r.ExpiresAt, "ix_idempotency_record_expires_at").HasDatabaseName("ix_idempotency_record_expires_at");
    }
}
