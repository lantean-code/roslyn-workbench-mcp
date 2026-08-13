using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed partial class WorkspaceShutdownLifecycleService : IHostedLifecycleService
{
    private readonly IWorkspaceLifecycleService _workspaceLifecycleService;
    private readonly ILogger<WorkspaceShutdownLifecycleService> _logger;

    public WorkspaceShutdownLifecycleService(
        IWorkspaceLifecycleService workspaceLifecycleService,
        ILogger<WorkspaceShutdownLifecycleService> logger)
    {
        _workspaceLifecycleService = workspaceLifecycleService;
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
