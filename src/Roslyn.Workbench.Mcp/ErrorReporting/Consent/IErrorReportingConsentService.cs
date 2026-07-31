namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal interface IErrorReportingConsentService
{
    ErrorReportingConsentState GetState(string? workspaceId, long? workspaceEpoch);

    void AllowWorkspace(string workspaceId, long workspaceEpoch);

    void AllowSession();

    void SuppressSession();
}
