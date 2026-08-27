using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-symbol-info.
/// </summary>
internal sealed record SymbolInfoData : IQueryResponse
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
    [UnconditionalSuppressMessage(
        "RoslynWorkbench.PluginAuthoring",
        "RWMCP014:Bound agent-facing query collections",
        Justification = "Roslyn symbol modifiers are a fixed, small language-defined set rather than an unbounded result collection.")]
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    /// <summary>
    /// Gets the containing or declared type information, when applicable.
    /// </summary>
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the callable parameter information, when applicable.
    /// </summary>
    public BoundedCollection<ParameterInfo>? Parameters { get; init; }

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
    public BoundedCollection<ResolvedLocation> Declarations { get; init; } = BoundedCollection.Empty<ResolvedLocation>();
}
