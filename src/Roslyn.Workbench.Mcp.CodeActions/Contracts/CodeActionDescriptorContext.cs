namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Describes the dynamic preflight context for one discovered code action.
/// </summary>
public sealed record CodeActionDescriptorContext
{
    /// <summary>
    /// Gets the context kind.
    /// </summary>
    public CodeActionDescriptorContextKind Kind { get; init; }

    /// <summary>
    /// Gets simple name-oriented input hints.
    /// </summary>
    public IReadOnlyList<CodeActionNameOptionInfo>? NameOptions { get; init; }

    /// <summary>
    /// Gets selectable members for member-based actions.
    /// </summary>
    public IReadOnlyList<SymbolReference>? Members { get; init; }

    /// <summary>
    /// Gets the optional explanatory message when the action is unsupported.
    /// </summary>
    public string? Message { get; init; }
}
