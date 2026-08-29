namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents analyzer configuration inputs and effective options for one document.
/// </summary>
internal sealed record AnalyzerConfigInfo
{
    /// <summary>
    /// Gets the global analyzer-config file paths applied to the document.
    /// </summary>
    [Description("The global analyzer-config file paths applied to the document.")]
    public IReadOnlyList<string> GlobalConfigPaths { get; init; } = [];

    /// <summary>
    /// Gets the editor-config file paths applied to the document.
    /// </summary>
    [Description("The editor-config file paths applied to the document.")]
    public IReadOnlyList<string> EditorConfigPaths { get; init; } = [];

    /// <summary>
    /// Gets the effective analyzer-config options for the document.
    /// </summary>
    [Description("The effective analyzer-config options for the document.")]
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
