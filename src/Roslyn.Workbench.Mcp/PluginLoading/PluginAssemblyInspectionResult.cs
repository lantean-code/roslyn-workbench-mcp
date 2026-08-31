using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Reports the plugin entry points read from assembly metadata or why the candidate was skipped or rejected.
/// </summary>
internal sealed class PluginAssemblyInspectionResult
{
    /// <summary>
    /// Gets a value indicating whether assembly inspection discovered valid plugin entry points.
    /// </summary>
    [MemberNotNullWhen(true, nameof(EntryPoints))]
    public bool Succeeded => Outcome == PluginAssemblyInspectionOutcome.Success;

    /// <summary>
    /// Gets a value indicating whether the assembly was intentionally skipped.
    /// </summary>
    public bool WasSkipped => Outcome == PluginAssemblyInspectionOutcome.Skipped;

    /// <summary>
    /// Gets a value indicating whether assembly inspection failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool Failed => Outcome == PluginAssemblyInspectionOutcome.Failure;

    /// <summary>
    /// Gets the outcome of plugin assembly inspection.
    /// </summary>
    public PluginAssemblyInspectionOutcome Outcome { get; }

    /// <summary>
    /// Gets the valid plugin entry points discovered when inspection succeeds.
    /// </summary>
    public IReadOnlyList<PluginEntryPointMetadata>? EntryPoints { get; }

    /// <summary>
    /// Gets the error reported when inspection fails.
    /// </summary>
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

    /// <summary>
    /// Creates a result that represents successful completion.
    /// </summary>
    /// <param name="entryPoints">The valid plugin entry points discovered in the assembly.</param>
    /// <returns>A result that represents successful completion.</returns>
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

    /// <summary>
    /// Creates a result that represents a skipped candidate.
    /// </summary>
    /// <returns>A result that represents a skipped candidate.</returns>
    public static PluginAssemblyInspectionResult Skipped()
    {
        return new PluginAssemblyInspectionResult(
            PluginAssemblyInspectionOutcome.Skipped,
            entryPoints: null,
            error: null);
    }

    /// <summary>
    /// Creates a result that represents failure.
    /// </summary>
    /// <param name="error">The error that caused the operation to fail.</param>
    /// <returns>A result that represents failure.</returns>
    public static PluginAssemblyInspectionResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        return new PluginAssemblyInspectionResult(
            PluginAssemblyInspectionOutcome.Failure,
            entryPoints: null,
            error);
    }
}
