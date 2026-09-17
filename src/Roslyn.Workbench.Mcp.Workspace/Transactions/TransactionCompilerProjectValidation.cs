namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Reports compiler-error comparison counts for one affected loaded project evaluation.
/// </summary>
internal sealed record TransactionCompilerProjectValidation
{
    /// <summary>
    /// Gets the canonical Workspace-relative project path or project name fallback.
    /// </summary>
    [Description("Workspace-relative path for the evaluated project.")]
    public required string Project { get; init; }

    /// <summary>
    /// Gets the loaded target-framework identity when available.
    /// </summary>
    [Description("Loaded target-framework identity when available.")]
    public string? TargetFramework { get; init; }

    /// <summary>
    /// Gets whether both compiler evaluations completed.
    /// </summary>
    [Description("Whether baseline and staged compiler evaluations completed.")]
    public required bool IsComplete { get; init; }

    /// <summary>
    /// Gets the baseline compiler-error count.
    /// </summary>
    [Description("Baseline compiler-error count for this project evaluation.")]
    public required int BaselineErrorCount { get; init; }

    /// <summary>
    /// Gets the staged compiler-error count.
    /// </summary>
    [Description("Staged compiler-error count for this project evaluation.")]
    public required int StagedErrorCount { get; init; }

    /// <summary>
    /// Gets the newly introduced compiler-error count.
    /// </summary>
    [Description("Newly introduced compiler-error count for this project evaluation.")]
    public required int IntroducedErrorCount { get; init; }
}
