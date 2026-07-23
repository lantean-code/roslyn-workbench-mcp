namespace Roslyn.Workbench.Mcp.TestSupport;

internal static class PluginFixtureControl
{
    public static async Task WaitForReleaseAsync(
        string? controlDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(controlDirectory))
        {
            return;
        }

        Directory.CreateDirectory(controlDirectory);
        var readyPath = Path.Combine(controlDirectory, "ready");
        var releasePath = Path.Combine(controlDirectory, "release");
        var releaseObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var watcher = new FileSystemWatcher(controlDirectory)
        {
            EnableRaisingEvents = true,
            Filter = "release",
            NotifyFilter = NotifyFilters.FileName,
        };

        watcher.Created += handleRelease;
        await File.WriteAllTextAsync(readyPath, string.Empty, cancellationToken);
        if (!File.Exists(releasePath))
        {
            await releaseObserved.Task.WaitAsync(cancellationToken);
        }

        watcher.Created -= handleRelease;

        void handleRelease(object sender, FileSystemEventArgs arguments)
        {
            _ = sender;
            _ = arguments;
            releaseObserved.TrySetResult();
        }
    }
}
