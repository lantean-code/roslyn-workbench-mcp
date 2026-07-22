namespace Roslyn.Workbench.Mcp.CodeActions.Contracts;

/// <summary>
/// Represents the descriptor returned for one discovered code action.
/// </summary>
public sealed record DescribeCodeActionData
{
    /// <summary>
    /// Gets the discovered action descriptor.
    /// </summary>
    public CodeActionInfo Descriptor { get; init; } = new();

    /// <summary>
    /// Gets the dynamic preflight context for the action.
    /// </summary>
    public CodeActionDescriptorContext Context { get; init; } = new();
}
