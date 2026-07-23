using System.Diagnostics;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CrashRecovery;

internal sealed class CrashRecoveryTargetMonitor
{
    private static readonly TimeSpan _interruptionTimeout = TimeSpan.FromMinutes(2);
    private readonly ScenarioHost _host;
    private readonly Dictionary<string, (bool Exists, long Length, long LastWriteTicks)> _targetStates;

    public CrashRecoveryTargetMonitor(
        ScenarioHost host,
        string repositoryRoot,
        IReadOnlyList<DurableCommitTarget> targets,
        DurableCommitFileOperation? requiredOperation)
    {
        _host = host;
        _targetStates = new Dictionary<string, (bool, long, long)>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);

        foreach (var target in targets)
        {
            if (requiredOperation is not null
                && target.Operation != requiredOperation)
            {
                continue;
            }

            var resolvedPath = ResolvePath(repositoryRoot, target.Path);
            _targetStates.TryAdd(resolvedPath, CaptureState(resolvedPath));
        }

        if (_targetStates.Count == 0)
        {
            throw new InvalidOperationException(
                requiredOperation is null
                    ? "The staged mutation did not expose a target to monitor."
                    : $"The staged mutation did not expose a {requiredOperation} target to monitor.");
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
