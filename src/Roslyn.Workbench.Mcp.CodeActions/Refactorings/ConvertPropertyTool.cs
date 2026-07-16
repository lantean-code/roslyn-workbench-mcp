using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertPropertyTool : CodeActionMutationToolHandler<ConvertPropertyRequest>
{
    private const string ConvertToFullProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider";
    private const string UseAutoPropertyProviderId = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider";
    private const string UseAutoPropertyAnalyzerTypeName = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer";
    private const string UseAutoPropertyDiagnosticId = "IDE0032";

    private readonly ICodeActionReplayService _replayService;
    private readonly ICodeActionLocationFixService _locationFixService;

    public ConvertPropertyTool(
        ICodeActionReplayService replayService,
        ICodeActionLocationFixService locationFixService)
    {
        _replayService = replayService;
        _locationFixService = locationFixService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertPropertyRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return request.Direction switch
        {
            ConvertPropertyDirection.ToFull => _replayService.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                cancellationToken,
                context,
                ConvertToFullProviderId,
                title: "Convert to full property"),
            ConvertPropertyDirection.ToAutoWhenSafe => _locationFixService.StageLocationCodeFixAsync(new LocationCodeFixRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                DiagnosticIds = [UseAutoPropertyDiagnosticId],
                ProviderId = UseAutoPropertyProviderId,
                Title = "Use auto property",
                AnalyzerTypeName = UseAutoPropertyAnalyzerTypeName,
                SyntheticDiagnosticId = UseAutoPropertyDiagnosticId,
            }, context, cancellationToken),
            _ => ValueTask.FromResult(CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("InvalidRequest", "The requested property conversion direction is not supported.")),
        };
    }
}
