using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceProjectInputResolution
{
    public IReadOnlyList<string> ArtifactRoots { get; }

    public WorkspaceProjectInputFailure? Failure { get; }

    public IReadOnlyList<string> ImportedPaths { get; }

    public IReadOnlyList<WorkspaceEvaluatedItemGlob> ItemGlobs { get; }

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
