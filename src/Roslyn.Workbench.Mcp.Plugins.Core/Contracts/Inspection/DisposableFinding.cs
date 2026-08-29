namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one disposable analysis finding.
/// </summary>
internal sealed record DisposableFinding
{
    /// <summary>
    /// Gets the finding kind.
    /// </summary>
    [Description("The finding kind.")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated local symbol.
    /// </summary>
    [Description("The associated local symbol.")]
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the disposable type.
    /// </summary>
    [Description("The disposable type.")]
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the finding location.
    /// </summary>
    [Description("The finding location.")]
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the finding message.
    /// </summary>
    [Description("The finding message.")]
    public string Message { get; init; } = string.Empty;
}
