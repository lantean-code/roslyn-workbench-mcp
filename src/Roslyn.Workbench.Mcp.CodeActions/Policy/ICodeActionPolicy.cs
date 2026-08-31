namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

/// <summary>
/// Determines whether Code Action providers and actions are safe for non-interactive execution.
/// </summary>
internal interface ICodeActionPolicy
{
    /// <summary>
    /// Determines whether a provider is eligible for discovery and execution.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The Code Action policy decision.</returns>
    CodeActionPolicyDecision EvaluateProvider(string providerId);

    /// <summary>
    /// Determines whether a discovered action is eligible for execution.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="action">The Code Action being inspected or executed.</param>
    /// <returns>The Code Action policy decision.</returns>
    CodeActionPolicyDecision EvaluateAction(
        string providerId,
        CodeAction action);
}
