namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Records the first input change that makes a loaded Workspace snapshot potentially stale.
/// </summary>
internal sealed record WorkspaceInputChange
{
    /// <summary>
    /// Gets how the change was detected.
    /// </summary>
    public WorkspaceInputChangeDetectionSource DetectionSource { get; init; }

    /// <summary>
    /// Gets the structured watcher or membership error when the change represents a detection failure.
    /// </summary>
    public WorkspaceInputChangeErrorCode? ErrorCode { get; init; }

    /// <summary>
    /// Gets the observed filesystem, metadata or manifest change category.
    /// </summary>
    public WorkspaceInputChangeKind Kind { get; init; }

    /// <summary>
    /// Gets the affected path when the change is path-specific.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the former path for a rename operation.
    /// </summary>
    public string? PreviousPath { get; init; }
}
