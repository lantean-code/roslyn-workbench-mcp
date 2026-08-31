namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Converts discovered actions into published items backed by short-lived references.
/// </summary>
internal interface ICodeActionInfoFactory
{
    /// <summary>
    /// Creates a published action item and retains the recipe needed to rediscover it.
    /// </summary>
    /// <param name="action">The discovered leaf action to publish.</param>
    /// <param name="context">The current Code Action execution context.</param>
    /// <param name="document">The document in which the action was discovered.</param>
    /// <param name="location">The canonical source location of the action.</param>
    /// <returns>The published item or a categorized reason it could not be created.</returns>
    CodeActionInfoCreationResult Create(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        ResolvedLocation location);
}
