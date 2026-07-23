using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class CrashRecoveryTargetMonitor
{
    private static readonly TimeSpan _interruptionTimeout = TimeSpan.FromMinutes(2);
    private readonly PerformanceHost _host;
    private readonly Dictionary<string, (bool Exists, long Length, long LastWriteTicks)> _targetStates;

    public CrashRecoveryTargetMonitor(
        PerformanceHost host,
        string repositoryRoot,
        IReadOnlyList<string> targetPaths)
    {
        _host = host;
        _targetStates = new Dictionary<string, (bool, long, long)>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);

        foreach (var path in targetPaths)
        {
            var resolvedPath = ResolvePath(repositoryRoot, path);
            _targetStates.TryAdd(resolvedPath, CaptureState(resolvedPath));
        }
    }

    public void WaitForChangeAndTerminate(
        Task commitTask,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _interruptionTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (path, initialState) in _targetStates)
            {
                var currentState = CaptureState(path);
                if (currentState == initialState)
                {
                    continue;
                }

                if (_host.TryTerminate())
                {
                    return;
                }

                throw new InvalidOperationException(
                    "The durable commit completed before the runner could terminate the Host.");
            }

            if (commitTask.IsCompleted)
            {
                throw new InvalidOperationException(
                    "The durable commit completed before the runner could interrupt an applied replacement.");
            }

            Thread.SpinWait(64);
        }

        throw new TimeoutException(
            $"No changed commit target was observed within {_interruptionTimeout}.");
    }

    private static (bool Exists, long Length, long LastWriteTicks) CaptureState(
        string path)
    {
        var file = new FileInfo(path);
        return file.Exists
            ? (true, file.Length, file.LastWriteTimeUtc.Ticks)
            : (false, 0, 0);
    }

    private static string ResolvePath(string repositoryRoot, string path)
    {
        return Path.GetFullPath(
            Path.IsPathRooted(path)
                ? path
                : Path.Combine(repositoryRoot, path));
    }
}
