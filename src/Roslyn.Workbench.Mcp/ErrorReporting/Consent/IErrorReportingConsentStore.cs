namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal interface IErrorReportingConsentStore
{
    ErrorReportingConsentState GetState(string? workspaceId, long? workspaceEpoch);

    void AllowWorkspace(string workspaceId, long workspaceEpoch);

    void AllowSession();

    void SuppressSession();

    void InvalidateWorkspace(string workspaceId, long workspaceEpoch);
}
