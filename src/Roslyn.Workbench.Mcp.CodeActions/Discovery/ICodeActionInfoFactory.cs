using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionInfoFactory
{
    bool TryCreate(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        TextSpan span,
        CodeActionDescriptorEntry descriptor,
        [NotNullWhen(true)] out CodeActionInfo? info);

    CodeActionInfo CreateFromReference(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        CodeActionDescriptorEntry descriptor,
        CodeActionReference reference);
}
