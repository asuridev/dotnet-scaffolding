namespace ProductCatalog.UnitTests;

using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// El servidor en memoria, bajo el perfil <c>test</c>: el que las pruebas arrancan sin infraestructura.
/// </summary>
public sealed class InMemoryServer : WebApplicationFactory<Program>
{
    public InMemoryServer()
    {
        // El arranque lee el perfil de PROFILE antes de crear la aplicación.
        Environment.SetEnvironmentVariable("PROFILE", "test");
    }
}
