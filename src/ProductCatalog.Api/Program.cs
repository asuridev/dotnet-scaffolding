using ProductCatalog.Application.Support;
using ProductCatalog.Infrastructure.Config;
using ProductCatalog.Infrastructure.Health;
using ProductCatalog.Infrastructure.UseCase;
using ProductCatalog.Infrastructure.Persistence;
using ProductCatalog.Api.Rest;

// La configuración del perfil activo (PROFILE), cargada y validada antes de crear la aplicación: una
// variable obligatoria sin valor no deja arrancar, y se listan todas las que faltan.
var configuration = ServiceConfiguration.Load();

var builder = WebApplication.CreateBuilder(args);
// El resto de la aplicación lee la configuración ya resuelta por la vía estándar de ASP.NET Core.
builder.Configuration.AddInMemoryCollection(configuration.Values);
builder.WebHost.UseUrls($"http://{configuration.Server.Address}:{configuration.Server.Port}");
builder.Services.AddSingleton(configuration);
builder.Services.AddSingleton<GracefulShutdown>();
// El margen del apagado ordenado: lo que el host espera a las peticiones en vuelo tras la señal.
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = configuration.Server.ShutdownTimeout);
// El contrato del cable: todo cuerpo JSON que la API lee o escribe pasa por estas opciones (WireJson).
builder.Services.ConfigureHttpJsonOptions(options => WireJson.Configure(options.SerializerOptions));
// Los casos de uso del diseño: el mediator, los handlers y los mappers (UseCaseRegistration, generado).
builder.Services.AddUseCases();
// La persistencia relacional del perfil: el DbContext, la transacción del caso de uso y los repositorios.
builder.Services.AddPersistence(DatabaseSettings.From(configuration));

var app = builder.Build();
app.Services.GetRequiredService<GracefulShutdown>().Watch(app.Lifetime, app.Logger, configuration.Server.ShutdownTimeout);
await app.Services.InitializeDatabase();
app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<ApiErrorMiddleware>();
app.UseMiddleware<IdempotencyKeyMiddleware>();
app.MapHealthEndpoints();
app.MapApiRoutes();
app.Run();

/// <summary>Visible para las pruebas, que levantan el servidor en memoria (WebApplicationFactory).</summary>
public partial class Program;
