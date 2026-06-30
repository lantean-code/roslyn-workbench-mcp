using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DescribeCodeActionTool : QueryToolHandler<DescribeCodeActionRequest, DescribeCodeActionData>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "describe-code-action",
        Title = "Describe Code Action",
        Description = "Revalidates one discovered code action and returns its execution descriptor and preflight context.",
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(_metadata, new DescribeCodeActionTool());
    }

    protected override ValueTask<PluginExecutionResult<DescribeCodeActionData>> ExecuteCoreAsync(DescribeCodeActionRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        return context.CodeActionService.DescribeCodeActionAsync(request, context, cancellationToken);
    }
}
