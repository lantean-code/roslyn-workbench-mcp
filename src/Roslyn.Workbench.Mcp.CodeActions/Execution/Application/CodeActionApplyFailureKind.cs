namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

/// <summary>
/// Identifies why a Code Action could not be resolved or evaluated.
/// </summary>
internal enum CodeActionApplyFailureKind
{
    /// <summary>
    /// The action produced operations other than one supported source change.
    /// </summary>
    UnsupportedActionOperation,
    /// <summary>
    /// The requested Fix All operation is no longer available.
    /// </summary>
    FixAllUnavailable,
    /// <summary>
    /// The referenced action has expired or was consumed.
    /// </summary>
    ActionExpired,
    /// <summary>
    /// The request does not satisfy the action's requirements.
    /// </summary>
    InvalidRequest,
    /// <summary>
    /// The originating document could not be resolved.
    /// </summary>
    DocumentNotFound,
    /// <summary>
    /// The originating project could not be resolved.
    /// </summary>
    ProjectNotFound,
    /// <summary>
    /// The originating Code Fix could not be rediscovered.
    /// </summary>
    CodeFixUnavailable,
    /// <summary>
    /// Rediscovery produced more than one matching action.
    /// </summary>
    ActionAmbiguous,
}
