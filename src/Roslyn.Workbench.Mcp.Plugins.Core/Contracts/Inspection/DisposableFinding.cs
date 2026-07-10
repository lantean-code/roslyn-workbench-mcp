using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one disposable analysis finding.
/// </summary>
public sealed record DisposableFinding
{
    /// <summary>
    /// Gets the finding kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the associated local symbol.
    /// </summary>
    public SymbolReference? Symbol { get; init; }

    /// <summary>
    /// Gets the disposable type.
    /// </summary>
    public TypeInfo? Type { get; init; }

    /// <summary>
    /// Gets the finding location.
    /// </summary>
    public ResolvedLocation? Location { get; init; }

    /// <summary>
    /// Gets the finding message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
