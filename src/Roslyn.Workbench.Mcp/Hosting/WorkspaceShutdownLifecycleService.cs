using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Performs best-effort cleanup of loaded workspaces during host shutdown.
/// </summary>
internal sealed partial class WorkspaceShutdownLifecycleService : IHostedLifecycleService
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;
    private readonly ILogger<WorkspaceShutdownLifecycleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceShutdownLifecycleService"/> class.
    /// </summary>
    /// <param name="workspaceLifecycleService">The service that controls workspace loading and lifetime.</param>
    /// <param name="logger">The logger used to record diagnostic information.</param>
    public WorkspaceShutdownLifecycleService(
        IWorkspaceLifecycleService workspaceLifecycleService,
        ILogger<WorkspaceShutdownLifecycleService> logger)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
        _logger = logger;
    }

    /// <summary>
    /// Performs no work before hosted services start.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no work during the hosted-service start phase.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs no work after the host has started.
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
    /// Shuts down every loaded workspace and logs cleanup failures without interrupting host shutdown.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes after workspace cleanup has been attempted.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Terminal Workspace cleanup failures are logged after best-effort cleanup so they do not disrupt the remaining Host shutdown process.")]
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _workspaceLifecycleService.ShutdownAsync();
        }
        catch (Exception exception)
        {
            LogWorkspaceShutdownFailure(_logger, exception);
        }
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

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Workspace resource cleanup failed during Host shutdown.")]
    private static partial void LogWorkspaceShutdownFailure(
        ILogger logger,
        Exception exception);
}
