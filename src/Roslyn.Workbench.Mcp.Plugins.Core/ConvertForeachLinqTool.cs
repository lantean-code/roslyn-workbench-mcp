using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ConvertForeachLinqTool : MutationToolHandler<ConvertForeachLinqRequest, MutationProposal>
{
    private const string ForEachToLinqProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider";
    private const string LinqToForEachProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "convert-foreach-linq",
        Title = "Convert Foreach LINQ",
        Description = "Stages one supported Roslyn foreach or LINQ conversion through refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ConvertForeachLinqTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(ConvertForeachLinqRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            return ValueTask.FromResult(ToolExecutionHelpers.Rejected<MutationProposal>("InvalidRequest", "A location selector is required."));
        }

        var replayRequest = request.ConversionKind switch
        {
            ConvertForeachLinqKind.ForeachToCallForm => new ReplayCodeActionRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                ProviderId = ForEachToLinqProviderId,
                Title = "Convert to LINQ call form",
                EquivalenceKey = "Convert_to_linq_call_form",
            },
            ConvertForeachLinqKind.LinqToForeach => new ReplayCodeActionRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                ProviderId = LinqToForEachProviderId,
                Title = "Convert to foreach",
                EquivalenceKey = "Convert_to_foreach",
            },
            _ => new ReplayCodeActionRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                ProviderId = ForEachToLinqProviderId,
                Title = "Convert to LINQ",
                EquivalenceKey = "Convert_to_linq",
            },
        };

        return context.CodeActionService.StageReplayCodeActionAsync(replayRequest, context, cancellationToken);
    }
}
