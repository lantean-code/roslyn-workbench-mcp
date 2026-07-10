using Roslyn.Workbench.Mcp.CodeActions.Contracts;
namespace Roslyn.Workbench.Mcp.CodeActions.Resolution;

internal sealed class CodeActionDescriptorRegistry : ICodeActionDescriptorRegistry
{
    private const string _addImportCodeFixProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider";
    private readonly IReadOnlyList<CodeActionDescriptorOverride> _overrides;

    public CodeActionDescriptorRegistry()
        : this([])
    {
    }

    internal CodeActionDescriptorRegistry(IReadOnlyList<CodeActionDescriptorOverride> overrides)
    {
        _overrides = overrides;
    }

    public CodeActionDescriptorEntry Classify(CodeAction action, string providerId, string title)
    {
        ArgumentNullException.ThrowIfNull(action);

        var normalizedTitle = title.Trim();

        foreach (var descriptorOverride in _overrides)
        {
            var overriddenEntry = descriptorOverride(action, providerId, normalizedTitle);
            if (overriddenEntry is not null)
            {
                return overriddenEntry;
            }
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
