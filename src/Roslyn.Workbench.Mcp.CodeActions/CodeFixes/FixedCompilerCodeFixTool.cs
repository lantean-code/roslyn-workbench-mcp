using Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal abstract class FixedCompilerCodeFixTool : CodeActionMutationToolHandler<FixedCompilerCodeFixRequest>
{
    private readonly ILocationCodeFixStager _locationFixStager;
    private readonly string _providerId;
    private readonly IReadOnlyList<string> _diagnosticIds;

    protected FixedCompilerCodeFixTool(
        ILocationCodeFixStager locationFixStager,
        string providerId,
        string diagnosticId)
        : this(locationFixStager, providerId, [diagnosticId])
    {
    }

    protected FixedCompilerCodeFixTool(
        ILocationCodeFixStager locationFixStager,
        string providerId,
        IReadOnlyList<string> diagnosticIds)
    {
        _locationFixStager = locationFixStager;
        _providerId = providerId;
        _diagnosticIds = diagnosticIds;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        FixedCompilerCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var fixRequest = new LocationCodeFixRequest
        {
            Location = request.Location,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = _diagnosticIds,
            ProviderId = _providerId,
        };

        return _locationFixStager.StageLocationCodeFixAsync(fixRequest, context, cancellationToken);
    }
}
