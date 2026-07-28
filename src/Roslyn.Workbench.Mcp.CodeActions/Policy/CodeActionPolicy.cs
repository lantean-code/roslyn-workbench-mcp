namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

internal sealed class CodeActionPolicy : ICodeActionPolicy
{
    public CodeActionPolicyDecision EvaluateProvider(string providerId)
    {
        if (CodeActionExclusions.ProviderReasons.TryGetValue(providerId, out var reasonCode))
        {
            return CodeActionPolicyDecision.Excluded(reasonCode);
        }

        return CodeActionPolicyDecision.Allowed();
    }

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
            return CodeActionPolicyDecision.Excluded(CodeActionExclusions._optionsRequired);
        }

        return CodeActionPolicyDecision.Allowed();
    }
}
