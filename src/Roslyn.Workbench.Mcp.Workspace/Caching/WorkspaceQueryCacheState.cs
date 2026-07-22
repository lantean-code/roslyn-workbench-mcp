using Microsoft.Extensions.Primitives;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class WorkspaceQueryCacheState : IQueryCacheInvalidationTokenSource, IWorkspaceQueryCacheState, IDisposable
{
    private readonly Lock _syncRoot;
    private readonly Dictionary<string, CancellationTokenSource> _workspaceInvalidationSources;

    public WorkspaceQueryCacheState()
    {
        _syncRoot = new Lock();
        _workspaceInvalidationSources = new Dictionary<string, CancellationTokenSource>(StringComparer.Ordinal);
    }

    public IChangeToken GetInvalidationToken(string workspaceId)
    {
        lock (_syncRoot)
        {
            if (!_workspaceInvalidationSources.TryGetValue(workspaceId, out var invalidationSource))
            {
                invalidationSource = new CancellationTokenSource();
                _workspaceInvalidationSources.Add(workspaceId, invalidationSource);
            }

            return new CancellationChangeToken(invalidationSource.Token);
        }
    }

    public void InvalidateWorkspace(string workspaceId)
    {
        CancellationTokenSource? invalidationSource;
        lock (_syncRoot)
        {
            invalidationSource = _workspaceInvalidationSources.GetValueOrDefault(workspaceId);
            _workspaceInvalidationSources.Remove(workspaceId);
        }

        if (invalidationSource is null)
        {
            return;
        }

        invalidationSource.Cancel();
        invalidationSource.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource[] invalidationSources;
        lock (_syncRoot)
        {
            invalidationSources = [.. _workspaceInvalidationSources.Values];
            _workspaceInvalidationSources.Clear();
        }

        foreach (var invalidationSource in invalidationSources)
        {
            invalidationSource.Cancel();
            invalidationSource.Dispose();
        }
    }
}
