using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class CodeActionDescriptorRegistry
{
    private const string _testRefactoringProviderId = "Roslyn.Workbench.Mcp.TestSupport.TestRefactoringProvider";
    private const string _testCodeFixProviderId = "Roslyn.Workbench.Mcp.TestSupport.TestCodeFixProvider";
    private const string _addImportCodeFixProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";

    public CodeActionDescriptorEntry Classify(CodeAction action, string providerId, string title)
    {
        ArgumentNullException.ThrowIfNull(action);

        var normalizedTitle = title.Trim();

        if (string.Equals(providerId, _testRefactoringProviderId, StringComparison.Ordinal))
        {
            return ClassifyTestRefactoring(action, normalizedTitle);
        }

        if (string.Equals(providerId, _testCodeFixProviderId, StringComparison.Ordinal))
        {
            return Replay();
        }

        if (string.Equals(providerId, _addImportCodeFixProviderId, StringComparison.Ordinal)
            && Contains(normalizedTitle, "Add missing using"))
        {
            return Replay();
        }

        if (Contains(normalizedTitle, "Remove unnecessary usings") || Contains(normalizedTitle, "Remove unused usings"))
        {
            return Replay();
        }

        if (!string.IsNullOrWhiteSpace(providerId)
            && BuiltInCodeActionLedger.TryGetFamily(providerId, out var family))
        {
            return family.State switch
            {
                BuiltInCodeActionSupportState.SupportedReplay => Replay(),
                BuiltInCodeActionSupportState.SupportedParameterised => Parameterised(family.ExecutorTool, CodeActionDescriptorContextKind.None),
                _ => Hidden(),
            };
        }

        return Hidden();
    }

    private static CodeActionDescriptorEntry ClassifyTestRefactoring(CodeAction action, string normalizedTitle)
    {
        if (string.Equals(normalizedTitle, "Change signature test refactoring", StringComparison.Ordinal))
        {
            return Parameterised(null, CodeActionDescriptorContextKind.SignaturePlan);
        }

        if (string.Equals(normalizedTitle, "Extract method test refactoring", StringComparison.Ordinal))
        {
            return Hidden();
        }

        if (action is CodeActionWithOptions)
        {
            return Unsupported("UnsupportedCodeActionWithOptions", "The selected action requires unsupported Roslyn option gathering.");
        }

        return Replay();
    }

    private static CodeActionDescriptorEntry Parameterised(string? executorTool, CodeActionDescriptorContextKind contextKind)
    {
        return new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Parameterised,
            ExecutorTool = executorTool,
            DescribeTool = "describe-code-action",
            Requirements = ["requires-dedicated-tool", "requires-preflight-description"],
            ContextKind = contextKind,
        };
    }

    private static CodeActionDescriptorEntry Unsupported(string reasonCode, string message)
    {
        return new CodeActionDescriptorEntry
        {
            ExecutionMode = CodeActionExecutionMode.Unsupported,
            UnsupportedReasonCode = reasonCode,
            Requirements = ["not-executable"],
            ContextKind = CodeActionDescriptorContextKind.Unsupported,
            Message = message,
        };
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

    private static CodeActionDescriptorEntry Hidden()
    {
        return new CodeActionDescriptorEntry
        {
            IsVisible = false,
            ExecutionMode = CodeActionExecutionMode.Unsupported,
            ContextKind = CodeActionDescriptorContextKind.Unsupported,
        };
    }

    private static bool Contains(string title, string fragment)
    {
        return title.Contains(fragment, StringComparison.OrdinalIgnoreCase);
    }
}
