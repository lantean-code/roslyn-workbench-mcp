namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class StartupPrerequisiteLifecycleService : IHostedLifecycleService
{
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;
    private readonly IWorkspaceStateDirectory _stateDirectory;
    private readonly IWorkspaceCommitRecoveryService _workspaceCommitRecoveryService;

    public StartupPrerequisiteLifecycleService(
        IMsBuildRegistrationService msBuildRegistrationService,
        IWorkspaceStateDirectory stateDirectory,
        IWorkspaceCommitRecoveryService workspaceCommitRecoveryService)
    {
        _msBuildRegistrationService = msBuildRegistrationService;
        _stateDirectory = stateDirectory;
        _workspaceCommitRecoveryService = workspaceCommitRecoveryService;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _msBuildRegistrationService.EnsureRegistered();
        _stateDirectory.Initialize();
        await _workspaceCommitRecoveryService.RecoverAsync(cancellationToken);
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
