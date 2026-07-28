namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

internal interface ICodeActionPolicy
{
    CodeActionPolicyDecision EvaluateProvider(string providerId);

    CodeActionPolicyDecision EvaluateAction(
        string providerId,
        CodeAction action);
}
