namespace Roslyn.Workbench.Mcp.Contracts.Refactorings;

/// <summary>
/// Selects the add-await replay variant.
/// </summary>
public enum AddAwaitKind
{
    /// <summary>
    /// Adds <c>await</c> only.
    /// </summary>
    Await = 0,

    /// <summary>
    /// Adds <c>await</c> plus <c>ConfigureAwait(false)</c>.
    /// </summary>
    AwaitConfigureAwaitFalse = 1,
}
