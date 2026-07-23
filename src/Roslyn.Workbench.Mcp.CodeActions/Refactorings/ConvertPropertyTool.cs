using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertPropertyTool : CodeActionMutationToolHandler<ConvertPropertyRequest>
{
    private const string _convertToFullProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider";
    private const string _useAutoPropertyProviderId = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider";
    private const string _useAutoPropertyAnalyzerTypeName = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer";
    private const string _useAutoPropertyDiagnosticId = "IDE0032";

    private readonly ICodeActionSelectionStager _selectionStager;
    private readonly ILocationCodeFixStager _locationFixStager;

    public ConvertPropertyTool(
        ICodeActionSelectionStager selectionStager,
        ILocationCodeFixStager locationFixStager)
    {
        _selectionStager = selectionStager;
        _locationFixStager = locationFixStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertPropertyRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Direction is not ConvertPropertyDirection.ToFull
            and not ConvertPropertyDirection.ToAutoWhenSafe)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "The requested property conversion direction is not supported.");

            return ValueTask.FromResult(rejection);
        }

        if (request.Direction == ConvertPropertyDirection.ToFull)
        {
            return _selectionStager.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                cancellationToken,
                context,
                _convertToFullProviderId,
                title: "Convert to full property");
        }

        var fixRequest = new LocationCodeFixRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = [_useAutoPropertyDiagnosticId],
            ProviderId = _useAutoPropertyProviderId,
            Title = "Use auto property",
            AnalyzerTypeName = _useAutoPropertyAnalyzerTypeName,
            SyntheticDiagnosticId = _useAutoPropertyDiagnosticId,
        };

        return _locationFixStager.StageLocationCodeFixAsync(fixRequest, context, cancellationToken);
    }
}
