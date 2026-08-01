using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Consent;

internal sealed class ErrorReportingConsentStore : IErrorReportingConsentStore
{
    private readonly object _gate = new();
    private readonly ErrorReportingConsentMode _startupMode;
    private readonly HashSet<WorkspaceConsentKey> _workspaceGrants = [];
    private bool _sessionAllowed;
    private bool _sessionSuppressed;

    public ErrorReportingConsentStore(IOptions<ErrorReportingOptions> options)
    {
        _startupMode = options.Value.ConsentMode;
    }

    public ErrorReportingConsentState GetState(Guid? workspaceId, long? workspaceEpoch)
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

            if (workspaceId is not null
                && workspaceEpoch is not null
                && _workspaceGrants.Contains(new WorkspaceConsentKey(workspaceId.Value, workspaceEpoch.Value)))
            {
                return ErrorReportingConsentState.AllowedForWorkspace;
            }

            return ErrorReportingConsentState.PromptRequired;
        }
    }

    public void AllowWorkspace(Guid workspaceId, long workspaceEpoch)
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

    public void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch)
    {
        lock (_gate)
        {
            _workspaceGrants.Remove(new WorkspaceConsentKey(workspaceId, workspaceEpoch));
        }
    }

    private readonly record struct WorkspaceConsentKey
    {
        public Guid WorkspaceId { get; }

        public long WorkspaceEpoch { get; }

        public WorkspaceConsentKey(Guid workspaceId, long workspaceEpoch)
        {
            WorkspaceId = workspaceId;
            WorkspaceEpoch = workspaceEpoch;
        }
    }
}
