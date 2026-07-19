namespace Roslyn.Workbench.Mcp.Configuration;

internal sealed partial class StartupConfigurationReporter : IHostedService
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
            LogConfigurationWarning(_logger, warning.Code, warning.Message);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Startup configuration warning {WarningCode}: {WarningMessage}")]
    private static partial void LogConfigurationWarning(
        ILogger logger,
        string warningCode,
        string warningMessage);
}
