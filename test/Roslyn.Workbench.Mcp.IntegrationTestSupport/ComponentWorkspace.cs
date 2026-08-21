using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Roslyn.Workbench.Mcp.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Coordination;
using Roslyn.Workbench.Mcp.Workspace.Loading;
using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal sealed class ComponentWorkspace : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly TemporaryDirectory? _ownedStateDirectory;
    private int _isDisposed;

    private ComponentWorkspace(
        IHost host,
        string stateDirectory,
        TemporaryDirectory? ownedStateDirectory)
    {
        _host = host;
        StateDirectory = stateDirectory;
        _ownedStateDirectory = ownedStateDirectory;
    }

    public string StateDirectory { get; }

    public IToolExecutionContextFactory PluginContextFactory
    {
        get { return _host.Services.GetRequiredService<IToolExecutionContextFactory>(); }
    }

    public ICodeActionExecutionContextFactory CodeActionContextFactory
    {
        get { return _host.Services.GetRequiredService<ICodeActionExecutionContextFactory>(); }
    }

    public static ComponentWorkspace Create(
        ComponentWorkspaceOptions? options = null,
        ICodeActionComposition? codeActionComposition = null)
    {
        options ??= new ComponentWorkspaceOptions();
        TemporaryDirectory? ownedStateDirectory = null;
        if (options.StateDirectory is null)
        {
            ownedStateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-state");
        }

        var stateDirectory = options.StateDirectory ?? ownedStateDirectory!.DirectoryPath;

        try
        {
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            var serviceProviderFactory = new DefaultServiceProviderFactory(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            builder.ConfigureContainer(serviceProviderFactory, static _ => { });
            var services = builder.Services;
            services.AddRoslynWorkbenchOptions(new StartupOptions
            {
                DefaultMaxResults = options.DefaultMaxResults,
                MaxConcurrentQueries = options.MaxConcurrentQueries,
                MaxTransactionRevisions = options.MaxTransactionRevisions,
                StateDirectory = stateDirectory,
            });

            services.Configure<WorkspaceOptions>(configured =>
                configured.MaxLoadedWorkspaces = options.MaxLoadedWorkspaces);

            services.Configure<CodeActionCompositionOptions>(configured =>
                configured.IncludeBuiltInAssemblies = options.IncludeBuiltInCodeActions);

            services.AddSingleton(TimeProvider.System);
            services.AddWorkspaceServices();
            if (options.CommitPlanner is not null)
            {
                services.RemoveAll<IWorkspaceCommitPlanner>();
                services.AddSingleton(options.CommitPlanner);
            }

            if (options.Boundary != ComponentWorkspaceBoundary.CodeActions)
            {
                services.AddPluginServices();
            }

            if (options.Boundary == ComponentWorkspaceBoundary.CodeActions)
            {
                services.AddCodeActionServices();
            }

            if (codeActionComposition is null
                && options.Boundary != ComponentWorkspaceBoundary.CodeActions)
            {
                var compositionOptions = new CodeActionCompositionOptions
                {
                    IncludeBuiltInAssemblies = false,
                };

                codeActionComposition = CodeActionCompositionFactory.Create(compositionOptions);
            }

            if (codeActionComposition is not null)
            {
                services.AddSingleton(codeActionComposition);
            }

            var host = builder.Build();
            try
            {
                host.Services.GetRequiredService<IWorkspaceStateDirectory>().Initialize();
                return new ComponentWorkspace(host, stateDirectory, ownedStateDirectory);
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }
        catch
        {
            ownedStateDirectory?.Dispose();
            throw;
        }
    }

    public T GetRequiredService<T>() where T : notnull
    {
        return _host.Services.GetRequiredService<T>();
    }

    public T CreateInstance<T>() where T : notnull
    {
        return ActivatorUtilities.CreateInstance<T>(_host.Services);
    }

    public ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        return PluginContextFactory.CreateQueryContext(request, cancellationToken);
    }

    public PluginMutationExecutionLease CreateMutationContext(
        WorkspaceMutationRequest request,
        CancellationToken cancellationToken)
    {
        return PluginContextFactory.CreateMutationContext(request, cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        CancellationToken cancellationToken,
        string? alias = null,
        string? workspaceRoot = null,
        WorkspaceMsBuildProperties? msBuildProperties = null)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().OpenAsync(
            path,
            alias,
            workspaceRoot,
            msBuildProperties,
            cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceListOutcome>> ListAsync(CancellationToken cancellationToken)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().ListAsync(cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceCloseOutcome>> CloseAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().CloseAsync(workspaceId, alias, path, cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceStatusOutcome>> GetStatusAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null,
        StatusDetailLevel detail = StatusDetailLevel.Standard)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().GetStatusAsync(
            workspaceId,
            alias,
            path,
            detail,
            cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<WorkspaceReloadOutcome>> ReloadAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().ReloadAsync(workspaceId, alias, path, cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartTransactionAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<ITransactionService>().StartAsync(workspaceId, alias, path, cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<TransactionPreviewOutcome>> PreviewTransactionAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null,
        DocumentSelector? document = null,
        bool includeDiff = false,
        int contextLines = 3)
    {
        return GetRequiredService<ITransactionService>().PreviewAsync(
            workspaceId,
            alias,
            path,
            document,
            includeDiff,
            contextLines,
            cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<TransactionHistoryOutcome>> MoveTransactionHistoryAsync(
        TransactionHistoryDirection direction,
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null,
        SnapshotPrecondition? expectedSnapshot = null)
    {
        return GetRequiredService<ITransactionService>().MoveHistoryAsync(
            workspaceId,
            alias,
            path,
            direction,
            expectedSnapshot,
            cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitTransactionAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null,
        SnapshotPrecondition? expectedSnapshot = null)
    {
        return GetRequiredService<ITransactionService>().CommitAsync(
            workspaceId,
            alias,
            path,
            expectedSnapshot,
            cancellationToken);
    }

    public ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackTransactionAsync(
        CancellationToken cancellationToken,
        Guid? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<ITransactionService>().RollbackAsync(workspaceId, alias, path, cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The integration fixture must attempt every independent cleanup step, retain the first failure, and report it after all owned resources have been released.")]
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        Exception? disposalFailure = null;
        var sessionStore = GetRequiredService<IWorkspaceSessionStore>();
        var instanceStatusPublisher = GetRequiredService<IWorkspaceInstanceStatusPublisher>();
        var workspaceIds = sessionStore.ReadSnapshot().Workspaces.Keys.ToArray();
        foreach (var workspaceId in workspaceIds)
        {
            try
            {
                await instanceStatusPublisher.CloseAsync(workspaceId);
            }
            catch (Exception exception)
            {
                disposalFailure ??= exception;
            }

            try
            {
                sessionStore.RemoveWorkspace(workspaceId)?.LoadedWorkspace.Dispose();
            }
            catch (Exception exception)
            {
                disposalFailure ??= exception;
            }
        }

        try
        {
            await _host.StopAsync();
        }
        catch (Exception exception)
        {
            disposalFailure ??= exception;
        }

        try
        {
            if (_host is IAsyncDisposable asyncDisposableHost)
            {
                await asyncDisposableHost.DisposeAsync();
            }
            else
            {
                _host.Dispose();
            }
        }
        catch (Exception exception)
        {
            disposalFailure ??= exception;
        }

        try
        {
            if (_ownedStateDirectory is not null)
            {
                _ownedStateDirectory.Dispose();
            }
        }
        catch (Exception exception)
        {
            disposalFailure ??= exception;
        }

        if (disposalFailure is not null)
        {
            throw new InvalidOperationException(
                $"Failed to dispose the component Workspace that owns state directory '{StateDirectory}'.",
                disposalFailure);
        }
    }
}
