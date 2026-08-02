using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

internal sealed record DurableCommitMeasurement
{
    public required int Iteration { get; init; }

    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double CommitMilliseconds { get; init; }

    public required double CommitHostCpuMilliseconds { get; init; }

    public required double RestorationMilliseconds { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required HostMemoryMeasurement CommitMemory { get; init; }

    public required int CommitResponseBytes { get; init; }

    public required string CommitResponseSha256 { get; init; }

    public required int PreviewDocumentCount { get; init; }

    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }

    public string? DiagnosticArtifact { get; init; }

    public IReadOnlyList<PhaseTraceSummary> PhaseSummary { get; init; } = [];

    public int ChangedFileCount => Files.Count;

    public int CreatedFileCount => Files.Count(static file => file.Operation == DurableCommitFileOperation.Create);

    public int ReplacedFileCount => Files.Count(static file => file.Operation == DurableCommitFileOperation.Replace);

    public int DeletedFileCount => Files.Count(static file => file.Operation == DurableCommitFileOperation.Delete);

    public long OriginalBytes => Files.Sum(static file => file.OriginalBytes ?? 0);

    public long CommittedBytes => Files.Sum(static file => file.CommittedBytes ?? 0);
}
