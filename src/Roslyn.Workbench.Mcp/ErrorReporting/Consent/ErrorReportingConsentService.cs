namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal sealed class ErrorReportingConsentService : IErrorReportingConsentService
{
    private readonly IErrorReportingConsentStore _store;

    public ErrorReportingConsentService(IErrorReportingConsentStore store)
    {
        _store = store;
    }

    public ErrorReportingConsentState GetState(Guid? workspaceId, long? workspaceEpoch)
    {
        return _store.GetState(workspaceId, workspaceEpoch);
    }

    public void AllowWorkspace(Guid workspaceId, long workspaceEpoch)
    {
        _store.AllowWorkspace(workspaceId, workspaceEpoch);
    }

    public void AllowSession()
    {
        _store.AllowSession();
    }

    public void SuppressSession()
    {
        _store.SuppressSession();
    }
}
