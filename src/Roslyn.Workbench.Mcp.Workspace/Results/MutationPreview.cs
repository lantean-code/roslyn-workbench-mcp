namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents the compact preview returned for a staged mutation.
/// </summary>
public sealed record MutationPreview
{
    /// <summary>
    /// Gets the concise summary of the staged mutation.
    /// </summary>
    public string Summary { get; init; } = string.Empty;
}
