using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins.Core;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Builds and publishes the fixed runtime plugin catalogue before the MCP host starts serving requests.
/// </summary>
internal sealed class PluginCatalogStartupLifecycleService : IHostedLifecycleService
{
    private readonly IPluginCatalogLoader _catalogLoader;
    private readonly IPluginMcpServerToolFactory _toolFactory;
    private readonly IPluginCatalogState _catalogState;
    private readonly StartupOptions _startupOptions;
    private readonly CodeActionCatalogSnapshot _codeActionCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginCatalogStartupLifecycleService"/> class.
    /// </summary>
    /// <param name="catalogLoader">The component that discovers and materializes plugin catalogue entries.</param>
    /// <param name="toolFactory">The factory that wraps registered plugin tools for MCP invocation.</param>
    /// <param name="catalogState">The published plugin catalogue used to resolve tool invocations.</param>
    /// <param name="startupOptions">The configured external plugin directories.</param>
    /// <param name="codeActionCatalog">The catalogue of host-published Code Action tools.</param>
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

    /// <summary>
    /// Loads plugins, creates their MCP wrappers and atomically publishes the runtime catalogue.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task after catalogue publication succeeds.</returns>
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

    /// <summary>
    /// Performs no additional work during the hosted-service start phase.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no additional work after the host has started.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no work before hosted services stop.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no work during the hosted-service stop phase; catalogue disposal is owned by its registered state.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no work after the host has stopped.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
