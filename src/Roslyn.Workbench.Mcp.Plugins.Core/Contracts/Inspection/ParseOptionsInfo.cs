namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents selected Roslyn parse options for a document.
/// </summary>
internal sealed record ParseOptionsInfo
{
    /// <summary>
    /// Gets the language name.
    /// </summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>
    /// Gets the language version.
    /// </summary>
    public string LanguageVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets the documentation mode.
    /// </summary>
    public string DocumentationMode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the effective preprocessor symbols.
    /// </summary>
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = [];
}
