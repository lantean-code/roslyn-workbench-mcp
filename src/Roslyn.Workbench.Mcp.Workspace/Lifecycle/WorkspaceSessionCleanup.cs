using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed class WorkspaceSessionCleanup : IWorkspaceSessionCleanup
{
    private readonly IWorkspaceInstanceStatusPublisher _instanceStatusPublisher;

    public WorkspaceSessionCleanup(IWorkspaceInstanceStatusPublisher instanceStatusPublisher)
    {
        _instanceStatusPublisher = instanceStatusPublisher;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Workspace cleanup must attempt every owned resource and report all failures after cleanup completes.")]
    public async ValueTask CleanupAsync(WorkspaceSessionSnapshot session)
    {
        List<Exception>? failures = null;
        try
        {
            await _instanceStatusPublisher.CloseAsync(session.Workspace.WorkspaceId);
        }
        catch (Exception exception)
        {
            AddFailure(exception);
        }

        try
        {
            session.InputManifest.Dispose();
        }
        catch (Exception exception)
        {
            AddFailure(exception);
        }

        try
        {
            session.LoadedWorkspace.Dispose();
        }
        catch (Exception exception)
        {
            AddFailure(exception);
        }

        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException(
            "One or more failures occurred while releasing workspace resources.",
            failures);

        void AddFailure(Exception exception)
        {
            failures ??= [];
            failures.Add(exception);
        }
    }
}
