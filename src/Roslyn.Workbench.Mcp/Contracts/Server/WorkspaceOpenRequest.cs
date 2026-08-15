using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to load a writable workspace.
/// </summary>
internal sealed record WorkspaceOpenRequest
{
    /// <summary>
    /// Gets the optional caller-friendly alias for the workspace.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Gets the absolute solution or project path to load.
    /// </summary>
    [Required]
    public required string Path { get; init; }

    /// <summary>
    /// Gets the optional allowlisted MSBuild global properties used to evaluate this workspace.
    /// </summary>
    public WorkspaceMsBuildProperties? MsBuildProperties { get; init; }

    /// <summary>
    /// Gets the optional absolute repository or workspace root used for coordination and transaction boundaries.
    /// </summary>
    public string? WorkspaceRoot { get; init; }
}
