using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

/// <summary>
/// Evaluates a Code Action while permitting only one source-changing operation and known bookkeeping operations.
/// </summary>
internal sealed class CodeActionEvaluator : ICodeActionEvaluator
{
    /// <summary>
    /// Evaluates a Code Action and accepts only a single solution-changing operation.
    /// </summary>
    /// <param name="action">The Code Action to evaluate.</param>
    /// <param name="solution">The solution against which the action was resolved.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the changed solution or an unsupported-operation failure.</returns>
    public async ValueTask<CodeActionApplyResult> EvaluateAsync(
        CodeAction action,
        Solution solution,
        CancellationToken cancellationToken)
    {
        var operations = await action.GetOperationsAsync(
            solution,
            new Progress<CodeAnalysisProgress>(),
            cancellationToken);

        if (!TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
        {
            return CodeActionApplyResult.Failed(
                CodeActionApplyFailureKind.UnsupportedActionOperation,
                "The selected action produced unsupported operations.");
        }

        return CodeActionApplyResult.Applied(applyChanges.ChangedSolution);
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
}
