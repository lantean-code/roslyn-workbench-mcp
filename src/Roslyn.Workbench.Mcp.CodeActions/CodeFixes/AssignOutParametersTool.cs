using Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class AssignOutParametersTool : CodeActionMutationToolHandler<FixedCompilerCodeFixRequest>
{
    private const string _atStartProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAtStartCodeFixProvider";
    private const string _aboveReturnProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAboveReturnCodeFixProvider";
    private const string _diagnosticId = "CS0177";

    private readonly ILocationCodeFixStager _locationFixStager;

    public AssignOutParametersTool(ILocationCodeFixStager locationFixStager)
    {
        _locationFixStager = locationFixStager;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        FixedCompilerCodeFixRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var atStartRequest = CreateStageRequest(request, _atStartProviderId);
        var atStartResult = await _locationFixStager.StageLocationCodeFixAsync(
            atStartRequest,
            context,
            cancellationToken);

        if (!ShouldTryAboveReturnProvider(atStartResult))
        {
            return atStartResult;
        }

        var aboveReturnRequest = CreateStageRequest(request, _aboveReturnProviderId);
        return await _locationFixStager.StageLocationCodeFixAsync(
            aboveReturnRequest,
            context,
            cancellationToken);
    }

    private static LocationCodeFixRequest CreateStageRequest(
        FixedCompilerCodeFixRequest request,
        string providerId)
    {
        return new LocationCodeFixRequest
        {
            Location = request.Location,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = [_diagnosticId],
            ProviderId = providerId,
        };
    }

    private static bool ShouldTryAboveReturnProvider(
        CodeActionExecutionResult<WorkspaceMutationCandidate> result)
    {
        return result.Outcome == CodeActionExecutionOutcome.Rejected
            && string.Equals(result.Error?.Code, "CodeFixUnavailable", StringComparison.Ordinal);
    }
}
