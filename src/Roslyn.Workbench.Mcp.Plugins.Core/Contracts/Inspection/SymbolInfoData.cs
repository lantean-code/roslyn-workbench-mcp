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
    [Description("The resolved symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the symbol accessibility.
    /// </summary>
    [Description("The symbol accessibility.")]
    public required string Accessibility { get; init; }

    /// <summary>
    /// Gets the symbol modifiers.
    /// </summary>
    [Description("The symbol modifiers.")]
    [UnconditionalSuppressMessage(
       "RoslynWorkbench.PluginAuthoring",
       "RWMCP014:Bound agent-facing query collections",
       Justification = "Roslyn symbol modifiers are a fixed, small language-defined set rather than an unbounded result collection.")]
    public IReadOnlyList<string> Modifiers { get; init; } = [];

    /// <summary>
    /// Gets the containing or declared type information, when applicable.
    /// </summary>
    [Description("The containing or declared type information, when applicable.")]
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the callable parameter information, when applicable.
    /// </summary>
    [Description("The callable parameter information, when applicable.")]
    public BoundedCollection<ParameterInfo>? Parameters { get; init; }

    /// <summary>
    /// Gets the return type information, when applicable.
    /// </summary>
    [Description("The return type information, when applicable.")]
    public TypeInfo? ReturnType { get; init; }

    /// <summary>
    /// Gets the XML documentation text, when requested.
    /// </summary>
    [Description("The XML documentation text, when requested.")]
    public string? Documentation { get; init; }

    /// <summary>
    /// Gets the source declarations for the symbol.
    /// </summary>
    [Description("The source declarations for the symbol.")]
    public BoundedCollection<ResolvedLocation> Declarations { get; init; } = BoundedCollection.Empty<ResolvedLocation>();
}
