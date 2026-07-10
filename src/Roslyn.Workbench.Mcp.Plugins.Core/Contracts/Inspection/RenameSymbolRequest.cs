using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to rename a resolved symbol.
/// </summary>
public sealed record RenameSymbolRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the new symbol name.
    /// </summary>
    public string NewName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether overloads should also be renamed.
    /// </summary>
    public bool RenameOverloads { get; init; }

    /// <summary>
    /// Gets a value indicating whether implementations should also be renamed.
    /// </summary>
    public bool RenameImplementations { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the containing file should be renamed for type symbols.
    /// </summary>
    public bool RenameFile { get; init; }

    /// <summary>
    /// Gets the expected snapshot for the selected symbol.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
