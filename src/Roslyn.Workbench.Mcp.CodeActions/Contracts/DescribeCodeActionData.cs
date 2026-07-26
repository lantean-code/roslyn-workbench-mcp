namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents the descriptor returned for one discovered code action.
/// </summary>
internal sealed record DescribeCodeActionData
{
    /// <summary>
    /// Gets the discovered action descriptor.
    /// </summary>
    public required CodeActionInfo Descriptor { get; init; }

    /// <summary>
    /// Gets the dynamic preflight context for the action.
    /// </summary>
    public CodeActionDescriptorContext Context { get; init; } = new();
}
