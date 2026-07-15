using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceProjectInputResolution
{
    public IReadOnlyList<string> Paths { get; init; } = [];

    public WorkspaceProjectInputFailure? Failure { get; init; }

    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsSucceeded => Failure is null;

    public static WorkspaceProjectInputResolution Succeeded(IReadOnlyList<string>? paths = null)
    {
        return new WorkspaceProjectInputResolution
        {
            Paths = paths ?? [],
        };
    }

    public static WorkspaceProjectInputResolution Failed(string projectPath, string message)
    {
        return new WorkspaceProjectInputResolution
        {
            Failure = new WorkspaceProjectInputFailure
            {
                ProjectPath = projectPath,
                Message = message,
            },
        };
    }
}
