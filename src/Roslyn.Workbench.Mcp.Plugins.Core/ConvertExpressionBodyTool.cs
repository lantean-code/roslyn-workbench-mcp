using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ConvertExpressionBodyTool : MutationToolHandler<LocationRefactoringRequest, MutationProposal>
{
    private const string UseExpressionBodyProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider";
    private const string UseExpressionBodyForLambdaProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-expression-body",
        Title = "Convert Expression Body",
        Description = "Stages a supported Roslyn block-body or expression-body conversion at the selected declaration.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertExpressionBodyTool());
    }

    protected override async ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(LocationRefactoringRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        var result = await ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            UseExpressionBodyProviderId).ConfigureAwait(false);
        if (!ShouldTryLambdaProvider(result))
        {
            return result;
        }

        return await ToolExecutionHelpers.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            cancellationToken,
            UseExpressionBodyForLambdaProviderId).ConfigureAwait(false);
    }

    private static bool ShouldTryLambdaProvider(PluginExecutionResult<MutationProposal> result)
    {
        return result.Outcome == ToolOutcome.Rejected
            && string.Equals(result.Error?.Code, "CodeActionUnavailable", StringComparison.Ordinal);
    }
}
