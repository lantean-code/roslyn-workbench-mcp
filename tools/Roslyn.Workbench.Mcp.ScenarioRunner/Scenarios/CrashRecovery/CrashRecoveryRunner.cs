using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CrashRecovery;

internal sealed class CrashRecoveryRunner
{
    private readonly ScenarioHost _host;
    private readonly string _repositoryRoot;
    private readonly string _stateDirectory;
    private readonly string _workspaceId;

    public CrashRecoveryRunner(
        ScenarioHost host,
        string workspaceId,
        string repositoryRoot,
        string stateDirectory)
    {
        _host = host;
        _workspaceId = workspaceId;
        _repositoryRoot = repositoryRoot;
        _stateDirectory = stateDirectory;
    }

    public async Task<CrashRecoveryInterruption> InterruptAsync(
        DurableCommitPreparation preparation,
        DurableCommitFileOperation? requiredOperation,
        CancellationToken cancellationToken)
    {
        var interruptionStopwatch = Stopwatch.StartNew();
        var targetMonitor = new CrashRecoveryTargetMonitor(
            _host,
            _repositoryRoot,
            preparation.ChangedTargets,
            requiredOperation);

        var commitTask = _host.CallToolAsync(
            "transaction-commit",
            CreateWorkspaceArguments(),
            cancellationToken).AsTask();

        targetMonitor.WaitForChangeAndTerminate(
            commitTask,
            cancellationToken);

        await _host.TerminateAsync();
        interruptionStopwatch.Stop();
        await ObserveInterruptedCallAsync(commitTask);

        var recoveryEvidence = await RecoveryEvidenceReader.ReadAsync(
            _stateDirectory,
            cancellationToken);

        var appliedTargetPath = await FindAppliedMutationAsync(
            requiredOperation,
            cancellationToken);

        if (!string.Equals(
            recoveryEvidence.State,
            "Applying",
            StringComparison.Ordinal)
            || recoveryEvidence.ArtifactCount == 0)
        {
            throw new InvalidOperationException(
                "The terminated Host did not leave an Applying recovery manifest with durable artifacts.");
        }

        return new CrashRecoveryInterruption
        {
            StagingMilliseconds = preparation.StagingMilliseconds,
            PreviewMilliseconds = preparation.PreviewMilliseconds,
            InterruptionMilliseconds = interruptionStopwatch.Elapsed.TotalMilliseconds,
            AppliedTargetPath = appliedTargetPath,
            RecoveryEvidence = recoveryEvidence,
            HostShutdown = _host.GetShutdownResult(),
        };
    }

    private async Task<string> FindAppliedMutationAsync(
        DurableCommitFileOperation? requiredOperation,
        CancellationToken cancellationToken)
    {
        var recoveryDirectory = Path.Combine(_stateDirectory, "recovery");
        if (Directory.Exists(recoveryDirectory))
        {
            foreach (var manifestPath in Directory.EnumerateFiles(
                recoveryDirectory,
                "manifest.json",
                SearchOption.AllDirectories))
            {
                var appliedTargetPath = await TryFindAppliedMutationAsync(
                    manifestPath,
                    requiredOperation,
                    cancellationToken);

                if (appliedTargetPath is not null)
                {
                    return appliedTargetPath;
                }
            }
        }

        throw new InvalidOperationException(
            "Host termination occurred before an applied mutation could be verified.");
    }

    private async Task<string?> TryFindAppliedMutationAsync(
        string manifestPath,
        DurableCommitFileOperation? requiredOperation,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            var root = document.RootElement;
            if (!string.Equals(
                root.GetProperty("state").GetString(),
                "Applying",
                StringComparison.Ordinal))
            {
                return null;
            }

            foreach (var entry in root.GetProperty("entries").EnumerateArray())
            {
                var operation = ParseOperation(entry.GetProperty("operation"));
                if (operation is null
                    || (requiredOperation is not null
                        && operation != requiredOperation))
                {
                    continue;
                }

                var targetPath = entry.GetProperty("targetPath").GetString();
                if (targetPath is null)
                {
                    continue;
                }

                var resolvedPath = ResolveRepositoryPath(targetPath);
                if (operation == DurableCommitFileOperation.Delete)
                {
                    var deleteMarkerPath = entry.GetProperty("deleteMarkerPath").GetString();
                    if (!File.Exists(resolvedPath)
                        && deleteMarkerPath is not null
                        && File.Exists(ResolveRepositoryPath(deleteMarkerPath)))
                    {
                        return resolvedPath;
                    }

                    continue;
                }

                var intendedHash = entry.GetProperty("intendedHash").GetString();
                if (intendedHash is null)
                {
                    continue;
                }

                if (!File.Exists(resolvedPath))
                {
                    continue;
                }

                var currentHash = await HashFileAsync(
                    resolvedPath,
                    cancellationToken);

                if (string.Equals(
                    currentHash,
                    intendedHash,
                    StringComparison.Ordinal))
                {
                    return resolvedPath;
                }
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Dictionary<string, object?> CreateWorkspaceArguments()
    {
        return new Dictionary<string, object?>
        {
            ["workspace"] = new Dictionary<string, object?>
            {
                ["workspaceId"] = _workspaceId,
            },
        };
    }

    private string ResolveRepositoryPath(string path)
    {
        var candidatePath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(_repositoryRoot, path);

        var fullPath = Path.GetFullPath(candidatePath);
        var relativePath = Path.GetRelativePath(_repositoryRoot, fullPath);
        if (relativePath == ".."
            || relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"Recovery target '{path}' resolves outside '{_repositoryRoot}'.");
        }

        return fullPath;
    }

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "An intentionally terminated stdio Host can surface different transport exception types; only an MCP result reaching the client is an unexpected outcome.")]
    private static async Task ObserveInterruptedCallAsync(Task<CallToolResult> commitTask)
    {
        var returnedResult = false;
        try
        {
            _ = await commitTask;
            returnedResult = true;
        }
        catch (Exception)
        {
        }

        if (returnedResult)
        {
            throw new InvalidOperationException(
                "The interrupted transaction commit returned an MCP result after Host termination.");
        }
    }

    private static DurableCommitFileOperation? ParseOperation(JsonElement operation)
    {
        return operation.ValueKind switch
        {
            JsonValueKind.Number when operation.TryGetInt32(out var value)
                && Enum.IsDefined((DurableCommitFileOperation)value)
                => (DurableCommitFileOperation)value,
            JsonValueKind.String when Enum.TryParse<DurableCommitFileOperation>(
                operation.GetString(),
                ignoreCase: false,
                out var value)
                => value,
            _ => null,
        };
    }
}
