namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one analyzer referenced by a project or document.
/// </summary>
internal sealed record AnalyzerInfo
{
    /// <summary>
    /// Gets the analyzer display name.
    /// </summary>
    [Description("The analyzer display name.")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the analyzer assembly path, when available.
    /// </summary>
    [Description("The analyzer assembly path, when available.")]
    public string? Path { get; init; }
}
