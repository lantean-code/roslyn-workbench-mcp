namespace Roslyn.Workbench.Mcp;

internal sealed class WorkspaceCommitRecoveryHostedService : IHostedService
{
    private readonly IWorkspaceCommitRecoveryService _recoveryService;

    public WorkspaceCommitRecoveryHostedService(IWorkspaceCommitRecoveryService recoveryService)
    {
        _recoveryService = recoveryService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _recoveryService.RecoverAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
