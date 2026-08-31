namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Couples a query cache scope with its underlying workspace lease so both are released together.
/// </summary>
internal sealed class PluginQueryExecutionLease : IAsyncDisposable
{
    private readonly QueryResultCacheScope _cacheScope;
    private readonly IAsyncDisposable _workspaceLease;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginQueryExecutionLease"/> class.
    /// </summary>
    /// <param name="cacheScope">The invocation-scoped query cache.</param>
    /// <param name="workspaceLease">The underlying workspace query lease.</param>
    public PluginQueryExecutionLease(
        QueryResultCacheScope cacheScope,
        IAsyncDisposable workspaceLease)
    {
        _cacheScope = cacheScope;
        _workspaceLease = workspaceLease;
    }

    /// <summary>
    /// Releases cached values before releasing the workspace execution lease.
    /// </summary>
    /// <returns>A task representing asynchronous disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        _cacheScope.Dispose();
        await _workspaceLease.DisposeAsync();
    }
}
