using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertForeachLinqTool : CodeActionMutationToolHandler<ConvertForeachLinqRequest>
{
    private const string _forEachToLinqProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider";
    private const string _linqToForEachProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider";

    private readonly ICodeActionSelectionStager _selectionStager;

    public ConvertForeachLinqTool(ICodeActionSelectionStager selectionStager)
    {
        _selectionStager = selectionStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertForeachLinqRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (request.Selection is null)
        {
            var rejection = CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "A location selector is required.");

            return ValueTask.FromResult(rejection);
        }

        var replayRequest = request.ConversionKind switch
        {
            ConvertForeachLinqKind.ForeachToCallForm => new ReplayCodeActionRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                ProviderId = _forEachToLinqProviderId,
                Title = "Convert to LINQ call form",
                EquivalenceKey = "Convert_to_linq_call_form",
            },
            ConvertForeachLinqKind.LinqToForeach => new ReplayCodeActionRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                ProviderId = _linqToForEachProviderId,
                Title = "Convert to foreach",
                EquivalenceKey = "Convert_to_foreach",
            },
            _ => new ReplayCodeActionRequest
            {
                Location = request.Selection,
                ExpectedSnapshot = request.ExpectedSnapshot,
                ProviderId = _forEachToLinqProviderId,
                Title = "Convert to LINQ",
                EquivalenceKey = "Convert_to_linq",
            },
        };

        return _selectionStager.StageReplayCodeActionAsync(replayRequest, context, cancellationToken);
    }
}
