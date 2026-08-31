using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Represents either the evaluated inputs of one project or the reason evaluation failed.
/// </summary>
internal sealed record WorkspaceProjectInputResolution
{
    /// <summary>
    /// Gets generated-output roots that may safely be excluded when they contain no protected input.
    /// </summary>
    public IReadOnlyList<string> ArtifactRoots { get; }

    /// <summary>
    /// Gets the evaluation failure when resolution did not succeed.
    /// </summary>
    public WorkspaceProjectInputFailure? Failure { get; }

    /// <summary>
    /// Gets project and imported build files that affect evaluation.
    /// </summary>
    public IReadOnlyList<string> ImportedPaths { get; }

    /// <summary>
    /// Gets evaluated item globs whose membership can change the loaded source set.
    /// </summary>
    public IReadOnlyList<WorkspaceEvaluatedItemGlob> ItemGlobs { get; }

    /// <summary>
    /// Gets whether project input evaluation completed successfully.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSucceeded => Failure is null;

    private WorkspaceProjectInputResolution(
        IReadOnlyList<string> artifactRoots,
        IReadOnlyList<string> importedPaths,
        IReadOnlyList<WorkspaceEvaluatedItemGlob> itemGlobs,
        WorkspaceProjectInputFailure? failure)
    {
        ArtifactRoots = artifactRoots;
        ImportedPaths = importedPaths;
        ItemGlobs = itemGlobs;
        Failure = failure;
    }

    /// <summary>
    /// Creates a successful project-input resolution.
    /// </summary>
    /// <param name="importedPaths">The project and imported build files.</param>
    /// <param name="artifactRoots">Generated-output roots discovered during evaluation.</param>
    /// <param name="itemGlobs">The evaluated item globs.</param>
    /// <returns>A successful resolution with empty collections for omitted categories.</returns>
    public static WorkspaceProjectInputResolution Succeeded(
        IReadOnlyList<string>? importedPaths = null,
        IReadOnlyList<string>? artifactRoots = null,
        IReadOnlyList<WorkspaceEvaluatedItemGlob>? itemGlobs = null)
    {
        return new WorkspaceProjectInputResolution(
            artifactRoots ?? [],
            importedPaths ?? [],
            itemGlobs ?? [],
            failure: null);
    }

    /// <summary>
    /// Creates a failed project-input resolution without partial input data.
    /// </summary>
    /// <param name="projectPath">The project path that could not be evaluated.</param>
    /// <param name="message">The actionable evaluation failure message.</param>
    /// <returns>A failed resolution.</returns>
    public static WorkspaceProjectInputResolution Failed(string projectPath, string message)
    {
        var failure = new WorkspaceProjectInputFailure
        {
            ProjectPath = projectPath,
            Message = message,
        };

        return new WorkspaceProjectInputResolution(
            artifactRoots: [],
            importedPaths: [],
            itemGlobs: [],
            failure);
    }
}
