namespace ProductCatalog.IntegrationTests.Support;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

/// <summary>La fontanería del arnés: la raíz del proyecto, el cliente HTTP, los comandos y la base de prueba.</summary>
internal static class Harness
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>La raíz del proyecto (donde está la solución): todo lo de infra/ y build/ cuelga de ahí.</summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.GetFiles("ProductCatalog.slnx").Length > 0) return dir.FullName;
        }
        throw new InvalidOperationException("No se encontró la raíz del proyecto (ProductCatalog.slnx) por encima de " + AppContext.BaseDirectory);
    }

    public static async Task<FlowResponse> SendAsync(string method, string path, object? body, IReadOnlyDictionary<string, string>? headers, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(FlowServer.BaseUrl)) throw new InvalidOperationException("El servidor no está arrancado: la prueba de flujo hereda de FlowTest.");
        var payload = body switch
        {
            null => null,
            string json => json,
            _ => JsonSerializer.Serialize(body)
        };
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(new Uri(FlowServer.BaseUrl), path));
        var sent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Accept"] = "application/json" };
        foreach (var (name, value) in headers ?? new Dictionary<string, string>()) sent[name] = value;
        string? contentType = null;
        foreach (var (name, value) in sent)
        {
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) contentType = value;
            else request.Headers.TryAddWithoutValidation(name, value);
        }
        if (payload is not null)
        {
            request.Content = new StringContent(payload, Encoding.UTF8);
            request.Content.Headers.Remove("Content-Type");
            request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType ?? "application/json");
            sent["Content-Type"] = contentType ?? "application/json";
        }
        using var response = await Http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var received = new Dictionary<string, string>();
        foreach (var header in response.Headers.Concat(response.Content.Headers)) received[header.Key.ToLowerInvariant()] = string.Join(", ", header.Value);
        FailureCapture.RecordExchange(new ExchangeRecord(method, path, sent, payload, (int)response.StatusCode, received, text));
        return new FlowResponse((int)response.StatusCode, received, text);
    }

    public static string Run(string command, IEnumerable<string> arguments, string hint = "")
    {
        var start = new ProcessStartInfo(command)
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var line = string.Join(' ', start.ArgumentList.Prepend(command));
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"no se pudo lanzar {command}");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var output = stdout + stderr.Result;
        FailureCapture.RecordProbe(new ProbeRecord(line, process.ExitCode, output));
        if (process.ExitCode != 0)
        {
            var tail = string.Join('\n', output.Trim().Split('\n').TakeLast(12));
            throw new InvalidOperationException($"{line} falló (código {process.ExitCode}). {hint}\n{(tail.Length > 0 ? tail : "(no escribió nada)")}");
        }
        return stdout;
    }

    public static async Task EventuallyAsync(Func<Task<bool>> condition, TimeSpan timeout, string message, CancellationToken cancellationToken)
    {
        var until = DateTime.UtcNow + timeout;
        while (true)
        {
            if (await condition()) return;
            if (DateTime.UtcNow > until) throw new TimeoutException(message);
            await Task.Delay(200, cancellationToken);
        }
    }

    /// <summary>
    /// El bash con el que se invocan los scripts de infra/. En Windows, <c>bash</c> a secas puede resolver al
    /// lanzador de WSL (System32 va antes que el PATH), un Linux aislado que no ve el PATH ni las variables de
    /// Windows: se busca el de Git for Windows, con override por <c>BASH_EXECUTABLE</c>.
    /// </summary>
    public static string BashExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("BASH_EXECUTABLE");
        if (!string.IsNullOrEmpty(configured)) return configured;
        if (OperatingSystem.IsWindows())
        {
            string?[] candidates =
            [
                Environment.GetEnvironmentVariable("ProgramFiles") is { } programFiles ? Path.Combine(programFiles, "Git", "bin", "bash.exe") : null,
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") is { } programFilesX86 ? Path.Combine(programFilesX86, "Git", "bin", "bash.exe") : null,
                Environment.GetEnvironmentVariable("LOCALAPPDATA") is { } localAppData ? Path.Combine(localAppData, "Programs", "Git", "bin", "bash.exe") : null
            ];
            foreach (var candidate in candidates) if (candidate is not null && File.Exists(candidate)) return candidate;
        }
        return "bash";
    }

    /// <summary>El runtime de contenedores: <c>CONTAINER_RUNTIME</c>, o docker si está, o podman.</summary>
    public static string ContainerRuntime()
    {
        var configured = Environment.GetEnvironmentVariable("CONTAINER_RUNTIME");
        if (!string.IsNullOrEmpty(configured)) return configured;
        try
        {
            using var docker = Process.Start(new ProcessStartInfo("docker", "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            docker?.WaitForExit();
            if (docker?.ExitCode == 0) return "docker";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // docker no está instalado.
        }
        return "podman";
    }

    public static void ResetState() => Run(BashExecutable(), ["infra/reset-db.sh"], "¿Está la infraestructura arriba (bash infra/up.sh)?");

    private const string DbContainer = "product-catalog-db";
    private static readonly string[] DbQueryArguments = ["env", "PGPASSWORD=changeme", "psql", "-h", "db", "-U", "product_catalog", "-d", "product_catalog", "-v", "ON_ERROR_STOP=1", "-q", "-t", "-A", "-c"];

    /// <summary>La sentencia viaja como UN argumento más, nunca armada para un shell.</summary>
    public static string Db(string sql) => Run(ContainerRuntime(), ["exec", DbContainer, .. DbQueryArguments, sql], "¿Está la base arriba (bash infra/up.sh)?");
}
