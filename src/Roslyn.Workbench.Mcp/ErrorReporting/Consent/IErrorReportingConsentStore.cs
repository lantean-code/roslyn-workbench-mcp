namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal interface IErrorReportingConsentStore
{
    ErrorReportingConsentState GetState(Guid? workspaceId, long? workspaceEpoch);

    void AllowWorkspace(Guid workspaceId, long workspaceEpoch);

    void AllowSession();

    void SuppressSession();

    void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch);
}
