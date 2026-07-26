namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

internal static class CodeActionExecutionResultFactory
{
    public static CodeActionExecutionResult<T>? ValidateSnapshot<T>(
        IWorkspaceResolver resolver,
        SnapshotPrecondition? expectedSnapshot)
    {
        var result = resolver.ValidateSnapshot(expectedSnapshot);
        if (result.Kind == SnapshotMatchKind.Matched)
        {
            return null;
        }

        var error = new CodeActionExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "The request snapshot does not match the current workspace snapshot.",
        };

        return CodeActionExecutionResult.Conflict<T>(error, RequiredAction.ResolveTargetAgain);
    }

    public static CodeActionExecutionResult<T> RejectFromStatus<T>(
        SelectorResolveStatus status,
        string targetCode,
        string targetDisplayName)
    {
        var (code, message) = status switch
        {
            SelectorResolveStatus.Ambiguous => (
                $"{targetCode}Ambiguous",
                $"The {targetDisplayName} selector matched multiple results."),
            _ => (
                $"{targetCode}NotFound",
                $"The {targetDisplayName} selector did not match any result."),
        };

        return Rejected<T>(code, message, RequiredAction.ResolveTargetAgain);
    }

    public static CodeActionExecutionResult<T> Rejected<T>(
        string code,
        string message,
        RequiredAction? requiredAction = null)
    {
        var error = new CodeActionExecutionError
        {
            Code = code,
            Message = message,
        };

        return CodeActionExecutionResult.Rejected<T>(error, requiredAction);
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
        var error = new CodeActionExecutionError
        {
            Code = "FixAllUnavailable",
            Message = message,
        };

        return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error);
    }

    public static CodeActionExecutionResult<T> CodeActionsUnavailable<T>()
    {
        return Rejected<T>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    public static CodeActionExecutionResult<T> ActionExpired<T>()
    {
        var error = new CodeActionExecutionError
        {
            Code = "ActionExpired",
            Message = "The requested action reference is no longer valid.",
        };

        return CodeActionExecutionResult.Rejected<T>(error, RequiredAction.ResolveTargetAgain);
    }
}
