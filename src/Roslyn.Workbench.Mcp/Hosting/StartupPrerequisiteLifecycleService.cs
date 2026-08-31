namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Registers MSBuild, initializes durable state and performs commit recovery before the host starts.
/// </summary>
internal sealed class StartupPrerequisiteLifecycleService : IHostedLifecycleService
{
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly IWorkspaceStateDirectory _stateDirectory;
    private readonly IWorkspaceCommitRecoveryService _workspaceCommitRecoveryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupPrerequisiteLifecycleService"/> class.
    /// </summary>
    /// <param name="msBuildRegistrationService">The MSBuild registration service.</param>
    /// <param name="stateDirectory">The directory used for workspace ownership and recovery state.</param>
    /// <param name="workspaceCommitRecoveryService">The service that recovers interrupted workspace commits.</param>
    public StartupPrerequisiteLifecycleService(
        IMsBuildRegistrationService msBuildRegistrationService,
        IWorkspaceStateDirectory stateDirectory,
        IWorkspaceCommitRecoveryService workspaceCommitRecoveryService)
    {
        _msBuildRegistrationService = msBuildRegistrationService;
        _stateDirectory = stateDirectory;
        _workspaceCommitRecoveryService = workspaceCommitRecoveryService;
    }

    /// <summary>
    /// Initializes all startup prerequisites before ordinary hosted services start.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes after durable commit recovery has finished.</returns>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _msBuildRegistrationService.EnsureRegistered();
        _stateDirectory.Initialize();
        await _workspaceCommitRecoveryService.RecoverAsync(cancellationToken);
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
    /// Performs no work during the hosted-service stop phase.
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
