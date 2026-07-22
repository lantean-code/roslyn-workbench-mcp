using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceProjectInputResolution
{
    public IReadOnlyList<string> Paths { get; }

    public WorkspaceProjectInputFailure? Failure { get; }

    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSucceeded => Failure is null;

    private WorkspaceProjectInputResolution(
        IReadOnlyList<string> paths,
        WorkspaceProjectInputFailure? failure)
    {
        Paths = paths;
        Failure = failure;
    }

    public static WorkspaceProjectInputResolution Succeeded(IReadOnlyList<string>? paths = null)
    {
        return new WorkspaceProjectInputResolution(paths ?? [], failure: null);
    }

    public static WorkspaceProjectInputResolution Failed(string projectPath, string message)
    {
        var failure = new WorkspaceProjectInputFailure
        {
            ProjectPath = projectPath,
            Message = message,
        };

        return new WorkspaceProjectInputResolution(paths: [], failure);
    }
}
