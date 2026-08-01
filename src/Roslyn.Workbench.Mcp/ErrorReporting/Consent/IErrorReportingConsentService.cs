namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal interface IErrorReportingConsentService
{
    ErrorReportingConsentState GetState(Guid? workspaceId, long? workspaceEpoch);

    void AllowWorkspace(Guid workspaceId, long workspaceEpoch);

    void AllowSession();

    void SuppressSession();
}
