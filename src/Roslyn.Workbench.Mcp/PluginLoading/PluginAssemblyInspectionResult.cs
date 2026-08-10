using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginAssemblyInspectionResult
{
    [MemberNotNullWhen(true, nameof(EntryPoints))]
    public bool Succeeded => Outcome == PluginAssemblyInspectionOutcome.Success;

    public bool WasSkipped => Outcome == PluginAssemblyInspectionOutcome.Skipped;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool Failed => Outcome == PluginAssemblyInspectionOutcome.Failure;

    public PluginAssemblyInspectionOutcome Outcome { get; }

    public IReadOnlyList<PluginEntryPointMetadata>? EntryPoints { get; }

    public string? Error { get; }

    private PluginAssemblyInspectionResult(
        PluginAssemblyInspectionOutcome outcome,
        IReadOnlyList<PluginEntryPointMetadata>? entryPoints,
        string? error)
    {
        Outcome = outcome;
        EntryPoints = entryPoints;
        Error = error;
    }

    public static PluginAssemblyInspectionResult Success(IReadOnlyList<PluginEntryPointMetadata> entryPoints)
    {
        if (entryPoints.Count == 0)
        {
            throw new ArgumentException("A successful assembly inspection must contain at least one plugin entry point.", nameof(entryPoints));
        }

        return new PluginAssemblyInspectionResult(
            PluginAssemblyInspectionOutcome.Success,
            entryPoints,
            error: null);
    }

    public static PluginAssemblyInspectionResult Skipped()
    {
        return new PluginAssemblyInspectionResult(
            PluginAssemblyInspectionOutcome.Skipped,
            entryPoints: null,
            error: null);
    }

    public static PluginAssemblyInspectionResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new PluginAssemblyInspectionResult(
            PluginAssemblyInspectionOutcome.Failure,
            entryPoints: null,
            error);
    }
}
