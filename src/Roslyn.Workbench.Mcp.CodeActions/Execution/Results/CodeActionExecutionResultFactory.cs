namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal static class CodeActionExecutionResultFactory
{
    public static CodeActionExecutionResult<T>? ValidateSnapshot<T>(
        IWorkspaceResolver resolver,
        SnapshotPrecondition? expectedSnapshot)
    {
        var result = resolver.ValidateSnapshot(expectedSnapshot);
        return result.Kind == SnapshotMatchKind.Matched
            ? null
            : CodeActionExecutionResult<T>.Conflict(new CodeActionExecutionError
            {
                Code = "SnapshotMismatch",
                Message = "The request snapshot does not match the current workspace snapshot.",
            }, RequiredAction.ResolveTargetAgain);
    }

    public static CodeActionExecutionResult<T> RejectFromStatus<T>(SelectorResolveStatus status, string targetCode, string targetDisplayName)
    {
        return status switch
        {
            SelectorResolveStatus.Ambiguous => Rejected<T>($"{targetCode}Ambiguous", $"The {targetDisplayName} selector matched multiple results.", RequiredAction.ResolveTargetAgain),
            _ => Rejected<T>($"{targetCode}NotFound", $"The {targetDisplayName} selector did not match any result.", RequiredAction.ResolveTargetAgain),
        };
    }

    public static CodeActionExecutionResult<T> Rejected<T>(
        string code,
        string message,
        RequiredAction? requiredAction = null)
    {
        return CodeActionExecutionResult<T>.Rejected(new CodeActionExecutionError
        {
            Code = code,
            Message = message,
        }, requiredAction);
    }

    public static CodeActionExecutionResult<T> Rejected<T>(CodeActionApplyFailure failure)
    {
        RequiredAction? requiredAction = failure.Kind switch
        {
            CodeActionApplyFailureKind.ActionExpired => RequiredAction.ResolveTargetAgain,
            CodeActionApplyFailureKind.DocumentNotFound => RequiredAction.ResolveTargetAgain,
            CodeActionApplyFailureKind.ProjectNotFound => RequiredAction.ResolveTargetAgain,
            _ => null,
        };

        return Rejected<T>(failure.Kind.ToString(), failure.Message, requiredAction);
    }

    public static CodeActionExecutionResult<WorkspaceMutationCandidate> FixAllUnavailable(string message)
    {
        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
        {
            Code = "FixAllUnavailable",
            Message = message,
        });
    }

    public static CodeActionExecutionResult<T> CodeActionsUnavailable<T>()
    {
        return Rejected<T>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    public static CodeActionExecutionResult<T> ActionExpired<T>()
    {
        return CodeActionExecutionResult<T>.Rejected(new CodeActionExecutionError
        {
            Code = "ActionExpired",
            Message = "The requested action token is no longer valid.",
        }, RequiredAction.ResolveTargetAgain);
    }
}
