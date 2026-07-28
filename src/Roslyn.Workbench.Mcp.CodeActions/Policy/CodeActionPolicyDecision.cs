namespace Roslyn.Workbench.Mcp.CodeActions.Policy;

internal sealed record CodeActionPolicyDecision
{
    public bool IsAllowed { get; }

    public string? ReasonCode { get; }

    private CodeActionPolicyDecision(bool isAllowed, string? reasonCode)
    {
        IsAllowed = isAllowed;
        ReasonCode = reasonCode;
    }

    public static CodeActionPolicyDecision Allowed()
    {
        return new CodeActionPolicyDecision(isAllowed: true, reasonCode: null);
    }

    public static CodeActionPolicyDecision Excluded(string reasonCode)
    {
        return new CodeActionPolicyDecision(isAllowed: false, reasonCode);
    }
}
