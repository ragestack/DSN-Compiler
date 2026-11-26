// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;

namespace DSN.Execution;

public sealed record ExecutionSchedule(string Contract, string Policy, bool ReferenceNamesAffectOrder,
    IReadOnlyList<string> Instances, IReadOnlyList<string> BeforeTick, IReadOnlyList<string> Notices)
{
    public const string SequentialContract = "sequential-in-place-v1";
    public const string ProfileOrder = "profile-order";
    public const string ReferenceOrdinal = "dsn-reference-ordinal-v1";

    public static ExecutionSchedule Create(LogicProgramDocument document, ExecutionProfile profile)
    {
        if (document.Pages.Count != 1) throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text001"));
        var references = document.Pages[0].Instances.Select(i => i.Reference).ToArray();
        if (references.Any(string.IsNullOrWhiteSpace) || references.Any(r => r != r.Trim() || r.Any(char.IsControl)))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text002"));
        if (references.Distinct(StringComparer.OrdinalIgnoreCase).Count() != references.Length)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text003"));
        if (profile.Order is null || profile.BeforeTick is null)
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text004"));
        if (profile.BeforeTick.Any(c => c is null || string.IsNullOrWhiteSpace(c.Instance)))
            throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text005"));
        var policy = profile.OrderPolicy ?? ProfileOrder;
        string[] order;
        switch (policy)
        {
            case ProfileOrder:
                var known = references.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (profile.Order.Length != references.Length || profile.Order.Any(id => id is null || !known.Contains(id)) ||
                    profile.Order.Distinct(StringComparer.OrdinalIgnoreCase).Count() != references.Length)
                    throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text006"));
                order = profile.Order.Select(id => references.Single(r => string.Equals(r, id, StringComparison.OrdinalIgnoreCase))).ToArray();
                break;
            case ReferenceOrdinal:
                if (profile.CompatibilityConfirmed)
                    throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text007"));
                if (profile.Order.Length != 0)
                    throw new InvalidDataException(DSNText.Russian("Execution.ExecutionSchedule.Text008"));
                order = references.Order(StringComparer.Ordinal).ToArray();
                break;
            default:
                throw new InvalidDataException(DSNText.Concat(DSNText.Russian("Execution.ExecutionSchedule.Text009"), policy));
        }
        var notices = policy == ReferenceOrdinal
            ? new[] { DSNText.Russian("Execution.ExecutionSchedule.Text010"),
                DSNText.Russian("Execution.ExecutionSchedule.Text011") }
            : new[] { DSNText.Russian("Execution.ExecutionSchedule.Text012") };
        return new(SequentialContract, policy, policy == ReferenceOrdinal, Array.AsReadOnly(order),
            Array.AsReadOnly(profile.BeforeTick.Select(c => c.Instance).ToArray()), Array.AsReadOnly(notices));
    }
}
