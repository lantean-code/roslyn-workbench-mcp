namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

/// <summary>
/// Describes why a Code Action could not produce a supported candidate solution.
/// </summary>
internal sealed record CodeActionApplyFailure
{
    /// <summary>
    /// Gets the stable failure category.
    /// </summary>
    public required CodeActionApplyFailureKind Kind { get; init; }

    /// <summary>
    /// Gets the user-facing failure explanation.
    /// </summary>
    public required string Message { get; init; }
}
