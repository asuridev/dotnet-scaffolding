namespace ProductCatalog.Infrastructure.Persistence;

using Npgsql;
using ProductCatalog.Infrastructure.Config;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>Cómo se gobierna el esquema al arrancar.</summary>
public enum SchemaMode
{
    /// <summary>Nada: el perfil no tiene base o el esquema lo pone otro.</summary>
    None,

    /// <summary>EF crea el esquema desde el modelo si la base está vacía (solo local).</summary>
    Create,

    /// <summary>Se aplican las migraciones pendientes del proyecto Infrastructure.</summary>
    Migrate
}

/// <summary>
/// La configuración de la base del perfil activo, ya resuelta y validada. Una URL que no es de PostgreSQL no deja
/// arrancar: conectaría a otra cosa o fallaría en la primera petición, lejos de la causa.
/// </summary>
public sealed partial record DatabaseSettings(bool Enabled, string? ConnectionString, TimeSpan TransactionTimeout, SchemaMode Schema)
{
    /// <summary>Sin base de datos: el perfil test.</summary>
    public static DatabaseSettings Disabled { get; } = new(false, null, TimeSpan.FromSeconds(30), SchemaMode.None);

    public static DatabaseSettings From(ServiceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var url = configuration.Get("database:url");
        var timeout = ServiceConfiguration.ToDuration(configuration.Get("database:transaction-timeout") ?? "30s");
        if (string.Equals(configuration.Get("database:enabled"), "false", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(url))
        {
            return Disabled with { TransactionTimeout = timeout };
        }
        var match = DatabaseUrl().Match(url.Trim());
        if (!match.Success)
        {
            throw new InvalidOperationException($"database.url no es una URL de PostgreSQL: '{url}' (usa jdbc:postgresql://host:puerto/base)");
        }
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = match.Groups["host"].Value,
            Port = match.Groups["port"].Success ? int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture) : 5432,
            Database = Uri.UnescapeDataString(match.Groups["database"].Value),
            Username = configuration.Get("database:username"),
            Password = configuration.Get("database:password"),
            ApplicationName = configuration.Get("database:pool:name") ?? configuration.ApplicationName,
            MaxPoolSize = Positive(configuration.Get("database:pool:max-size"), 10),
            // Npgsql mide en segundos: la espera por una conexión libre del pool y la vida máxima de una conexión.
            Timeout = Seconds(Positive(configuration.Get("database:pool:connection-timeout-ms"), 5000)),
            ConnectionLifetime = Seconds(Positive(configuration.Get("database:pool:max-lifetime-ms"), 1800000))
        };
        var schema = configuration.Get("database:schema") switch
        {
            "create" => SchemaMode.Create,
            "migrate" => SchemaMode.Migrate,
            _ => SchemaMode.None
        };
        return new DatabaseSettings(true, builder.ConnectionString, timeout, schema);
    }

    private static int Positive(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : fallback;

    private static int Seconds(int milliseconds) => Math.Max(1, (milliseconds + 999) / 1000);

    /// <summary><c>jdbc:postgresql://host:puerto/base?…</c> o <c>postgresql://host:puerto/base</c>. Los parámetros de la URL no se trasladan: los que importan tienen su propia variable.</summary>
    [GeneratedRegex(@"^(?:jdbc:)?(?:postgresql|postgres)://(?<host>[^/:?]+)(?::(?<port>\d+))?/(?<database>[^?;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseUrl();
}
