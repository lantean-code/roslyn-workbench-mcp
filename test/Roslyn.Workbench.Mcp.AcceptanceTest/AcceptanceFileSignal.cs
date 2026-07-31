namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class AcceptanceFileSignal
{
    public static async Task WaitAsync(string path, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"The signal path '{path}' does not have a parent directory.");

        Directory.CreateDirectory(directory);
        if (File.Exists(path))
        {
            return;
        }

        var signalObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FileSystemWatcher(directory)
        {
            EnableRaisingEvents = true,
            Filter = Path.GetFileName(path),
            NotifyFilter = NotifyFilters.FileName,
        };

        watcher.Created += handleSignal;
        if (!File.Exists(path))
        {
            await signalObserved.Task.WaitAsync(cancellationToken);
        }

        watcher.Created -= handleSignal;

        void handleSignal(object sender, FileSystemEventArgs arguments)
        {
            signalObserved.TrySetResult();
        }
    }
}
