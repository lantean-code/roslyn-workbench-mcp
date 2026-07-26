namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Conversions;

/// <summary>
/// Requests one future Roslyn-backed property conversion at a selected property declaration.
/// </summary>
internal sealed record ConvertPropertyRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// Gets the selected property declaration to rewrite.
    /// </summary>
    public required LocationSelector Selection { get; init; }

    /// <summary>
    /// Gets the property-conversion direction to stage.
    /// </summary>
    public required ConvertPropertyDirection Direction { get; init; }
}
