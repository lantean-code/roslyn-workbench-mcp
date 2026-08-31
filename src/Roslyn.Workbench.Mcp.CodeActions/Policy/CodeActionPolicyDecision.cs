namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

/// <summary>
/// Records whether policy permits a Code Action and, when excluded, why.
/// </summary>
internal sealed record CodeActionPolicyDecision
{
    /// <summary>
    /// Gets a value indicating whether policy permits the Code Action.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the stable exclusion reason code, or <see langword="null"/> when the action is allowed.
    /// </summary>
    public string? ReasonCode { get; }

    private CodeActionPolicyDecision(bool isAllowed, string? reasonCode)
    {
        IsAllowed = isAllowed;
        ReasonCode = reasonCode;
    }

    /// <summary>
    /// Creates a decision that permits the Code Action.
    /// </summary>
    /// <returns>The Code Action policy decision.</returns>
    public static CodeActionPolicyDecision Allowed()
    {
        return new CodeActionPolicyDecision(isAllowed: true, reasonCode: null);
    }

    /// <summary>
    /// Creates a result that represents policy exclusion.
    /// </summary>
    /// <param name="reasonCode">The stable reason that identifies the policy exclusion.</param>
    /// <returns>The Code Action policy decision.</returns>
    public static CodeActionPolicyDecision Excluded(string reasonCode)
    {
        return new CodeActionPolicyDecision(isAllowed: false, reasonCode);
    }
}
