// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Text;
using System.Text.Json;
using DSN.Execution;

Console.OutputEncoding = Encoding.UTF8;
static void Print(string text) => Console.WriteLine(DSNText.Translate(text));
static void PrintError(string text) => Console.Error.WriteLine(DSNText.Translate(text));
static int PrintVerification(ExpectationReport? report)
{
    if (report is null) return 0;
    Print(ExecutionSession.VerificationText(report));
    foreach (var check in report.Checks.Where(c => c.Result == "FAIL").Take(5))
        Print(DSNText.Concat(DSNText.Russian("Runner.Program.Text001", check.Tick, check.Word, ExecutionSession.FormatWord(check.Expected), ExecutionSession.FormatWord(check.Actual!.Value)), (check.Mask is { } mask ? DSNText.Russian("Runner.Program.Text002", mask) : "")));
    return report.Failed > 0 ? 2 : 0;
}
try
{
    DSNText.Configure(AppContext.BaseDirectory);
    var languageIndex = Array.IndexOf(args, "--language");
    if (languageIndex >= 0)
    {
        if (languageIndex + 1 >= args.Length || args.Count(a => a == "--language") != 1 || args[languageIndex + 1].ToLowerInvariant() is not ("ru" or "en"))
            throw new ArgumentException(DSNText.Russian("Localization.LanguageArgument"));
        DSNText.SetLanguage(args[languageIndex + 1].ToLowerInvariant());
        args = args.Where((_, index) => index != languageIndex && index != languageIndex + 1).ToArray();
    }
    string? implementation = null;
    var implementationIndex = Array.IndexOf(args, "--implementation");
    if (implementationIndex >= 0)
    {
        if (implementationIndex + 1 >= args.Length || args.Count(a => a == "--implementation") != 1) throw new ArgumentException(DSNText.Russian("Runner.Program.Text003"));
        implementation = args[implementationIndex + 1];
        args = args.Where((_, i) => i != implementationIndex && i != implementationIndex + 1).ToArray();
    }
    if (args.FirstOrDefault() == "--build-native")
    {
        var objectOnly = args.Contains("--object-only", StringComparer.Ordinal);
        if (args.Count(a => a == "--object-only") > 1) throw new ArgumentException(DSNText.Russian("Implementation.NativeUsage"));
        args = args.Where(a => a != "--object-only").ToArray();
        if (args.Length is < 4 or > 5) throw new ArgumentException(DSNText.Russian("Implementation.NativeUsage"));
        var context = args.Length == 5 ? DSNProgramBinder.ReadJson<ExecutionProfile>(args[4]) : null;
        var scope = context ?? UserProgramPreparation.StandardScope();
        var document = DSN.Core.DSNReader.Read(args[1]);
        var library = ExecutionImplementation.Load(AppContext.BaseDirectory, scope, implementation);
        var bindings = BindingCatalog.FromLibrary(library);
        var preparation = UserProgramPreparation.Prepare(document, library, bindings, FunctionSemantics.FromLibrary(library), scope, context);
        if (!preparation.Ready)
        {
            foreach (var message in preparation.Diagnostics.Take(12)) PrintError(message);
            return 2;
        }
        var bound = DSNProgramBinder.Bind(document, library, bindings, preparation.Profile!);
        var nativeTools = NativeToolPaths.ForArchitecture(bound.Program.Architecture, args[3]);
        var nativeResult = await NativeBuild.BuildAsync(NativeAssembly.Generate(bound.Program, document.SourceSha256), nativeTools, args[2], linkImage: !objectOnly);
        Print(DSNText.Russian("Native.Complete", nativeResult.Image ?? nativeResult.Object));
        return 0;
    }
    if (args.Length is 1 or 2 && !args[0].StartsWith("--", StringComparison.Ordinal))
        args = ["--run-user", args[0], args.ElementAtOrDefault(1) ?? Path.Combine(AppContext.BaseDirectory, "results", "DSN-result.json")];
    if (args.FirstOrDefault() is "--analyze-data" or "--prepare-data" or "--run-user")
    {
        var dataCommand = args[0]; var dataSteps = 1;
        var stepsIndex = Array.IndexOf(args, "--steps");
        if (stepsIndex >= 0)
        {
            if (stepsIndex + 1 >= args.Length || args.Count(a => a == "--steps") != 1 || dataCommand != "--run-user" ||
                !int.TryParse(args[stepsIndex + 1], out dataSteps) || dataSteps is < 1 or > ExecutionSession.MaxTicks)
                throw new ArgumentException(DSNText.Russian("Runner.Program.Text005"));
            args = args.Where((_, i) => i != stepsIndex && i != stepsIndex + 1).ToArray();
        }
        if (args.Length is < 3 or > 4) throw new ArgumentException(DSNText.Russian("Runner.Program.Text006"));
        var dataContext = args.Length == 4 ? DSNProgramBinder.ReadJson<ExecutionProfile>(args[3]) : null;
        var dataScope = dataContext ?? UserProgramPreparation.StandardScope();
        var dataDocument = DSN.Core.DSNReader.Read(args[1]);
        var dataLibrary = ExecutionImplementation.Load(AppContext.BaseDirectory, dataScope, implementation);
        var dataBindings = BindingCatalog.FromLibrary(dataLibrary);
        var semantics = FunctionSemantics.FromLibrary(dataLibrary);
        var preparation = UserProgramPreparation.Prepare(dataDocument, dataLibrary, dataBindings, semantics, dataScope, dataContext);
        object dataResult;
        if (dataCommand == "--analyze-data") dataResult = preparation;
        else
        {
            if (!preparation.Ready)
            {
                foreach (var message in preparation.Diagnostics.Take(12)) PrintError(message);
                return 2;
            }
            if (dataCommand == "--prepare-data") dataResult = preparation.Profile!;
            else
            {
                var userSession = new ExecutionSession(dataDocument, dataLibrary, dataBindings, preparation.Profile!);
                userSession.Step(dataSteps);
                dataResult = new { Result = "COMPLETED", ExecutionChecked = true, userSession.Identity, userSession.Schedule, userSession.Profile,
                    TimeUnit = DSNText.Russian("Execution.ScenarioRunner.Text001"), userSession.Samples, Preparation = preparation.Analysis };
            }
        }
        var dataDestination = Path.GetFullPath(args[2]); Directory.CreateDirectory(Path.GetDirectoryName(dataDestination)!);
        File.WriteAllText(dataDestination, JsonSerializer.Serialize(dataResult, DSNProgramBinder.JsonOptions));
        Print(DSNText.Russian("Runner.Program.Text009", preparation.Analysis.Variables.Length, (preparation.Ready ? DSNText.Russian("Runner.Program.Text007") : DSNText.Russian("Runner.Program.Text008"))));
        return 0;
    }
    if (args.FirstOrDefault() == "--schedule")
    {
        string? policy = null;
        var policyIndex = Array.IndexOf(args, "--order-policy");
        if (policyIndex >= 0)
        {
            if (policyIndex + 1 >= args.Length || args.Count(a => a == "--order-policy") != 1)
                throw new ArgumentException(DSNText.Russian("Runner.Program.Text010"));
            policy = args[policyIndex + 1];
            args = args.Where((_, i) => i != policyIndex && i != policyIndex + 1).ToArray();
        }
        if (args.Length is < 3 or > 4)
            throw new ArgumentException(DSNText.Russian("Runner.Program.Text011"));
        var profilePath = args.ElementAtOrDefault(3);
        var scheduleProfile = profilePath is null ? UserProgramPreparation.StandardScope() : DSNProgramBinder.ReadJson<ExecutionProfile>(profilePath);
        var sourceProfileHash = ScenarioFile.Fingerprint(scheduleProfile);
        if (policy is not null)
        {
            scheduleProfile.OrderPolicy = policy;
            if (policy == ExecutionSchedule.ReferenceOrdinal) scheduleProfile.Order = [];
        }
        var document = DSN.Core.DSNReader.Read(args[1]);
        var schedule = ExecutionSchedule.Create(document, scheduleProfile);
        var destination = Path.GetFullPath(args[2]); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var scheduleResult = new { Format = "DSNCompiler.Schedule", Version = 1, DSNSha256 = document.SourceSha256,
            SourceProfileSha256 = sourceProfileHash, PreviewOverride = policy is not null, ExecutionChecked = false, Schedule = schedule };
        File.WriteAllText(destination, JsonSerializer.Serialize(scheduleResult, DSNProgramBinder.JsonOptions));
        Print(DSNText.Russian("Runner.Program.Text012", schedule.Instances.Count, schedule.Policy));
        return 0;
    }
    if (args.FirstOrDefault() is "--compare" or "--prepare-scenario")
    {
        if (args.Length is < 4 or > 5) throw new ArgumentException(DSNText.Russian("Runner.Program.Text013"));
        var profileName = args.ElementAtOrDefault(4);
        var destination = Path.GetFullPath(args[3]);
        if (args[0] == "--prepare-scenario")
        {
            var document = DSN.Core.DSNReader.Read(args[1]);
            var scenario = ScenarioFile.Read(args[2]);
            ExecutionSession original; ExecutionSession target;
            if (profileName is not null)
            {
                var context = DSNProgramBinder.ReadJson<ExecutionProfile>(ExecutionSession.ProfilePath(AppContext.BaseDirectory, profileName));
                var sourceId = ExecutionImplementation.ForIdentity(AppContext.BaseDirectory, context, scenario.Identity);
                original = ExecutionSession.FromAssets(document, AppContext.BaseDirectory, profileName, sourceId);
                target = ExecutionSession.FromAssets(document, AppContext.BaseDirectory, profileName, implementation);
            }
            else
            {
                var context = scenario.PreparedProfile ?? throw new InvalidDataException(DSNText.Russian("Profile.Required"));
                original = ExecutionImplementation.Available(AppContext.BaseDirectory, context).Select(id => new ExecutionSession(document,
                    ExecutionImplementation.Load(AppContext.BaseDirectory, context, id), ProgramDataContext.Clone(context)))
                    .SingleOrDefault(s => s.Identity == scenario.Identity) ?? throw new InvalidDataException(DSNText.Russian("Implementation.Record"));
                target = new(document, ExecutionImplementation.Load(AppContext.BaseDirectory, context, implementation), ProgramDataContext.Clone(context));
            }
            var prepared = ImplementationComparison.ForImplementation(scenario, original, target);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!); ScenarioFile.Save(destination, prepared, target);
            Print(DSNText.Concat(DSNText.Concat(DSNText.Russian("Runner.Program.Text014"), target.Architecture), ": ") + destination); return 0;
        }
        var comparison = ImplementationComparison.Run(args[1], AppContext.BaseDirectory, profileName, args[2] == "-" ? null : args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, JsonSerializer.Serialize(comparison, DSNProgramBinder.JsonOptions));
        Print(DSNText.Russian("Implementation.Comparison", comparison.Result, string.Join(" / ", comparison.Runs.Select(r => r.Implementation)), comparison.CompletedTicks, comparison.Words, comparison.ComparedValues));
        if (comparison.Difference is { } difference) Print(JsonSerializer.Serialize(difference));
        return comparison.Result == "PASS" ? 0 : 2;
    }
    if (args.FirstOrDefault() == "--resume")
    {
        if (args.Length is < 5 or > 6 || !int.TryParse(args[3], out var steps))
            throw new ArgumentException(DSNText.Russian("Runner.Program.Text016"));
        var resumed = ScenarioRunner.Resume(args[1], AppContext.BaseDirectory, args.ElementAtOrDefault(5), args[2], steps, implementation);
        var destination = Path.GetFullPath(args[4]); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, JsonSerializer.Serialize(resumed, DSNProgramBinder.JsonOptions));
        Print(DSNText.Russian("Runner.Program.Text017", resumed.ResumedAt, resumed.Tick, destination));
        if (resumed.Stop is { } stop) Print(DSNText.Concat(DSNText.Russian("Runner.Program.Text018"), DSNText.Join("; ", stop.Matches.Take(3).Select(ExecutionSession.StopMatchText))));
        return PrintVerification(resumed.Verification);
    }
    if (args.FirstOrDefault() == "--scenario")
    {
        if (args.Length is < 4 or > 5) throw new ArgumentException(DSNText.Russian("Runner.Program.Text019"));
        var playback = ScenarioRunner.RunSaved(args[1], AppContext.BaseDirectory, args.ElementAtOrDefault(4), args[2], implementation);
        var destination = Path.GetFullPath(args[3]); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, JsonSerializer.Serialize(playback, DSNProgramBinder.JsonOptions));
        Print(DSNText.Russian("Runner.Program.Text020", playback.Scenario.Title, playback.Samples.Count - 1, destination));
        return PrintVerification(playback.Verification);
    }
    if (args.Length == 0 || args.Length > 3 || args.FirstOrDefault() is "--help" or "-h")
    {
        Print(DSNText.Russian("Runner.Program.Text022"));
        Print(DSNText.Russian("Runner.Program.Text019"));
        Print(DSNText.Russian("Runner.Program.Text016"));
        Print(DSNText.Russian("Runner.Program.Text011"));
        Print(DSNText.Russian("Runner.Program.Text006"));
        Print(DSNText.Russian("Implementation.ChoiceHelp", string.Join(" / ", ExecutionImplementation.Available(AppContext.BaseDirectory))));
        Print(DSNText.Russian("Implementation.RebindUsage"));
        Print(DSNText.Russian("Runner.Program.Text025"));
        Print(DSNText.Russian("Implementation.NativeUsage"));
        return args.Length > 3 ? 1 : 0;
    }
    var root = AppContext.BaseDirectory;
    var dsn = args[0];
    var output = Path.GetFullPath(args[1]);
    var profile = args[2];
    var result = ScenarioRunner.Run(dsn, "", profile,
        ExecutionImplementation.Load(root, DSNProgramBinder.ReadJson<ExecutionProfile>(profile), implementation));
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(result, DSNProgramBinder.JsonOptions));
    Print(result.Description);
    foreach (var assumption in result.Assumptions) Print(DSNText.Concat(DSNText.Russian("Runner.Program.Text026"), assumption));
    Print(DSNText.Russian("Runner.Program.Text027", result.Samples.Count - 1, output));
    return 0;
}
catch (Exception error) { PrintError(error.Message); return 1; }
