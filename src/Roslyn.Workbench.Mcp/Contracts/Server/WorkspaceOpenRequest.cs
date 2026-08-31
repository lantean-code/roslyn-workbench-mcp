using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to load a writable workspace.
/// </summary>
internal sealed record WorkspaceOpenRequest
{
    /// <summary>
    /// The optional caller-friendly alias for the workspace.
    /// </summary>
    [Description("The optional caller-friendly alias for the workspace.")]
    public string? Alias { get; init; }

    /// <summary>
    /// The absolute solution or project path to load.
    /// </summary>
    [Description("The absolute solution or project path to load.")]
    [Required]
    public required string Path { get; init; }

    /// <summary>
    /// The optional allowlisted MSBuild global properties used to evaluate this workspace.
    /// </summary>
    [Description("The optional allowlisted MSBuild global properties used to evaluate this workspace.")]
    public WorkspaceMsBuildProperties? MsBuildProperties { get; init; }

    /// <summary>
    /// The optional absolute repository or workspace root used for coordination and transaction boundaries.
    /// </summary>
    [Description("The optional absolute repository or workspace root used for coordination and transaction boundaries.")]
    public string? WorkspaceRoot { get; init; }
}
