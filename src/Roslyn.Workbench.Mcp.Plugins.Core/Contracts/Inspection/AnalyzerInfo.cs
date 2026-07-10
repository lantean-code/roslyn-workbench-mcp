namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one analyzer referenced by a project or document.
/// </summary>
public sealed record AnalyzerInfo
{
    /// <summary>
    /// Gets the analyzer display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the analyzer assembly path, when available.
    /// </summary>
    public string? Path { get; init; }
}
