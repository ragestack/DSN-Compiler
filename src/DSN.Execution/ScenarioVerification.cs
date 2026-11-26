// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;
using System.Text.Json.Serialization;

namespace DSN.Execution;

public sealed record ScenarioExpectation([property: JsonRequired] int Tick, [property: JsonRequired] string Word,
    [property: JsonRequired] ushort Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ushort? Mask = null);
public sealed record ExpectationDraft(string Tick, string Word, string Value, string Mask);
public sealed record ExpectationCheck(int Tick, string Word, ushort Expected, ushort? Actual, ushort? Mask, string Result);
public sealed record ExpectationReport(string Result, int Total, int Checked, int Passed, int Failed, int Pending,
    IReadOnlyList<ExpectationCheck> Checks);

public sealed partial class ExecutionSession
{
    public ExpectationReport VerifyExpectations()
    {
        var checks = (appliedScenario?.Expectations ?? []).Select(e =>
        {
            if (e.Tick >= samples.Count) return new ExpectationCheck(e.Tick, e.Word, e.Value, null, e.Mask, "PENDING");
            var actual = samples[e.Tick].Words[e.Word];
            var matched = (actual & (e.Mask ?? ushort.MaxValue)) == e.Value;
            return new ExpectationCheck(e.Tick, e.Word, e.Value, actual, e.Mask, matched ? "PASS" : "FAIL");
        }).ToArray();
        var passed = checks.Count(c => c.Result == "PASS"); var failed = checks.Count(c => c.Result == "FAIL");
        var pending = checks.Length - passed - failed;
        return new(checks.Length == 0 ? "NOT_CONFIGURED" : failed > 0 ? "FAIL" : pending > 0 ? "PENDING" : "PASS",
            checks.Length, passed + failed, passed, failed, pending, Array.AsReadOnly(checks));
    }

    public static string VerificationText(ExpectationReport report) => report.Result switch
    {
        "NOT_CONFIGURED" => DSNText.Russian("Execution.ScenarioVerification.Text001"),
        "PASS" => DSNText.Russian("Execution.ScenarioVerification.Text002", report.Passed, report.Total),
        "FAIL" => DSNText.Russian("Execution.ScenarioVerification.Text003", report.Failed, report.Passed, report.Pending),
        _ => DSNText.Russian("Execution.ScenarioVerification.Text004", report.Checked, report.Total, report.Pending)
    };
}
