using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class DescribeCodeActionTool : CodeActionQueryToolHandler<DescribeCodeActionRequest, DescribeCodeActionData>
{
    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "describe-code-action",
        Title = "Describe Code Action",
        Description = "Revalidates one discovered code action and returns its execution descriptor and preflight context.",
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new DescribeCodeActionTool());
    }

    protected override ValueTask<CodeActionExecutionResult<DescribeCodeActionData>> ExecuteCoreAsync(DescribeCodeActionRequest request, ICodeActionQueryContext context, CancellationToken cancellationToken)
    {
        return context.DescribeCodeActionAsync(request, cancellationToken);
    }
}
