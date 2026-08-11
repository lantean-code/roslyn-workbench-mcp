using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogSnapshot : IDisposable, IAsyncDisposable
{
    private int _disposeState;

    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<PluginStatus> Plugins { get; init; } = [];

    public IReadOnlyList<AssemblyLoadContext> LoadContexts { get; init; } = [];

    public IReadOnlyList<IDisposable> ServiceProviderLifetimes { get; init; } = [];

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Every isolated plugin provider must be offered disposal even when another plugin-owned disposable throws; all genuine disposal failures are rethrown together.")]
    public void Dispose()
    {
        if (!TryBeginDisposal())
        {
            return;
        }

        List<Exception>? failures = null;
        for (var index = ServiceProviderLifetimes.Count - 1; index >= 0; index--)
        {
            try
            {
                ServiceProviderLifetimes[index].Dispose();
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }

        ThrowIfDisposalFailed(failures);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Every isolated plugin provider must be offered asynchronous disposal even when another plugin-owned disposable throws; all genuine disposal failures are rethrown together.")]
    public async ValueTask DisposeAsync()
    {
        if (!TryBeginDisposal())
        {
            return;
        }

        List<Exception>? failures = null;
        for (var index = ServiceProviderLifetimes.Count - 1; index >= 0; index--)
        {
            try
            {
                var lifetime = ServiceProviderLifetimes[index];
                if (lifetime is IAsyncDisposable asyncLifetime)
                {
                    await asyncLifetime.DisposeAsync();
                }
                else
                {
                    lifetime.Dispose();
                }
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }

        ThrowIfDisposalFailed(failures);
    }

    private bool TryBeginDisposal()
    {
        return Interlocked.Exchange(ref _disposeState, 1) == 0;
    }

    private static void ThrowIfDisposalFailed(List<Exception>? failures)
    {
        if (failures is null)
        {
            return;
        }

        throw new AggregateException(
            "One or more plugin service providers failed during disposal.",
            failures);
    }
}
