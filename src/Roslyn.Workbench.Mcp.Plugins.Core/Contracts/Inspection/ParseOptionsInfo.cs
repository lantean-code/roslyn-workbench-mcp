namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents selected Roslyn parse options for a document.
/// </summary>
internal sealed record ParseOptionsInfo
{
    /// <summary>
    /// Gets the language name.
    /// </summary>
    [Description("The language name.")]
    public required string Language { get; init; }

    /// <summary>
    /// Gets the language version.
    /// </summary>
    [Description("The language version, when available for the project language.")]
    public string? LanguageVersion { get; init; }

    /// <summary>
    /// Gets the documentation mode.
    /// </summary>
    [Description("The documentation mode.")]
    public required string DocumentationMode { get; init; }

    /// <summary>
    /// Gets the effective preprocessor symbols.
    /// </summary>
    [Description("The effective preprocessor symbols.")]
    public IReadOnlyList<string> PreprocessorSymbols { get; init; } = [];
}
