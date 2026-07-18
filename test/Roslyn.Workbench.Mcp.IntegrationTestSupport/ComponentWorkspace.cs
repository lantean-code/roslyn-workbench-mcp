using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Roslyn.Workbench.Mcp.Configuration;
using Roslyn.Workbench.Mcp.Workspace.Coordination;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal sealed class ComponentWorkspace : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly TemporaryDirectory? _ownedStateDirectory;
    private int _isDisposed;

    private ComponentWorkspace(
        ServiceProvider serviceProvider,
        string stateDirectory,
        TemporaryDirectory? ownedStateDirectory)
    {
        _serviceProvider = serviceProvider;
        StateDirectory = stateDirectory;
        _ownedStateDirectory = ownedStateDirectory;
    }

    internal string StateDirectory { get; }

    internal IToolExecutionContextFactory PluginContextFactory
    {
        get { return _serviceProvider.GetRequiredService<IToolExecutionContextFactory>(); }
    }

    internal ICodeActionExecutionContextFactory CodeActionContextFactory
    {
        get { return _serviceProvider.GetRequiredService<ICodeActionExecutionContextFactory>(); }
    }

    internal static ComponentWorkspace Create(
        ComponentWorkspaceOptions? options = null,
        ICodeActionProviderCatalog? codeActionProviderCatalog = null,
        CodeActionDescriptorOverride? descriptorOverride = null)
    {
        options ??= new ComponentWorkspaceOptions();
        var ownedStateDirectory = options.StateDirectory is null
            ? TemporaryDirectory.Create("roslyn-workbench-mcp-state")
            : null;
        var stateDirectory = options.StateDirectory ?? ownedStateDirectory!.DirectoryPath;

        try
        {
            var services = new ServiceCollection();
            services.AddRoslynWorkbenchOptions(new StartupOptions
            {
                DefaultMaxResults = options.DefaultMaxResults,
                MaxConcurrentQueries = options.MaxConcurrentQueries,
                MaxTransactionRevisions = options.MaxTransactionRevisions,
                StateDirectory = stateDirectory,
            });
            services.Configure<WorkspaceCoordinatorOptions>(configured =>
                configured.MaxLoadedWorkspaces = options.MaxLoadedWorkspaces);
            services.Configure<CodeActionCompositionOptions>(configured =>
                configured.IncludeBuiltInAssemblies = false);
            services.AddSingleton(TimeProvider.System);
            services.AddWorkspaceServices();
            if (options.Boundary != ComponentWorkspaceBoundary.CodeActions)
            {
                services.AddPluginServices();
            }

            if (options.Boundary == ComponentWorkspaceBoundary.CodeActions)
            {
                services.AddCodeActionServices();
            }

            codeActionProviderCatalog ??= options.Boundary == ComponentWorkspaceBoundary.CodeActions
                ? null
                : CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
                {
                    IncludeBuiltInAssemblies = false,
                });
            if (codeActionProviderCatalog is not null)
            {
                services.AddSingleton(codeActionProviderCatalog);
            }

            if (descriptorOverride is not null)
            {
                services.AddSingleton<ICodeActionDescriptorRegistry>(
                    new CodeActionDescriptorRegistry([descriptorOverride]));
            }

            var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
            return new ComponentWorkspace(serviceProvider, stateDirectory, ownedStateDirectory);
        }
        catch
        {
            ownedStateDirectory?.Dispose();
            throw;
        }
    }

    internal T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    internal T CreateInstance<T>() where T : notnull
    {
        return ActivatorUtilities.CreateInstance<T>(_serviceProvider);
    }

    internal ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        return PluginContextFactory.CreateQueryContext(request, cancellationToken);
    }

    internal PluginMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        return PluginContextFactory.CreateMutationContext(request, cancellationToken);
    }

    internal ValueTask<WorkspaceOperationResult<WorkspaceOpenOutcome>> OpenAsync(
        string path,
        CancellationToken cancellationToken,
        string? alias = null,
        string? workspaceRoot = null)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().OpenAsync(
            path,
            alias,
            workspaceRoot,
            cancellationToken);
    }

    internal ValueTask<WorkspaceOperationResult<WorkspaceListOutcome>> ListAsync(CancellationToken cancellationToken)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().ListAsync(cancellationToken);
    }

    internal ValueTask<WorkspaceOperationResult<WorkspaceCloseOutcome>> CloseAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().CloseAsync(workspaceId, alias, path, cancellationToken);
    }

    internal ValueTask<WorkspaceOperationResult<WorkspaceStatusOutcome>> GetStatusAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
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

    internal ValueTask<WorkspaceOperationResult<WorkspaceReloadOutcome>> ReloadAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<IWorkspaceLifecycleService>().ReloadAsync(workspaceId, alias, path, cancellationToken);
    }

    internal ValueTask<WorkspaceOperationResult<TransactionStartOutcome>> StartTransactionAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
        string? alias = null,
        string? path = null)
    {
        return GetRequiredService<ITransactionService>().StartAsync(workspaceId, alias, path, cancellationToken);
    }

    internal ValueTask<WorkspaceOperationResult<TransactionPreviewOutcome>> PreviewTransactionAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
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

    internal ValueTask<WorkspaceOperationResult<TransactionHistoryOutcome>> MoveTransactionHistoryAsync(
        TransactionHistoryDirection direction,
        CancellationToken cancellationToken,
        string? workspaceId = null,
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

    internal ValueTask<WorkspaceOperationResult<TransactionCommitOutcome>> CommitTransactionAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
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

    internal ValueTask<WorkspaceOperationResult<TransactionRollbackOutcome>> RollbackTransactionAsync(
        CancellationToken cancellationToken,
        string? workspaceId = null,
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
                await instanceStatusPublisher.CloseAsync(workspaceId).ConfigureAwait(false);
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
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            disposalFailure ??= exception;
        }

        try
        {
            if (_ownedStateDirectory is not null)
            {
                await _ownedStateDirectory.DisposeAsync().ConfigureAwait(false);
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
