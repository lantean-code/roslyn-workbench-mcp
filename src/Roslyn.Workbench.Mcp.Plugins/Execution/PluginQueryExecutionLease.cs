namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginQueryExecutionLease : IAsyncDisposable
{
    private readonly QueryResultCacheScope _cacheScope;
    private readonly IAsyncDisposable _workspaceLease;

    public PluginQueryExecutionLease(
        QueryResultCacheScope cacheScope,
        IAsyncDisposable workspaceLease)
    {
        _cacheScope = cacheScope;
        _workspaceLease = workspaceLease;
    }

    public async ValueTask DisposeAsync()
    {
        _cacheScope.Dispose();
        await _workspaceLease.DisposeAsync();
    }
}
