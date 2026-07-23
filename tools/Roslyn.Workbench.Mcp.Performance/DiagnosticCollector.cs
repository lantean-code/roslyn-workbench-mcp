using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class DiagnosticCollector
{
    private const string _performanceProvider = "Roslyn-Workbench-Mcp:0xFFFFFFFFFFFFFFFF:4";
    private static readonly TimeSpan _collectionStartupDelay = TimeSpan.FromSeconds(1);
    private readonly string _frameworkRoot;

    public DiagnosticCollector(string frameworkRoot)
    {
        _frameworkRoot = frameworkRoot;
    }

    public Process StartDurationProfile(
        ProfileKind profile,
        int processId,
        TimeSpan duration,
        string outputPath)
    {
        var arguments = profile switch
        {
            ProfileKind.Trace => CreateTraceArguments(processId, duration, outputPath),
            ProfileKind.Counters => CreateCountersArguments(processId, duration, outputPath),
            ProfileKind.GcDump => throw new InvalidOperationException("GC dumps are point-in-time captures."),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown profile kind."),
        };

        return ExternalCommand.Start("dotnet", arguments, _frameworkRoot);
    }

    public static async Task WaitForCollectionStartAsync(
        Process diagnosticProcess,
        CancellationToken cancellationToken)
    {
        await Task.Delay(_collectionStartupDelay, cancellationToken);
        if (diagnosticProcess.HasExited)
        {
            throw new InvalidOperationException(
                $"The diagnostic collector exited before collection started with code {diagnosticProcess.ExitCode}.");
        }
    }

    public async Task CollectGcDumpAsync(
        int processId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = await ExternalCommand.RunAsync(
            "dotnet",
            [
                "tool",
                "run",
                "dotnet-gcdump",
                "--",
                "collect",
                "--process-id",
                processId.ToString(CultureInfo.InvariantCulture),
                "--output",
                outputPath,
            ],
            _frameworkRoot,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet-gcdump failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }
    }

    public static async Task EnsureToolsRestoredAsync(
        string frameworkRoot,
        CancellationToken cancellationToken)
    {
        var result = await ExternalCommand.RunAsync(
            "dotnet",
            ["tool", "restore"],
            frameworkRoot,
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The diagnostics tools could not be restored.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }
    }

    private static IReadOnlyList<string> CreateTraceArguments(
        int processId,
        TimeSpan duration,
        string outputPath)
    {
        return
        [
            "tool",
            "run",
            "dotnet-trace",
            "--",
            "collect",
            "--process-id",
            processId.ToString(CultureInfo.InvariantCulture),
            "--profile",
            "dotnet-sampled-thread-time",
            "--providers",
            _performanceProvider,
            "--duration",
            FormatDuration(duration),
            "--output",
            outputPath,
        ];
    }

    private static IReadOnlyList<string> CreateCountersArguments(
        int processId,
        TimeSpan duration,
        string outputPath)
    {
        return
        [
            "tool",
            "run",
            "dotnet-counters",
            "--",
            "collect",
            "--process-id",
            processId.ToString(CultureInfo.InvariantCulture),
            "--counters",
            "System.Runtime",
            "--format",
            "json",
            "--duration",
            FormatDuration(duration),
            "--output",
            outputPath,
        ];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }
}
