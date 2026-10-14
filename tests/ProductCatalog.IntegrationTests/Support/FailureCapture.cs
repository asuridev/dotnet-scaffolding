namespace ProductCatalog.IntegrationTests.Support;

using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

/// <summary>Un intercambio HTTP tal como viajó: lo que el árbitro lee sin reproducir el escenario a mano.</summary>
internal sealed record ExchangeRecord(
    string Method, string Path, IReadOnlyDictionary<string, string> RequestHeaders, string? RequestBody,
    int Status, IReadOnlyDictionary<string, string> ResponseHeaders, string ResponseBody);

/// <summary>Un comando que no es HTTP (la base, el reset): lo que se ejecutó, cómo salió y qué escribió.</summary>
internal sealed record ProbeRecord(string Command, int? ExitCode, string Output);

/// <summary>
/// La evidencia de cada fallo, volcada a <c>build/keel-failures/&lt;escenario&gt;.json</c> con el último
/// intercambio y el último sondeo: es lo que lee el árbitro junto al XML para decidir
/// <c>culprit: code | test | design</c>. Mismo formato que el de keel-spring y keel-nest.
/// </summary>
internal static class FailureCapture
{
    // Los flujos corren en serie y cada caso espera a sus peticiones: basta con lo último del proceso.
    private static ExchangeRecord? lastExchange;
    private static ProbeRecord? lastProbe;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Directory => Path.Combine(Harness.Root, "build", "keel-failures");

    public static void RecordExchange(ExchangeRecord exchange) => lastExchange = exchange;

    public static void RecordProbe(ProbeRecord probe) => lastProbe = probe;

    public static void Clear()
    {
        lastExchange = null;
        lastProbe = null;
    }

    /// <summary>El fallo de un caso: el id es lo que va delante de los dos puntos de su título.</summary>
    public static void DumpFailure(string displayName, string testClass, TestResultState state)
    {
        var scenario = displayName.Split(':')[0].Trim();
        var assertion = string.Join("\n", state.ExceptionMessages ?? []);
        Dump(scenario, displayName, testClass, null, assertion);
    }

    /// <summary>Un flujo que no llegó a arrancar: <c>&lt;Flujo&gt;-init.json</c>.</summary>
    public static void DumpInit(string testClass, Exception error) => Dump($"{testClass}-init", testClass, testClass, "init", error.Message);

    private static void Dump(string scenario, string displayName, string testClass, string? phase, string assertion)
    {
        var report = new Dictionary<string, object?> { ["scenario"] = scenario, ["displayName"] = displayName, ["testClass"] = testClass };
        if (phase is not null) report["phase"] = phase;
        report["assertion"] = assertion;
        if (lastExchange is { } exchange)
        {
            report["request"] = new Dictionary<string, object?> { ["method"] = exchange.Method, ["path"] = exchange.Path, ["headers"] = exchange.RequestHeaders, ["body"] = exchange.RequestBody };
            report["response"] = new Dictionary<string, object?> { ["status"] = exchange.Status, ["headers"] = exchange.ResponseHeaders, ["body"] = exchange.ResponseBody };
        }
        if (lastProbe is { } probe)
        {
            report["probe"] = new Dictionary<string, object?> { ["command"] = probe.Command, ["exitCode"] = probe.ExitCode, ["output"] = probe.Output };
        }
        var name = new string(scenario.Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-' ? c : '_').ToArray());
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, $"{(name.Length > 0 ? name : "unnamed")}.json"), JsonSerializer.Serialize(report, Options) + "\n");
        Clear();
    }
}
