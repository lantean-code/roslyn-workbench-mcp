namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed record WorkspaceContractValidationFailure
{
    public string Message { get; }

    public IReadOnlyList<string> MemberNames { get; }

    public WorkspaceContractValidationFailure(string message, IReadOnlyList<string> memberNames)
    {
        Message = message;
        MemberNames = memberNames;
    }
}
