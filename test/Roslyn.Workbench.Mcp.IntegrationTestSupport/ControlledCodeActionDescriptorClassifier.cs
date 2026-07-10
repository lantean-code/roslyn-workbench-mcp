using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class ControlledCodeActionDescriptorClassifier
{
    private const string _refactoringProviderId = "Roslyn.Workbench.Mcp.IntegrationTestSupport.TestRefactoringProvider";
    private const string _codeFixProviderId = "Roslyn.Workbench.Mcp.IntegrationTestSupport.TestCodeFixProvider";

    public static CodeActionDescriptorEntry? Classify(CodeAction action, string providerId, string title)
    {
        if (string.Equals(providerId, _codeFixProviderId, StringComparison.Ordinal))
        {
            return Replay();
        }

        if (!string.Equals(providerId, _refactoringProviderId, StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(title, "Change signature test refactoring", StringComparison.Ordinal))
        {
            return new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Parameterised,
                DescribeTool = "describe-code-action",
                Requirements = ["requires-dedicated-tool", "requires-preflight-description"],
                ContextKind = CodeActionDescriptorContextKind.SignaturePlan,
            };
        }

        if (string.Equals(title, "Extract method test refactoring", StringComparison.Ordinal))
        {
            return new CodeActionDescriptorEntry
            {
                IsVisible = false,
                ExecutionMode = CodeActionExecutionMode.Unsupported,
                ContextKind = CodeActionDescriptorContextKind.Unsupported,
            };
        }

        if (action is CodeActionWithOptions)
        {
            return new CodeActionDescriptorEntry
            {
                ExecutionMode = CodeActionExecutionMode.Unsupported,
                UnsupportedReasonCode = "UnsupportedCodeActionWithOptions",
                Requirements = ["not-executable"],
                ContextKind = CodeActionDescriptorContextKind.Unsupported,
                Message = "The selected action requires unsupported Roslyn option gathering.",
            };
        }

        return Replay();
    }

    private static CodeActionDescriptorEntry Replay()
    {
        return new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Replay,
            Requirements = ["deterministic-replay"],
            ContextKind = CodeActionDescriptorContextKind.None,
        };
    }
}
