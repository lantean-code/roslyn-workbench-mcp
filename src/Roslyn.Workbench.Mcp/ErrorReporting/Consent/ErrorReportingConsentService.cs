using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal sealed class ErrorReportingConsentService :
    IErrorReportingConsentService,
    IWorkspaceSnapshotLifecycleObserver
{
    private readonly object _gate = new();
    private readonly ErrorReportingConsentMode _startupMode;
    private readonly HashSet<WorkspaceConsentKey> _workspaceGrants = [];
    private bool _sessionAllowed;
    private bool _sessionSuppressed;

    public ErrorReportingConsentService(IOptions<ErrorReportingOptions> options)
    {
        _startupMode = options.Value.ConsentMode;
    }

    public ErrorReportingConsentState GetState(string? workspaceId, long? workspaceEpoch)
    {
        lock (_gate)
        {
            if (_sessionSuppressed)
            {
                return ErrorReportingConsentState.SuppressedForSession;
            }

            if (_startupMode == ErrorReportingConsentMode.Always)
            {
                return ErrorReportingConsentState.AlwaysApproved;
            }

            if (_sessionAllowed)
            {
                return ErrorReportingConsentState.AllowedForSession;
            }

            if (!string.IsNullOrWhiteSpace(workspaceId)
                && workspaceEpoch is not null
                && _workspaceGrants.Contains(new WorkspaceConsentKey(workspaceId, workspaceEpoch.Value)))
            {
                return ErrorReportingConsentState.AllowedForWorkspace;
            }

            return ErrorReportingConsentState.PromptRequired;
        }
    }

    public void AllowWorkspace(string workspaceId, long workspaceEpoch)
    {
        lock (_gate)
        {
            _workspaceGrants.Add(new WorkspaceConsentKey(workspaceId, workspaceEpoch));
        }
    }

    public void AllowSession()
    {
        lock (_gate)
        {
            _sessionAllowed = true;
        }
    }

    public void SuppressSession()
    {
        lock (_gate)
        {
            _sessionSuppressed = true;
            _sessionAllowed = false;
            _workspaceGrants.Clear();
        }
    }

    public void InvalidateWorkspace(string workspaceId, long workspaceEpoch)
    {
        lock (_gate)
        {
            _workspaceGrants.Remove(new WorkspaceConsentKey(workspaceId, workspaceEpoch));
        }
    }

    public void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
    }

    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
    }

    private readonly record struct WorkspaceConsentKey(string WorkspaceId, long WorkspaceEpoch);
}
