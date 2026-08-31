namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Describes why a project's evaluated inputs could not be included in a complete manifest.
/// </summary>
internal sealed record WorkspaceProjectInputFailure
{
    /// <summary>
    /// Gets the project path whose evaluation failed.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Gets the actionable evaluation failure message.
    /// </summary>
    public required string Message { get; init; }
}
