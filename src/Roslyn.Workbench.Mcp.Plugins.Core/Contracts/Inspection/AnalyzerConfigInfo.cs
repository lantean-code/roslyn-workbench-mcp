namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents analyzer configuration inputs and effective options for one document.
/// </summary>
internal sealed record AnalyzerConfigInfo
{
    /// <summary>
    /// Gets the global analyzer-config file paths applied to the document.
    /// </summary>
    public IReadOnlyList<string> GlobalConfigPaths { get; init; } = [];

    /// <summary>
    /// Gets the editor-config file paths applied to the document.
    /// </summary>
    public IReadOnlyList<string> EditorConfigPaths { get; init; } = [];

    /// <summary>
    /// Gets the effective analyzer-config options for the document.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
