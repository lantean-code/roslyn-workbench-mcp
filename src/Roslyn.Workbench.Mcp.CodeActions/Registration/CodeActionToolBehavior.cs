namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Describes MCP behavior hints for a Code Action tool.
/// </summary>
internal sealed record CodeActionToolBehavior
{
    /// <summary>
    /// Gets a value indicating whether the tool can make destructive changes.
    /// </summary>
    public bool Destructive { get; init; }

    /// <summary>
    /// Gets a value indicating whether repeated calls with the same arguments have the same effect.
    /// </summary>
    public bool Idempotent { get; init; }
}
