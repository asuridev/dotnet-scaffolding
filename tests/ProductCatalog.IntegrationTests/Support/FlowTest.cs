namespace ProductCatalog.IntegrationTests.Support;

using System.Collections.Concurrent;
using Xunit;

/// <summary>
/// Base de las pruebas de flujo (<c>&lt;Flujo&gt;Flow.cs</c>) que ejecutan los escenarios FL-* de
/// specs/validation-scenarios.md contra el servidor REAL —un proceso aparte, escuchando en un puerto libre bajo el
/// perfil <c>local</c>— y la infraestructura de infra/docker-compose.yaml.
/// </summary>
/// <remarks>
/// Uso, una clase por flujo:
/// <code>
/// public sealed class ProductCreationFlow : FlowTest
/// {
///     [Scenario("FL-PRD-001-A: crea el producto")]
///     public async Task CreaElProducto()
///     {
///         var response = await Post($"{RouteBase}/products", new { sku = "A-1" });
///         Assert.Equal(201, response.Status);
///     }
/// }
/// </code>
/// <para><b>Caja negra.</b> El proyecto no ve ningún tipo del servicio: los escenarios se expresan con HTTP y JSON,
/// que es lo que exige la equivalencia con los servidores de keel-spring y keel-nest del mismo diseño.</para>
/// <para><b>El título de cada caso empieza por su id</b> y dos puntos (<c>FL-PRD-001-A: …</c>): de ahí sale la
/// matriz de infra/score-scenarios.sh, y el nombre del volcado de su evidencia. Los casos de un flujo se ejecutan
/// en el ORDEN en que están escritos (<see cref="ScenarioAttribute"/>): dentro de un flujo, un escenario usa lo que
/// dejó el anterior.</para>
/// <para>Al empezar cada flujo se arranca el servidor (una vez por suite), se resetea el estado y se
/// ejecuta <see cref="GivenAsync"/>. Si eso falla, los casos del flujo salen OMITIDOS y se vuelca
/// <c>build/keel-failures/&lt;Flujo&gt;-init.json</c>: infra/score-scenarios.sh lo lee para decir que el arnés —no el
/// servidor— está roto.</para>
/// <para>Lo generó keel-dotnet build: no se edita. Lo que un flujo necesite y no esté aquí se escribe en la propia
/// clase del flujo.</para>
/// </remarks>
public abstract class FlowTest : IAsyncLifetime
{
    /// <summary>Prefijo de todas las rutas del servicio (basePath del diseño + versión).</summary>
    public const string RouteBase = "/api/v1";

    // El arranque de cada flujo: una vez por clase (xUnit crea una instancia por caso). Los flujos corren en serie.
    private static readonly ConcurrentDictionary<Type, string?> Prepared = new();

    /// <summary>
    /// El Given común a todo el flujo, después del reset: lo que el flujo necesita antes de su primer escenario y
    /// ninguna operación del diseño fabrica. Se ejecuta una vez por flujo.
    /// </summary>
    protected virtual Task GivenAsync() => Task.CompletedTask;

    public async ValueTask InitializeAsync()
    {
        FailureCapture.Clear();
        var flow = GetType();
        if (Prepared.TryGetValue(flow, out var failure))
        {
            if (failure is not null) Assert.Skip($"El flujo no llegó a arrancar: {failure}");
            return;
        }
        try
        {
            await FlowServer.EnsureStartedAsync(TestContext.Current.CancellationToken);
            Harness.ResetState();
            await GivenAsync();
            Prepared[flow] = null;
        }
        catch (Exception error)
        {
            Prepared[flow] = error.Message;
            FailureCapture.DumpInit(flow.Name, error);
            Assert.Skip($"El flujo no llegó a arrancar: {error.Message}");
        }
        finally
        {
            FailureCapture.Clear();
        }
    }

    public ValueTask DisposeAsync()
    {
        var state = TestContext.Current.TestState;
        if (state?.Result == TestResult.Failed) FailureCapture.DumpFailure(TestContext.Current.Test?.TestDisplayName ?? "", GetType().Name, state);
        FailureCapture.Clear();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>URL base del servidor arrancado (http://127.0.0.1:&lt;puerto&gt;).</summary>
    protected static string BaseUrl => FlowServer.BaseUrl;

    protected static Task<FlowResponse> Get(string path, Dictionary<string, string>? headers = null) => Exchange("GET", path, null, headers);

    /// <summary><paramref name="body"/> es un objeto (se serializa) o el JSON ya escrito, tal cual viaja.</summary>
    protected static Task<FlowResponse> Post(string path, object? body = null, Dictionary<string, string>? headers = null) => Exchange("POST", path, body, headers);

    protected static Task<FlowResponse> Put(string path, object? body = null, Dictionary<string, string>? headers = null) => Exchange("PUT", path, body, headers);

    protected static Task<FlowResponse> Patch(string path, object? body = null, Dictionary<string, string>? headers = null) => Exchange("PATCH", path, body, headers);

    protected static Task<FlowResponse> Delete(string path, Dictionary<string, string>? headers = null) => Exchange("DELETE", path, null, headers);

    /// <summary>Una petición HTTP de verdad. Nunca lanza por un 4xx/5xx: el status es una aserción del escenario.</summary>
    protected static Task<FlowResponse> Exchange(string method, string path, object? body = null, Dictionary<string, string>? headers = null) =>
        Harness.SendAsync(method, path, body, headers, TestContext.Current.CancellationToken);

    /// <summary>
    /// Deja el estado como recién arrancado: exactamente lo que enumera <c>infra/reset-db.sh</c>. La base ya lo
    /// llama al empezar cada flujo; llamarlo a mano solo tiene sentido para un escenario que tenga que empezar de cero.
    /// </summary>
    protected static void ResetState() => Harness.ResetState();

    /// <summary>
    /// Ejecuta una sentencia contra la base de prueba (PostgreSQL) y devuelve su salida en crudo. Es para lo que
    /// no se ve por HTTP: que una escritura llegó de verdad al almacén, o la precondición de un escenario que ninguna
    /// operación del diseño puede fabricar. Si el servicio lo expone por su API, se comprueba por ahí.
    /// </summary>
    protected static string Db(string sql) => Harness.Db(sql);

    /// <summary>
    /// Ejecuta un comando y devuelve su salida; lanza nombrando sus últimas líneas si sale mal. Los argumentos van
    /// siempre como lista, nunca armados para un shell.
    /// </summary>
    protected static string Run(string command, IEnumerable<string> arguments, string hint = "") => Harness.Run(command, arguments, hint);

    /// <summary>Espera hasta que <paramref name="condition"/> se cumpla o se agote el plazo; falla con <paramref name="message"/> si no llega.</summary>
    protected static Task Eventually(Func<Task<bool>> condition, TimeSpan? timeout = null, string message = "la condición no se cumplió a tiempo") =>
        Harness.EventuallyAsync(condition, timeout ?? TimeSpan.FromSeconds(10), message, TestContext.Current.CancellationToken);

    /// <summary>
    /// Compara el cuerpo con el JSON esperado, ESTRICTO: las mismas claves, los números por su texto exacto
    /// (<c>2.50</c> no es <c>2.5</c>: la escala es contrato) y las listas en orden. Un valor <c>"&lt;uuid&gt;"</c>,
    /// <c>"&lt;instant&gt;"</c> o <c>"&lt;any&gt;"</c> afirma solo la forma de lo que genera el servidor.
    /// </summary>
    protected static void AssertJson(string expected, FlowResponse response) => JsonShape.AssertMatches(expected, response.Json());
}
