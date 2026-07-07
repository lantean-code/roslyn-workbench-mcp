using Roslyn.Workbench.Mcp.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Refactorings;

internal sealed class ExtractMethodTool : MutationToolHandler<ExtractMethodRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider";
    private const string MethodTitle = "Extract method";
    private const string MethodEquivalenceKey = "Extract_method";
    private const string LocalFunctionTitle = "Extract local function";
    private const string LocalFunctionEquivalenceKey = "Extract_local_function";

    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "extract-method",
        Title = "Extract Method",
        Description = "Extracts a selected statement or expression block through Roslyn refactoring composition.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ExtractMethodTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(ExtractMethodRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            return ValueTask.FromResult(context.ToolExecutionServices.ResultShaper.Rejected<MutationProposal>("InvalidRequest", "A location selector is required."));
        }

        var (title, equivalenceKey) = request.TargetKind switch
        {
            ExtractMethodTargetKind.LocalFunction => (LocalFunctionTitle, LocalFunctionEquivalenceKey),
            _ => (MethodTitle, MethodEquivalenceKey),
        };

        return context.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = equivalenceKey,
        }, cancellationToken);
    }
}
