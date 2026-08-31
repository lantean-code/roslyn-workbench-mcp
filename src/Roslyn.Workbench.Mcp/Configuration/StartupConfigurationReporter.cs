namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Writes configuration fallbacks discovered during startup to the host log.
/// </summary>
internal sealed partial class StartupConfigurationReporter : IHostedService
{
    private readonly StartupConfigurationSnapshot _configuration;
    private readonly ILogger<StartupConfigurationReporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupConfigurationReporter"/> class.
    /// </summary>
    /// <param name="configuration">The resolved options and warnings produced during startup.</param>
    /// <param name="logger">The logger used to record diagnostic information.</param>
    public StartupConfigurationReporter(
        StartupConfigurationSnapshot configuration,
        ILogger<StartupConfigurationReporter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Logs the warnings produced while resolving startup configuration.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task after every warning has been logged.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var warning in _configuration.Warnings)
        {
            LogConfigurationWarning(_logger, warning.Code, warning.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes host shutdown without additional reporting work.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
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
