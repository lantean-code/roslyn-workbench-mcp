namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Requests one supported Roslyn foreach or LINQ conversion through refactoring composition.
/// </summary>
internal sealed record ConvertForeachLinqRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected foreach statement or query expression to convert.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the conversion variant to stage.
    /// </summary>
    public required ConvertForeachLinqKind ConversionKind { get; init; }
}
