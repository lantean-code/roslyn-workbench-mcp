namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceInputChange
{
    public WorkspaceInputChangeDetectionSource DetectionSource { get; init; }

    public WorkspaceInputChangeErrorCode? ErrorCode { get; init; }

    public WorkspaceInputChangeKind Kind { get; init; }

    public string? Path { get; init; }

    public string? PreviousPath { get; init; }
}
