namespace Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

/// <summary>
/// Identifies whether generated constructor parameters are required or optional.
/// </summary>
internal enum AddConstructorParametersKind
{
    /// <summary>
    /// Adds required constructor parameters.
    /// </summary>
    Required,

    /// <summary>
    /// Adds optional constructor parameters.
    /// </summary>
    Optional,
}
