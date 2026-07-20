using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionOperationService : ICodeActionOperationService
{
    private readonly ICodeActionDiagnosticService _diagnosticService;

    public CodeActionOperationService(ICodeActionDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> CreateMutationCandidateAsync(
        CodeAction action,
        string summary,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var operations = await action.GetOperationsAsync(
            context.CurrentSolution,
            new Progress<CodeAnalysisProgress>(),
            cancellationToken);

        if (!TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
        {
            var error = new CodeActionExecutionError
            {
                Code = "UnsupportedActionOperation",
                Message = "The selected action produced unsupported operations.",
            };

            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(error);
        }

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = applyChanges.ChangedSolution,
            Summary = summary,
        };

        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate);
    }

    public Task<CodeActionApplyResult> ApplyFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Document document,
        TextSpan originSpan,
        FixAllScope scope,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            document,
            scope is FixAllScope.ContainingMember or FixAllScope.ContainingType ? originSpan : null,
            provider,
            scope,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return ApplyFixAllCoreAsync(fixAllProvider, fixAllContext, cancellationToken);
    }

    public Task<CodeActionApplyResult> ApplyFixAllAsync(
        CodeFixProvider provider,
        FixAllProvider fixAllProvider,
        Project project,
        IReadOnlyList<string> diagnosticIds,
        string? equivalenceKey,
        string? syntheticDiagnosticId,
        CancellationToken cancellationToken)
    {
        var fixAllContext = new FixAllContext(
            project,
            provider,
            FixAllScope.Project,
            equivalenceKey,
            diagnosticIds,
            new WorkspaceFixAllDiagnosticProvider(_diagnosticService, diagnosticIds, syntheticDiagnosticId),
            cancellationToken);

        return ApplyFixAllCoreAsync(fixAllProvider, fixAllContext, cancellationToken);
    }

    private static async Task<CodeActionApplyResult> ApplyFixAllCoreAsync(
        FixAllProvider fixAllProvider,
        FixAllContext fixAllContext,
        CancellationToken cancellationToken)
    {
        var fixAllAction = await fixAllProvider.GetFixAsync(fixAllContext);
        if (fixAllAction is null)
        {
            return new CodeActionApplyResult
            {
                Rejection = FixAllUnavailable("The selected code fix could not produce a fix-all action."),
            };
        }

        var operations = await fixAllAction.GetOperationsAsync(
            fixAllContext.Solution,
            new Progress<CodeAnalysisProgress>(),
            cancellationToken);

        if (!TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
        {
            var error = new CodeActionExecutionError
            {
                Code = "UnsupportedActionOperation",
                Message = "The selected action produced unsupported operations.",
            };

            var rejection = CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(error);
            return new CodeActionApplyResult
            {
                Rejection = rejection,
            };
        }

        return new CodeActionApplyResult
        {
            CandidateSolution = applyChanges.ChangedSolution,
        };
    }

    private static bool TryGetSupportedApplyChangesOperation(
        IReadOnlyList<CodeActionOperation> operations,
        [NotNullWhen(true)]
        out ApplyChangesOperation? applyChanges)
    {
        applyChanges = null;

        foreach (var operation in operations)
        {
            if (operation is ApplyChangesOperation candidate)
            {
                if (applyChanges is not null)
                {
                    applyChanges = null;
                    return false;
                }

                applyChanges = candidate;
                continue;
            }

            if (!IsIgnorableAuxiliaryOperation(operation))
            {
                applyChanges = null;
                return false;
            }
        }

        return applyChanges is not null;
    }

    private static bool IsIgnorableAuxiliaryOperation(CodeActionOperation operation)
    {
        // Roslyn wrapping actions emit this bookkeeping operation alongside their single source mutation.
        return string.Equals(
            operation.GetType().FullName,
            "Microsoft.CodeAnalysis.Wrapping.WrapItemsAction+RecordCodeActionOperation",
            StringComparison.Ordinal);
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> FixAllUnavailable(string message)
    {
        var error = new CodeActionExecutionError
        {
            Code = "FixAllUnavailable",
            Message = message,
        };

        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(error);
    }
}
