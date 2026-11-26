// Автор и правообладатель: Леонов Денис <466611@mail.ru>
using DSN.Core;

namespace DSN.Execution;

public static class PreparedProgramContext
{
    public static ExecutionProfile? Capture(ExecutionSession session) => session.Profile.PreparedDSNSha256 is null ? null : ProgramDataContext.Clone(session.Profile);
    public static void Validate(ExecutionProfile? profile, ScenarioIdentity identity)
    {
        if (profile is null) return;
        ProgramDataContext.Shape(profile);
        if (profile.PreparedDSNSha256 != identity.DSNSha256 || ScenarioFile.Fingerprint(profile) != identity.ProfileSha256 ||
            profile.Id != identity.ProfileId || profile.CompatibilityConfirmed != identity.CompatibilityConfirmed)
            throw new InvalidDataException(DSNText.Russian("Execution.PreparedProgramContext.Text001"));
    }
    public static ExecutionSession Create(LogicProgramDocument document, string assetsRoot, ExecutionProfile profile, ScenarioIdentity identity, string? implementation = null)
    {
        Validate(profile, identity);
        if (identity.DSNSha256 != document.SourceSha256)
            throw new InvalidDataException(DSNText.Russian("Execution.PreparedProgramContext.Text002"));
        var selected = ExecutionImplementation.ForIdentity(assetsRoot, profile, identity, implementation);
        var library = ExecutionImplementation.Load(assetsRoot, profile, selected);
        if (library.Sha256 != identity.LibrarySha256) throw new InvalidDataException(DSNText.Russian("Execution.PreparedProgramContext.Text003"));
        var bindings = BindingCatalog.FromLibrary(library);
        var session = new ExecutionSession(document, library, bindings, ProgramDataContext.Clone(profile));
        if (session.Identity != identity) throw new InvalidDataException(DSNText.Russian("Execution.PreparedProgramContext.Text004"));
        return session;
    }
}
