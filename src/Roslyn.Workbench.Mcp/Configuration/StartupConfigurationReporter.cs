namespace Roslyn.Workbench.Mcp.Configuration;

internal sealed class StartupConfigurationReporter : IHostedService
{
    private readonly StartupConfigurationSnapshot _configuration;
    private readonly ILogger<StartupConfigurationReporter> _logger;

    public StartupConfigurationReporter(
        StartupConfigurationSnapshot configuration,
        ILogger<StartupConfigurationReporter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var warning in _configuration.Warnings)
        {
            _logger.LogWarning("Startup configuration warning {WarningCode}: {WarningMessage}", warning.Code, warning.Message);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
