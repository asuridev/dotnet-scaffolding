namespace ProductCatalog.IntegrationTests.Support;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Xunit;

/// <summary>
/// El servidor del servicio, arrancado UNA vez por suite como un proceso aparte: el mismo ejecutable que se
/// despliega (<c>src/ProductCatalog.Api</c>), bajo el perfil <c>local</c> (o el de <c>PROFILE</c>), escuchando en
/// 127.0.0.1 en un puerto libre. Su salida va a <c>build/keel-scenarios/server.log</c>: es la evidencia de un
/// arranque que no llega o de un 500.
/// </summary>
internal static class FlowServer
{
    private const string ApiProject = "ProductCatalog.Api";
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(90);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly object LogLock = new();
    private static Process? process;
    private static StreamWriter? log;
    private static string? failure;

    public static string BaseUrl { get; private set; } = "";

    public static string LogPath => Path.Combine(Harness.Root, "build", "keel-scenarios", "server.log");

    public static async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (failure is not null) throw new InvalidOperationException(failure);
            if (process is { HasExited: false }) return;
            if (process is { HasExited: true })
            {
                failure = $"el servidor terminó a mitad de la suite (código {process.ExitCode}). Log: {LogPath}\n{LogTail()}";
                throw new InvalidOperationException(failure);
            }
            try
            {
                await StartAsync(cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                failure = error.Message;
                Stop();
                throw;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task StartAsync(CancellationToken cancellationToken)
    {
        var dll = ApiAssembly();
        if (!File.Exists(dll))
        {
            throw new InvalidOperationException($"No está compilado el servidor ({dll}): compila la solución antes (dotnet build).");
        }
        var port = FreePort();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Harness.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(dll);
        start.Environment["PROFILE"] = Environment.GetEnvironmentVariable("PROFILE") ?? "local";
        start.Environment["SERVER_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["SERVER_ADDRESS"] = "127.0.0.1";
        start.Environment["DOTNET_NOLOGO"] = "1";

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        log = new StreamWriter(LogPath, append: false) { AutoFlush = true };
        process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => Write(line.Data);
        process.ErrorDataReceived += (_, line) => Write(line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();

        BaseUrl = $"http://127.0.0.1:{port}";
        var until = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < until)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"el servidor no arrancó (salió con código {process.ExitCode}). Log: {LogPath}\n{LogTail()}");
            }
            try
            {
                using var response = await Probe.GetAsync($"{BaseUrl}/livez", cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException)
            {
                // Todavía no escucha.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // La sonda agotó su plazo: se reintenta.
            }
            await Task.Delay(200, cancellationToken);
        }
        throw new InvalidOperationException($"el servidor no respondió /livez en {StartTimeout.TotalSeconds} s. Log: {LogPath}\n{LogTail()}");
    }

    /// <summary>Para el servidor (y lo que haya lanzado). Lo llama el final de la suite y la salida del proceso.</summary>
    public static void Stop()
    {
        try
        {
            if (process is { HasExited: false }) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Ya había terminado.
        }
        lock (LogLock)
        {
            log?.Dispose();
            log = null;
        }
    }

    /// <summary>Las últimas líneas del log del servidor, para el mensaje de un arranque que falla.</summary>
    public static string LogTail(int lines = 20)
    {
        lock (LogLock)
        {
            log?.Flush();
            if (!File.Exists(LogPath)) return "(sin log)";
            using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var all = reader.ReadToEnd().Split('\n');
            return string.Join('\n', all.TakeLast(lines)).TrimEnd();
        }
    }

    private static void Write(string? line)
    {
        if (line is null) return;
        lock (LogLock) log?.WriteLine(line);
    }

    /// <summary>
    /// El ejecutable del Api, en la configuración con la que se compiló este proyecto (Debug o Release): el
    /// proyecto de flujos ordena su compilación sin ver sus tipos.
    /// </summary>
    private static string ApiAssembly()
    {
        var configuration = typeof(FlowServer).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == "Configuration")?.Value ?? "Debug";
        return Path.Combine(Harness.Root, "src", ApiProject, "bin", configuration, "net10.0", $"{ApiProject}.dll");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

/// <summary>Para el servidor al terminar la suite (xUnit libera los fixtures de ensamblado al final).</summary>
public sealed class FlowServerShutdown : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        FlowServer.Stop();
        return ValueTask.CompletedTask;
    }
}
