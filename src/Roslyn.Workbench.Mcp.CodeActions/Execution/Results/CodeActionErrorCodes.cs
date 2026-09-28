namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Results;

/// <summary>
/// Defines stable error codes emitted by Code Action tools and adapters.
/// </summary>
internal static class CodeActionErrorCodes
{
    /// <summary>
    /// More than one Code Action matched the saved action reference.
    /// </summary>
    public const string ActionAmbiguous = "ActionAmbiguous";

    /// <summary>
    /// The saved Code Action reference has expired.
    /// </summary>
    public const string ActionExpired = "ActionExpired";

    /// <summary>
    /// The action-reference store has reached its configured capacity.
    /// </summary>
    public const string ActionReferenceCapacityExceeded = "ActionReferenceCapacityExceeded";

    /// <summary>
    /// The requested Code Action is no longer available.
    /// </summary>
    public const string ActionUnavailable = "ActionUnavailable";

    /// <summary>
    /// A Code Action result did not expose an addressable document path.
    /// </summary>
    public const string CodeActionDocumentPathUnavailable = "CodeActionDocumentPathUnavailable";

    /// <summary>
    /// A Code Action result did not expose an addressable source location.
    /// </summary>
    public const string CodeActionLocationUnavailable = "CodeActionLocationUnavailable";

    /// <summary>
    /// A Code Action result could not be projected into the published contract.
    /// </summary>
    public const string CodeActionProjectionFailed = "CodeActionProjectionFailed";

    /// <summary>
    /// Code Action composition is unavailable.
    /// </summary>
    public const string CodeActionsUnavailable = "CodeActionsUnavailable";

    /// <summary>
    /// The requested Fix All operation exceeds its configured change limit.
    /// </summary>
    public const string FixAllLimitExceeded = "FixAllLimitExceeded";

    /// <summary>
    /// The requested Fix All operation is unavailable.
    /// </summary>
    public const string FixAllUnavailable = "FixAllUnavailable";

    /// <summary>
    /// The supplied source range is invalid.
    /// </summary>
    public const string InvalidRange = "InvalidRange";
}
