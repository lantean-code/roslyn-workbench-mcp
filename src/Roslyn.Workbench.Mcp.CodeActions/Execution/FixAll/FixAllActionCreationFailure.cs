namespace Roslyn.Workbench.Mcp.CodeActions.Execution.FixAll;

/// <summary>
/// Explains why a Fix All provider did not produce an action.
/// </summary>
internal sealed record FixAllActionCreationFailure
{
    /// <summary>
    /// Gets the user-facing failure explanation.
    /// </summary>
    public required string Message { get; init; }
}
