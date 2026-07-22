using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionEvaluator : ICodeActionEvaluator
{
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
