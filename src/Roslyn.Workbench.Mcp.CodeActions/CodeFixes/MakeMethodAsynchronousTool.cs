using Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;

namespace Roslyn.Workbench.Mcp.CodeActions.CodeFixes;

internal sealed class MakeMethodAsynchronousTool : CodeActionMutationToolHandler<MakeMethodAsynchronousRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider";
    private const string _returnTaskTitle = "Make method async";
    private const string _stayVoidTitle = "Make method async (stay void)";

    private static readonly IReadOnlyList<string> _diagnosticIds = ["CS0246", "CS4032", "CS4033", "CS4034"];

    private readonly ILocationCodeFixStager _locationFixStager;

    public MakeMethodAsynchronousTool(ILocationCodeFixStager locationFixStager)
    {
        _locationFixStager = locationFixStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        MakeMethodAsynchronousRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var title = request.Strategy == MakeMethodAsynchronousStrategy.StayVoid
            ? _stayVoidTitle
            : _returnTaskTitle;

        var fixRequest = new LocationCodeFixRequest
        {
            Location = request.Location,
            ExpectedSnapshot = request.ExpectedSnapshot,
            DiagnosticIds = _diagnosticIds,
            ProviderId = _providerId,
            Title = title,
        };

        return _locationFixStager.StageLocationCodeFixAsync(fixRequest, context, cancellationToken);
    }
}
