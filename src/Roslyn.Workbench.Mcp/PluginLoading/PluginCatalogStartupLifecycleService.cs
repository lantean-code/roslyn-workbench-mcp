using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCatalogStartupLifecycleService : IHostedLifecycleService
{
    private readonly IPluginCatalogLoader _catalogLoader;
    private readonly IPluginMcpServerToolFactory _toolFactory;
    private readonly IPluginCatalogState _catalogState;
    private readonly StartupOptions _startupOptions;
    private readonly CodeActionCatalogSnapshot _codeActionCatalog;

    public PluginCatalogStartupLifecycleService(
        IPluginCatalogLoader catalogLoader,
        IPluginMcpServerToolFactory toolFactory,
        IPluginCatalogState catalogState,
        IOptions<StartupOptions> startupOptions,
        CodeActionCatalogSnapshot codeActionCatalog)
    {
        _catalogLoader = catalogLoader;
        _toolFactory = toolFactory;
        _catalogState = catalogState;
        _startupOptions = startupOptions.Value;
        _codeActionCatalog = codeActionCatalog;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Startup must retain both the original startup failure and any failure while releasing provisional plugin providers.")]
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var reservedToolNames = ServerOwnedToolRegistration.ToolNames
            .Concat(_codeActionCatalog.Tools.Select(static tool => tool.Metadata.Name));
        var catalog = _catalogLoader.Load(
            _startupOptions,
            [typeof(BundledCorePlugin).Assembly],
            reservedToolNames);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tools = new Dictionary<string, McpServerTool>(StringComparer.Ordinal);
            foreach (var registration in catalog.Tools)
            {
                var tool = registration.Accept(_toolFactory);
                tools.Add(tool.ProtocolTool.Name, tool);
            }

            var publishedTools = tools.ToFrozenDictionary(StringComparer.Ordinal);
            var runtimeCatalog = new PluginRuntimeCatalogSnapshot
            {
                Catalog = catalog,
                Tools = publishedTools,
            };

            _catalogState.Publish(runtimeCatalog);
        }
        catch (Exception startupException)
        {
            DisposeProvisionalCatalog(catalog, startupException);
            throw;
        }

        return Task.CompletedTask;
    }

    private static void DisposeProvisionalCatalog(
        PluginCatalogSnapshot catalog,
        Exception startupException)
    {
        try
        {
            catalog.Dispose();
        }
        catch (Exception disposalException)
        {
            throw new AggregateException(
                "Plugin catalogue startup failed and one or more provisional plugin service providers also failed during disposal.",
                startupException,
                disposalException);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
