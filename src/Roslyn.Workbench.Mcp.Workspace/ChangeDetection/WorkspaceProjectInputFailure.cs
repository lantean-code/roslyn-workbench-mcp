namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceProjectInputFailure
{
    public required string ProjectPath { get; init; }

    public required string Message { get; init; }
}
