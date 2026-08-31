namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Builds consistent Code Action rejection and conflict results from execution failures.
/// </summary>
internal static class CodeActionExecutionResultFactory
{
    /// <summary>
    /// Validates a snapshot precondition and creates a conflict result when it does not match.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="resolver">The resolver scoped to the current workspace snapshot.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <returns>A conflict result when validation fails; otherwise, <see langword="null"/>.</returns>
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

    /// <summary>
    /// Creates a result that represents rejection derived from a workspace status.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="status">The status value to expose in the result.</param>
    /// <param name="targetCode">The result code used when the Code Action target cannot be resolved.</param>
    /// <param name="targetDisplayName">The user-facing name of the unresolved Code Action target.</param>
    /// <returns>A result that represents rejection derived from a workspace status.</returns>
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
            SelectorResolveStatus.Invalid => (
                $"{targetCode}SelectorInvalid",
                $"The {targetDisplayName} selector contains an invalid path."),
            _ => (
                $"{targetCode}NotFound",
                $"The {targetDisplayName} selector did not match any result."),
        };

        return Rejected<T>(code, message, RequiredAction.ResolveTargetAgain);
    }

    /// <summary>
    /// Creates a rejected operation result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="code">The stable code used to identify the reported condition.</param>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <param name="requiredAction">The action required before processing can continue.</param>
    /// <returns>A result that represents rejection.</returns>
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

    /// <summary>
    /// Creates a rejected operation result.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="failure">The failure that prevents the operation from continuing.</param>
    /// <returns>A result that represents rejection.</returns>
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

    /// <summary>
    /// Creates a result that represents unavailability.
    /// </summary>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>A result that represents unavailability.</returns>
    public static CodeActionExecutionResult<WorkspaceMutationCandidate> FixAllUnavailable(string message)
    {
        var error = new CodeActionExecutionError
        {
            Code = "FixAllUnavailable",
            Message = message,
        };

        return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error);
    }

    /// <summary>
    /// Creates a result that represents unavailability.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>A result that represents unavailability.</returns>
    public static CodeActionExecutionResult<T> CodeActionsUnavailable<T>()
    {
        return Rejected<T>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }

    /// <summary>
    /// Creates a result that represents an expired action reference.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>A result that represents an expired action reference.</returns>
    public static CodeActionExecutionResult<T> ActionExpired<T>()
    {
        var error = new CodeActionExecutionError
        {
            Code = "ActionExpired",
            Message = "The requested action reference is no longer valid.",
        };

        return CodeActionExecutionResult.Rejected<T>(error, RequiredAction.ResolveTargetAgain);
    }

    /// <summary>
    /// Creates a result that represents a stale workspace snapshot.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>A result that represents a stale workspace snapshot.</returns>
    public static CodeActionExecutionResult<T> SnapshotMismatch<T>()
    {
        var error = new CodeActionExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "The action reference does not belong to the current workspace snapshot.",
        };

        return CodeActionExecutionResult.Conflict<T>(error, RequiredAction.ResolveTargetAgain);
    }
}
