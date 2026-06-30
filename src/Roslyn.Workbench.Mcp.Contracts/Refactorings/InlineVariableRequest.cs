using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Requests inlining of a local variable through Roslyn refactoring composition.
/// </summary>
public sealed record InlineVariableRequest
{
    /// <summary>
    /// Gets the symbol selector for the local variable to inline.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether the declaration should be removed after inlining.
    /// </summary>
    public bool RemoveDeclaration { get; init; } = true;

    /// <summary>
    /// Gets the expected snapshot for the selected symbol.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
