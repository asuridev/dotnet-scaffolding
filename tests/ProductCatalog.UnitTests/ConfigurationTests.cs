namespace ProductCatalog.UnitTests;

using ProductCatalog.Infrastructure.Config;
using Xunit;

public sealed class ConfigurationTests : IDisposable
{
    // Lo que el cargador exige siempre (el puerto del servidor), para las pruebas que miran otra cosa.
    private const string BaseYaml = "server:\n  port: 8080\n";

    private readonly DirectoryInfo root = Directory.CreateTempSubdirectory("keel-config-");

    public void Dispose() => root.Delete(recursive: true);

    private void Write(string relativePath, string yaml)
    {
        var file = Path.Combine(root.FullName, "config", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, yaml);
    }

    private ServiceConfiguration Load(params (string Name, string Value)[] variables)
    {
        var environment = variables.ToDictionary(variable => variable.Name, variable => variable.Value);
        return ServiceConfiguration.Load(name => environment.GetValueOrDefault(name), root.FullName);
    }

    [Fact]
    public void ElDefaultValeSiLaVariableNoEstaYLaVariableGanaSiEsta()
    {
        Write("application.yaml", "server:\n  port: ${SERVER_PORT:8080}\n");
        Assert.Equal(8080, Load().Server.Port);
        Assert.Equal(9090, Load(("SERVER_PORT", "9090")).Server.Port);
    }

    [Fact]
    public void UnaVariableObligatoriaSinValorNoDejaArrancarYSeListanTodas()
    {
        Write("application.yaml", BaseYaml + "db:\n  url: ${DB_URL}\n  user: ${DB_USER}\n");
        var error = Assert.Throws<InvalidOperationException>(() => Load());
        Assert.Contains("faltan 2 variable(s)", error.Message, StringComparison.Ordinal);
        Assert.Contains("DB_URL (db.url)", error.Message, StringComparison.Ordinal);
        Assert.Contains("DB_USER (db.user)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaVariableVaciaCuentaComoAusente()
    {
        Write("application.yaml", BaseYaml + "a: ${A:por-defecto}\n");
        Assert.Equal("por-defecto", Load(("A", "")).Get("a"));
    }

    [Fact]
    public void ElDefaultPuedeLlevarOtroPlaceholder()
    {
        Write("application.yaml", BaseYaml + "id: ${SERVICE_INSTANCE_ID:${HOSTNAME:unknown}}\n");
        Assert.Equal("unknown", Load().Get("id"));
        Assert.Equal("host-1", Load(("HOSTNAME", "host-1")).Get("id"));
    }

    [Fact]
    public void LosFragmentosDelPerfilPisanLoComun()
    {
        Write("application.yaml", "server:\n  port: 8080\n  address: 0.0.0.0\n");
        Write("parameters/develop/server.yaml", "server:\n  port: 9000\n");
        var configuration = Load(("PROFILE", "develop"));
        Assert.Equal(9000, configuration.Server.Port);
        Assert.Equal("0.0.0.0", configuration.Server.Address);
        Assert.Equal(new[] { "develop" }, configuration.Profiles);
    }

    [Theory]
    [InlineData("30s", 30_000)]
    [InlineData("500ms", 500)]
    [InlineData("2m", 120_000)]
    [InlineData("1h", 3_600_000)]
    [InlineData("250", 250)]
    public void LasDuracionesSeLeenConSuUnidad(string text, long milliseconds) =>
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), ServiceConfiguration.ToDuration(text));
}
