namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed record WorkspaceReloadOutcome
{
    public required WorkspaceIdentity Workspace { get; init; }

    public int ProjectCount { get; init; }

    public int DocumentCount { get; init; }

    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
