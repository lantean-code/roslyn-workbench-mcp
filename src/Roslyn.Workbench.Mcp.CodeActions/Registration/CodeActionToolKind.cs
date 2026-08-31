namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Defines the supported Code Action tool kind values.
/// </summary>
internal enum CodeActionToolKind
{
    /// <summary>
    /// Identifies a read-only query tool.
    /// </summary>
    Query,
    /// <summary>
    /// Identifies a tool that stages workspace changes.
    /// </summary>
    Mutation,
}
