using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;
using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class OrganizeImportsTool : CodeActionMutationToolHandler<OrganizeImportsRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsCodeRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public OrganizeImportsTool(
        ICodeActionSelectionStager selectionStager,
        ICodeActionToolRequestResolver requestResolver)
    {
        _selectionStager = selectionStager;
        _requestResolver = requestResolver;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        OrganizeImportsRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        var snapshotRejection = _requestResolver.ValidateSnapshot<WorkspaceMutationCandidate>(
            context,
            request.ExpectedSnapshot);

        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var documentResolution = context.WorkspaceResolver.ResolveDocument(request.Document);
        if (!documentResolution.IsResolved)
        {
            return RejectFromStatus<WorkspaceMutationCandidate>(
                documentResolution.Status,
                "Document",
                "document");
        }

        var root = await documentResolution.Value.GetSyntaxRootAsync(cancellationToken);
        if (root is null)
        {
            return Rejected<WorkspaceMutationCandidate>(
                "DocumentNotFound",
                "The selected document does not have a syntax root.",
                RequiredAction.ResolveTargetAgain);
        }

        var importNode = root.DescendantNodes()
            .FirstOrDefault(static node => node is UsingDirectiveSyntax or ExternAliasDirectiveSyntax);

        if (importNode is null)
        {
            return CodeActionExecutionResult.NoChange<WorkspaceMutationCandidate>();
        }

        var selection = new LocationSelector
        {
            Span = new TextSpanSelector
            {
                Document = request.Document,
                Start = importNode.SpanStart,
                Length = 0,
            },
        };

        return await _selectionStager.StageSelectionAsync(
            selection,
            request.ExpectedSnapshot,
            cancellationToken,
            context,
            _providerId,
            title: "Sort Usings");
    }
}
