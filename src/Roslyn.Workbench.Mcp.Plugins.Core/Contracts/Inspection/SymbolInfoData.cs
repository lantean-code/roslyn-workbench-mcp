namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-info.
/// </summary>
internal sealed record SymbolInfoData
{
    /// <summary>
    /// Gets the resolved symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the symbol accessibility.
    /// </summary>
    public string Accessibility { get; init; } = string.Empty;

    /// <summary>
    /// Gets the symbol modifiers.
    /// </summary>
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    /// <summary>
    /// Gets the containing or declared type information, when applicable.
    /// </summary>
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the callable parameter information, when applicable.
    /// </summary>
    public IReadOnlyList<ParameterInfo>? Parameters { get; init; }

    /// <summary>
    /// Gets the return type information, when applicable.
    /// </summary>
    public TypeInfo? ReturnType { get; init; }

    /// <summary>
    /// Gets the XML documentation text, when requested.
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// Gets the source declarations for the symbol.
    /// </summary>
    public IReadOnlyList<ResolvedLocation> Declarations { get; init; } = [];
}
