using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ExtractMethodTool : CodeActionMutationToolHandler<ExtractMethodRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider";
    private const string MethodTitle = "Extract method";
    private const string MethodEquivalenceKey = "Extract_method";
    private const string LocalFunctionTitle = "Extract local function";
    private const string LocalFunctionEquivalenceKey = "Extract_local_function";

    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "extract-method",
        Title = "Extract Method",
        Description = "Extracts a selected statement or expression block through Roslyn refactoring composition.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new ExtractMethodTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(ExtractMethodRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            return ValueTask.FromResult(ToolExecutionHelpers.Rejected<WorkspaceMutationProposal>("InvalidRequest", "A location selector is required."));
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
