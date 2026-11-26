// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DSN.Core;

namespace DSN.Execution;

public sealed record ReportOptions(int FirstTick, int LastTick, bool Registers = true, bool Scheme = true,
    bool Diagrams = true, bool Json = true, string Language = "ru");
public sealed class ExecutionReportSnapshot
{
    public string Format { get; init; } = "DSNCompiler.ExecutionReport";
    public int Version { get; init; } = 1;
    public DateTimeOffset Created { get; init; }
    public string ApplicationVersion { get; init; } = "";
    public string Program { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string Language { get; init; } = "ru";
    public ScenarioIdentity Identity { get; init; } = null!;
    public int FirstTick { get; init; }
    public int LastTick { get; init; }
    public long CompletedTick { get; init; }
    public bool Faulted { get; init; }
    public string? FaultDiagnostic { get; init; }
    public bool Diagrams { get; init; }
    public string[] Order { get; init; } = [];
    public string[] Variables { get; init; } = [];
    public ushort[][] Values { get; init; } = [];
    public AdspFlags?[] Flags { get; init; } = [];
    public string[] Observed { get; init; } = [];
    public string[] RegisterNames { get; init; } = [];
    public int RegisterBits { get; init; }
    public ProcessorHistory? Processor { get; init; }
    public bool RegistersIncluded { get; init; }
    public ExecutionProfile Context { get; init; } = null!;
    public Dictionary<string, ushort> InitialOverrides { get; init; } = [];
    public ExecutionScenario? Scenario { get; init; }
    public bool UseScenarioEvents { get; init; }
    public InputEvent[] ManualInputs { get; init; } = [];
    public Dictionary<string, ushort> PendingInputs { get; init; } = [];
    public StopCondition[] StopConditions { get; init; } = [];
    public StopHit? LastStop { get; init; }
    public ExpectationReport Verification { get; init; } = null!;
}

public sealed partial class ExecutionSession
{
    public ExecutionReportSnapshot CreateReport(ReportOptions options, IEnumerable<string>? observed = null, DateTimeOffset? created = null)
    {
        if (options.FirstTick < 0 || options.LastTick < options.FirstTick || options.LastTick >= samples.Count || options.Language is not ("ru" or "en"))
            throw new ArgumentException(DSNText.Russian("Report.InvalidRange"));
        var history = options.Registers ? CaptureProcessorHistory() : null;
        if (history is not null)
        {
            var first = Math.Max(history.FirstTick, options.FirstTick); var last = Math.Min(history.LastTick, options.LastTick);
            history = first > last ? null : new() { Architecture = history.Architecture, RegisterBits = history.RegisterBits, Names = history.Names,
                FirstTick = first, Values = history.Values.Skip(checked((int)(first - history.FirstTick))).Take(checked((int)(last - first + 1))).ToArray() };
        }
        return new() { Created = created ?? DateTimeOffset.Now, ApplicationVersion = typeof(ExecutionSession).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "",
            Program = Path.GetFileName(Document.FileName), Architecture = Architecture, Identity = Identity, Language = options.Language,
            FirstTick = options.FirstTick, LastTick = options.LastTick, CompletedTick = samples[^1].Tick, Faulted = Faulted, FaultDiagnostic = FaultDiagnostic, Diagrams = options.Diagrams,
            Order = Bound.Program.InstanceOrder.ToArray(), Variables = Variables.ToArray(),
            Values = samples.Skip(options.FirstTick).Take(options.LastTick - options.FirstTick + 1).Select(s => Variables.Select(n => s.Words[n]).ToArray()).ToArray(),
            Flags = samples.Skip(options.FirstTick).Take(options.LastTick - options.FirstTick + 1).Select(s => Architecture == "RV32I" ? null : s.Flags).ToArray(),
            Observed = (observed ?? Profile.Observe).Where(n => Variables.Contains(n, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RegisterNames = Bound.Program.RegisterNames.ToArray(), RegisterBits = Architecture == "RV32I" ? 32 : 16, Processor = history, RegistersIncluded = options.Registers,
            Context = ProgramDataContext.Clone(Profile), InitialOverrides = new(appliedInitial),
            Scenario = appliedScenario is null ? null : ScenarioFile.Validate(appliedScenario, this), UseScenarioEvents = UseScenarioEvents,
            ManualInputs = ManualInputs.ToArray(), PendingInputs = new(pending), StopConditions = stopConditions.ToArray(), LastStop = LastStop,
            Verification = VerifyExpectations() };
    }
}

public sealed record ReportFiles(string Html, string? Json);

public static class ExecutionReport
{
    private static readonly JsonSerializerOptions Compact = new(DSNProgramBinder.JsonOptions) { WriteIndented = false };
    public static ReportFiles Save(string directory, ExecutionReportSnapshot snapshot, string svg, bool json)
    {
        var folder = Path.GetFullPath(directory); Directory.CreateDirectory(folder);
        using var stream = typeof(ExecutionReport).Assembly.GetManifestResourceStream("DSN.ExecutionReport.html")!;
        using var reader = new StreamReader(stream);
        var labels = new { ru = DSNText.TranslationCatalog("ru").Where(p => p.Key.StartsWith("Report.", StringComparison.Ordinal)).ToDictionary(p => p.Key[7..], p => p.Value),
            en = DSNText.TranslationCatalog("en").Where(p => p.Key.StartsWith("Report.", StringComparison.Ordinal)).ToDictionary(p => p.Key[7..], p => p.Value) };
        var title = System.Net.WebUtility.HtmlEncode(snapshot.Program);
        var html = reader.ReadToEnd().Replace("__TITLE__", title, StringComparison.Ordinal)
            .Replace("__SCHEME__", svg, StringComparison.Ordinal).Replace("__LANGUAGE__", snapshot.Language, StringComparison.Ordinal)
            .Replace("__LABELS__", JsonSerializer.Serialize(labels, Compact), StringComparison.Ordinal)
            .Replace("__DATA__", JsonSerializer.Serialize(snapshot, Compact), StringComparison.Ordinal);
        // Имена в данных JSON экранируются стандартным кодировщиком, в DOM используются только текстовые узлы.
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var name = "interactive-state-history-" + snapshot.Created.AddMilliseconds(attempt).ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            var destination = Path.Combine(folder, name + ".html"); var data = json ? Path.Combine(folder, name + ".json") : null;
            if (File.Exists(destination) || data is not null && File.Exists(data)) continue;
            var temporary = Path.Combine(folder, "." + Guid.NewGuid().ToString("N")); var moved = new List<string>();
            try
            {
                File.WriteAllText(temporary, html, new UTF8Encoding(false));
                File.Move(temporary, destination, false); moved.Add(destination);
                if (data is not null)
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) JsonSerializer.Serialize(output, snapshot, DSNProgramBinder.JsonOptions);
                    File.Move(temporary, data, false); moved.Add(data);
                }
                return new(destination, data);
            }
            catch { foreach (var file in moved) File.Delete(file); throw; }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        throw new IOException(DSNText.Russian("Report.NameConflict"));
    }
}
