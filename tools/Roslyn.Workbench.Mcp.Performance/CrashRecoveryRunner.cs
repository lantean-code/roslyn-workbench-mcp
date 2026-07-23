using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class CrashRecoveryRunner
{
    private readonly PerformanceHost _host;
    private readonly string _repositoryRoot;
    private readonly string _stateDirectory;
    private readonly string _workspaceId;

    public CrashRecoveryRunner(
        PerformanceHost host,
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
        CancellationToken cancellationToken)
    {
        var interruptionStopwatch = Stopwatch.StartNew();
        var targetMonitor = new CrashRecoveryTargetMonitor(
            _host,
            _repositoryRoot,
            preparation.ChangedDocumentPaths);

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

        var appliedTargetPath = await FindAppliedReplacementAsync(
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

    private async Task<string> FindAppliedReplacementAsync(
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
                var appliedTargetPath = await TryFindAppliedReplacementAsync(
                    manifestPath,
                    cancellationToken);

                if (appliedTargetPath is not null)
                {
                    return appliedTargetPath;
                }
            }
        }

        throw new InvalidOperationException(
            "Host termination occurred before an applied replacement could be verified.");
    }

    private async Task<string?> TryFindAppliedReplacementAsync(
        string manifestPath,
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
                if (!IsReplace(entry.GetProperty("operation")))
                {
                    continue;
                }

                var intendedHash = entry.GetProperty("intendedHash").GetString();
                var targetPath = entry.GetProperty("targetPath").GetString();
                if (intendedHash is null || targetPath is null)
                {
                    continue;
                }

                var resolvedPath = ResolveRepositoryPath(targetPath);
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

    private static bool IsReplace(JsonElement operation)
    {
        return operation.ValueKind switch
        {
            JsonValueKind.Number => operation.GetInt32() == 1,
            JsonValueKind.String => string.Equals(
                operation.GetString(),
                "Replace",
                StringComparison.Ordinal),
            _ => false,
        };
    }
}
