namespace Roslyn.Workbench.Mcp;

internal sealed class MsBuildRegistrationHostedService : IHostedService
{
    private readonly IMsBuildRegistrationService _msBuildRegistrationService;

    public MsBuildRegistrationHostedService(IMsBuildRegistrationService msBuildRegistrationService)
    {
        _msBuildRegistrationService = msBuildRegistrationService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = _msBuildRegistrationService.EnsureRegistered();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
