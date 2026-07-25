namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to rename a resolved symbol.
/// </summary>
internal sealed record RenameSymbolRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the new symbol name.
    /// </summary>
    public required string NewName { get; init; }

    /// <summary>
    /// Gets a value indicating whether overloads should also be renamed.
    /// </summary>
    public bool RenameOverloads { get; init; }

    /// <summary>
    /// Gets a value indicating whether matching identifiers in string literals should also be renamed.
    /// </summary>
    public bool RenameInStrings { get; init; }

    /// <summary>
    /// Gets a value indicating whether matching identifiers in comments should also be renamed.
    /// </summary>
    public bool RenameInComments { get; init; }

    /// <summary>
    /// Gets a value indicating whether the containing file should be renamed for type symbols.
    /// </summary>
    public bool RenameFile { get; init; }
}
