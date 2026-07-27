namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

/// <summary>
/// Identifies the asynchronous method shape to stage.
/// </summary>
internal enum MakeMethodAsynchronousStrategy
{
    /// <summary>
    /// Converts the method to return a task and applies Roslyn's asynchronous naming update when applicable.
    /// </summary>
    ReturnTask,

    /// <summary>
    /// Retains a void return type while making the method asynchronous.
    /// </summary>
    StayVoid,
}
