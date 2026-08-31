namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

/// <summary>
/// Excludes Code Action providers and actions that require unsupported interactive or external capabilities.
/// </summary>
internal sealed class CodeActionPolicy : ICodeActionPolicy
{
    /// <summary>
    /// Determines whether a provider is eligible for discovery and execution.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>The Code Action policy decision.</returns>
    public CodeActionPolicyDecision EvaluateProvider(string providerId)
    {
        if (CodeActionExclusions.ProviderReasons.TryGetValue(providerId, out var reasonCode))
        {
            return CodeActionPolicyDecision.Excluded(reasonCode);
        }

        return CodeActionPolicyDecision.Allowed();
    }

    /// <summary>
    /// Determines whether a discovered action is eligible for execution.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="action">The Code Action being inspected or executed.</param>
    /// <returns>The Code Action policy decision.</returns>
    public CodeActionPolicyDecision EvaluateAction(
        string providerId,
        CodeAction action)
    {
        var providerDecision = EvaluateProvider(providerId);
        if (!providerDecision.IsAllowed)
        {
            return providerDecision;
        }

        if (action is CodeActionWithOptions)
        {
            return CodeActionPolicyDecision.Excluded(CodeActionExclusions.OptionsRequired);
        }

        return CodeActionPolicyDecision.Allowed();
    }
}
