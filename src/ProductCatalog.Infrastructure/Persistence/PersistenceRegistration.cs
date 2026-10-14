namespace ProductCatalog.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProductCatalog.Domain.Idempotency;
using ProductCatalog.Domain.Repository;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Infrastructure.Persistence.Repositories;
using ProductCatalog.Infrastructure.UseCase;
using System.Data.Common;

/// <summary>
/// La persistencia relacional en el contenedor: el DbContext del perfil (uno por ámbito), la transacción del caso de
/// uso y un adaptador por puerto. El ÚNICO sitio que registra los repositorios.
/// </summary>
public static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, DatabaseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        services.AddSingleton(settings);
        services.AddDbContext<ProductCatalogDbContext>(options => Configure(options, settings));
        services.AddScoped<PersistenceTransactions>();
        // El mediator abre la transacción de cada caso de uso con ESTA implementación.
        services.Replace(ServiceDescriptor.Scoped<IUseCaseTransactions>(provider => provider.GetRequiredService<PersistenceTransactions>()));
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        return services;
    }

    /// <summary>
    /// Pone el esquema al ARRANCAR, antes de atender nada: una base inalcanzable o un esquema que no casa tumban el
    /// arranque, no la primera petición. En local lo crea EF; fuera, se aplican las migraciones.
    /// </summary>
    public static async Task InitializeDatabase(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var settings = services.GetRequiredService<DatabaseSettings>();
        if (!settings.Enabled || settings.Schema == SchemaMode.None)
        {
            return;
        }
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();
        if (settings.Schema == SchemaMode.Create)
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
    }

    private static void Configure(DbContextOptionsBuilder options, DatabaseSettings settings)
    {
        if (!settings.Enabled)
        {
            // Sin base en este perfil: el contexto se construye (los handlers se resuelven) y falla al conectar,
            // diciéndolo.
            options.UseNpgsql("Host=database-disabled").AddInterceptors(new DisabledDatabase());
            return;
        }
        options.UseNpgsql(
            settings.ConnectionString,
            npgsql => npgsql
                // Las colecciones del agregado en consultas aparte: con un JOIN, cada hija multiplicaría las filas.
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                .MigrationsHistoryTable("ef_migrations_history"));
    }

    /// <summary>El perfil sin base de datos: abrir una conexión es un error que lo dice.</summary>
    private sealed class DisabledDatabase : DbConnectionInterceptor
    {
        private const string Reason = "Sin base de datos en este perfil (database.enabled: false): el perfil test no la tiene.";

        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            throw new InvalidOperationException(Reason);

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(Reason);
    }
}
