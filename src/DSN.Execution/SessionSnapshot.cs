// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Text.Json.Serialization;

namespace DSN.Execution;

public sealed class AdspMachineState
{
    [JsonRequired] public long Tick { get; set; }
    [JsonRequired] public int MemoryWords { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Architecture { get; set; }
    [JsonRequired] public Dictionary<string, uint> Registers { get; set; } = [];
    [JsonRequired] public AdspFlags Flags { get; set; } = null!;
    [JsonIgnore] public ushort[] Memory { get; set; } = [];
}

public sealed record SavedHistoryRow(ushort[] Values, AdspFlags Flags);
public sealed record InitialWordDraft(string Name, bool Override, string Value);
public sealed record InputEventDraft(string Tick, string Word, string Value);
public sealed class VariableTimelineViewState
{
    [JsonRequired] public int HistoryTab { get; set; }
    [JsonRequired] public bool AutoFit { get; set; } = true;
    [JsonRequired] public bool FollowLatest { get; set; } = true;
    [JsonRequired] public int StartTick { get; set; }
    [JsonRequired] public int VisibleTicks { get; set; } = 1;
    [JsonRequired] public int CursorTick { get; set; }
}
public sealed class SessionViewState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProgramDataDraft? DataDraft { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExpectationDraft[]? ExpectationDrafts { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public StopConditionDraft[]? StopDrafts { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VariableTimelineViewState? Timeline { get; set; }
    [JsonRequired] public string[] Observed { get; set; } = [];
    [JsonRequired] public InitialWordDraft[] Initial { get; set; } = [];
    [JsonRequired] public string ScenarioTitle { get; set; } = "";
    [JsonRequired] public string ScenarioSteps { get; set; } = "";
    [JsonRequired] public InputEventDraft[] Events { get; set; } = [];
    [JsonRequired] public bool CustomScenario { get; set; }
    [JsonRequired] public bool EventsOnReset { get; set; }
    [JsonRequired] public string SelectedInput { get; set; } = "";
    [JsonRequired] public string InputText { get; set; } = "";
    [JsonRequired] public string? SelectedBlock { get; set; }
    [JsonRequired] public int SelectedTab { get; set; }
}

public sealed class SavedExecutionSession
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ProcessorHistory? ProcessorHistory { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExecutionProfile? PreparedProfile { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExecutionStopState? Stops { get; set; }
    [JsonRequired] public ScenarioIdentity Identity { get; set; } = null!;
    [JsonRequired] public string LayoutSha256 { get; set; } = "";
    [JsonRequired] public AdspMachineState Machine { get; set; } = null!;
    [JsonRequired] public bool UseScenarioEvents { get; set; }
    [JsonRequired] public Dictionary<string, ushort> InitialOverrides { get; set; } = [];
    [JsonRequired] public ExecutionScenario? Scenario { get; set; }
    [JsonRequired] public Dictionary<string, ushort> PendingInputs { get; set; } = [];
    [JsonRequired] public InputEvent[] ManualInputs { get; set; } = [];
    [JsonRequired] public string[] HistoryWords { get; set; } = [];
    [JsonRequired] public int HistoryCount { get; set; }
    [JsonRequired] public StepExecutionRecord? LastStep { get; set; }
    [JsonRequired] public string[] Changed { get; set; } = [];
    [JsonRequired] public SessionViewState View { get; set; } = null!;
    [JsonIgnore] public SavedHistoryRow[] History { get; set; } = [];
}
