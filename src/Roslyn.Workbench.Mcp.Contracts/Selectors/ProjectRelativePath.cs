namespace Roslyn.Workbench.Mcp.Contracts.Selectors;

/// <summary>
/// Represents a path relative to the owning project's canonical directory.
/// </summary>
public sealed record ProjectRelativePath
{
    /// <summary>
    /// Gets the relative path value.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
