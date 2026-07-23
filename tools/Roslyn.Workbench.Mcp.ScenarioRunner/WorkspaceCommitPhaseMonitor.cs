using System.Diagnostics;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed class WorkspaceCommitPhaseMonitor
{
    private static readonly TimeSpan _phaseTimeout = TimeSpan.FromMinutes(2);
    private readonly string _instanceDirectory;

    public WorkspaceCommitPhaseMonitor(string repositoryRoot)
    {
        _instanceDirectory = Path.Combine(
            repositoryRoot,
            ".vs",
            "roslyn-workbench-mcp",
            "instances");
    }

    public void WaitForPhase(
        string expectedPhase,
        Task commitTask,
        CancellationToken cancellationToken)
    {
        WaitForPhase(
            expectedPhase,
            commitTask,
            failWhenCommitCompletes: true,
            cancellationToken);
    }

    public void WaitForTerminalPhase(
        string expectedPhase,
        CancellationToken cancellationToken)
    {
        WaitForPhase(
            expectedPhase,
            commitTask: null,
            failWhenCommitCompletes: false,
            cancellationToken);
    }

    private void WaitForPhase(
        string expectedPhase,
        Task? commitTask,
        bool failWhenCommitCompletes,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _phaseTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryReadPhase() is { } phase
                && string.Equals(
                    phase,
                    expectedPhase,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (failWhenCommitCompletes
                && commitTask?.IsCompleted == true)
            {
                throw new InvalidOperationException(
                    $"The commit completed before phase '{expectedPhase}' could be observed.");
            }

            Thread.SpinWait(64);
        }

        throw new TimeoutException(
            $"Commit phase '{expectedPhase}' was not observed within {_phaseTimeout}.");
    }

    private string? TryReadPhase()
    {
        if (!Directory.Exists(_instanceDirectory))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(
            _instanceDirectory,
            "*.json",
            SearchOption.TopDirectoryOnly))
        {
            var phase = TryReadPhase(path);
            if (phase is not null)
            {
                return phase;
            }
        }

        return null;
    }

    private static string? TryReadPhase(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty(
                "commitPhase",
                out var phase)
                || phase.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return phase.GetString();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
