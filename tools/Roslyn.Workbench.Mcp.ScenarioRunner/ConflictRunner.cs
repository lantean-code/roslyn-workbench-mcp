using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed class ConflictRunner
{
    private static readonly byte[] _externalMarker = Encoding.UTF8.GetBytes(
        $"{Environment.NewLine}// Roslyn Workbench external conflict marker{Environment.NewLine}");
    private static readonly TimeSpan _manifestTimeout = TimeSpan.FromMinutes(2);
    private readonly ScenarioHost _host;
    private readonly string _repositoryRoot;
    private readonly string _stateDirectory;
    private readonly string _workspaceId;

    public ConflictRunner(
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

    public async Task<ConflictExecution> ExecuteAsync(
        ScenarioDefinition scenario,
        DurableCommitPreparation preparation,
        CancellationToken cancellationToken)
    {
        var definition = scenario.Conflict
            ?? throw new InvalidOperationException(
                $"Conflict scenario '{scenario.Id}' does not define a conflict mode.");

        return definition.Mode switch
        {
            ConflictMode.PreWriteDrift => await ExecutePreWriteDriftAsync(
                definition,
                preparation,
                cancellationToken),
            ConflictMode.DuringApplication => await ExecuteDuringApplicationAsync(
                preparation,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario),
                definition.Mode,
                "Unknown conflict mode."),
        };
    }

    private async Task<ConflictExecution> ExecutePreWriteDriftAsync(
        ConflictDefinition definition,
        DurableCommitPreparation preparation,
        CancellationToken cancellationToken)
    {
        var path = definition.ExternalChangePath
            ?? throw new InvalidOperationException(
                "A pre-write drift scenario requires an externalChangePath.");
        var externalMutation = await MutateFileAsync(
            ResolveRepositoryPath(path),
            expectedOriginalSha256: null,
            cancellationToken);

        var commitStopwatch = Stopwatch.StartNew();
        var result = await _host.CallToolAsync(
            "transaction-commit",
            CreateWorkspaceArguments(),
            cancellationToken);
        commitStopwatch.Stop();

        var (errorCode, requiredAction) = ReadError(result);
        if (!string.Equals(errorCode, "TransactionConflicted", StringComparison.Ordinal)
            || !string.Equals(requiredAction, "RollbackTransaction", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Pre-write drift returned '{errorCode}'/'{requiredAction}' instead of TransactionConflicted/RollbackTransaction.");
        }

        await InvokeRequiredAsync("transaction-rollback", cancellationToken);
        return new ConflictExecution
        {
            StagingMilliseconds = preparation.StagingMilliseconds,
            PreviewMilliseconds = preparation.PreviewMilliseconds,
            CommitMilliseconds = commitStopwatch.Elapsed.TotalMilliseconds,
            ConflictDetectionMilliseconds = commitStopwatch.Elapsed.TotalMilliseconds,
            RecoveryMilliseconds = 0,
            ErrorCode = errorCode,
            RequiredAction = requiredAction,
            ExternalMutation = externalMutation,
        };
    }

    private async Task<ConflictExecution> ExecuteDuringApplicationAsync(
        DurableCommitPreparation preparation,
        CancellationToken cancellationToken)
    {
        var commitStopwatch = Stopwatch.StartNew();
        var injectionTask = InjectWhenApplyingAsync(cancellationToken);
        var commitTask = _host.CallToolAsync(
            "transaction-commit",
            CreateWorkspaceArguments(),
            cancellationToken).AsTask();

        var externalMutation = await injectionTask;
        var conflictDetectionMilliseconds = commitStopwatch.Elapsed.TotalMilliseconds;
        var recoveryStopwatch = Stopwatch.StartNew();
        var result = await commitTask;
        recoveryStopwatch.Stop();
        commitStopwatch.Stop();

        var (errorCode, requiredAction) = ReadError(result);
        if (!string.Equals(errorCode, "CommitFailed", StringComparison.Ordinal)
            || !string.Equals(requiredAction, "ResolveRecovery", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"In-progress conflict returned '{errorCode}'/'{requiredAction}' instead of CommitFailed/ResolveRecovery.");
        }

        return new ConflictExecution
        {
            StagingMilliseconds = preparation.StagingMilliseconds,
            PreviewMilliseconds = preparation.PreviewMilliseconds,
            CommitMilliseconds = commitStopwatch.Elapsed.TotalMilliseconds,
            ConflictDetectionMilliseconds = conflictDetectionMilliseconds,
            RecoveryMilliseconds = recoveryStopwatch.Elapsed.TotalMilliseconds,
            ErrorCode = errorCode,
            RequiredAction = requiredAction,
            ExternalMutation = externalMutation,
        };
    }

    private async Task<ExternalFileMutation> InjectWhenApplyingAsync(
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_manifestTimeout);
        var recoveryDirectory = Path.Combine(_stateDirectory, "recovery");

        while (true)
        {
            timeoutSource.Token.ThrowIfCancellationRequested();
            if (Directory.Exists(recoveryDirectory))
            {
                foreach (var manifestPath in Directory.EnumerateFiles(
                    recoveryDirectory,
                    "manifest.json",
                    SearchOption.AllDirectories))
                {
                    var target = await TryReadApplyingTargetAsync(
                        manifestPath,
                        timeoutSource.Token);
                    if (target is not null)
                    {
                        return await MutateFileAsync(
                            target.Value.Path,
                            target.Value.OriginalSha256,
                            timeoutSource.Token);
                    }
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2), timeoutSource.Token);
        }
    }

    private async Task<(string Path, string OriginalSha256)?> TryReadApplyingTargetAsync(
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

            string? targetPath = null;
            string? originalSha256 = null;
            foreach (var entry in root.GetProperty("entries").EnumerateArray())
            {
                if (!IsReplace(entry.GetProperty("operation")))
                {
                    continue;
                }

                targetPath = entry.GetProperty("targetPath").GetString();
                originalSha256 = entry.GetProperty("originalHash").GetString();
            }

            if (targetPath is null || originalSha256 is null)
            {
                throw new InvalidDataException(
                    $"Applying manifest '{manifestPath}' contains no replacement target.");
            }

            return (ResolveRepositoryPath(targetPath), originalSha256);
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

    private static async Task<ExternalFileMutation> MutateFileAsync(
        string path,
        string? expectedOriginalSha256,
        CancellationToken cancellationToken)
    {
        var original = await File.ReadAllBytesAsync(path, cancellationToken);
        var originalSha256 = Hash(original);
        if (expectedOriginalSha256 is not null
            && !string.Equals(
                originalSha256,
                expectedOriginalSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The selected external-conflict target '{path}' changed before injection.");
        }

        var external = new byte[original.Length + _externalMarker.Length];
        original.CopyTo(external, 0);
        _externalMarker.CopyTo(external, original.Length);
        await File.WriteAllBytesAsync(path, external, cancellationToken);

        return new ExternalFileMutation
        {
            Path = path,
            OriginalSha256 = originalSha256,
            ExternalSha256 = Hash(external),
            OriginalBytes = original.Length,
            ExternalBytes = external.Length,
        };
    }

    private async Task InvokeRequiredAsync(string tool, CancellationToken cancellationToken)
    {
        var result = await _host.CallToolAsync(
            tool,
            CreateWorkspaceArguments(),
            cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException(
                $"Tool '{tool}' returned an MCP error: {result.StructuredContent?.GetRawText()}");
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
        var candidatePath = path;
        if (!Path.IsPathRooted(candidatePath))
        {
            candidatePath = Path.Combine(_repositoryRoot, candidatePath);
        }

        var fullPath = Path.GetFullPath(candidatePath);
        var relativePath = Path.GetRelativePath(_repositoryRoot, fullPath);
        if (relativePath == ".."
            || relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"External-conflict path '{path}' resolves outside '{_repositoryRoot}'.");
        }

        return fullPath;
    }

    private static (string ErrorCode, string? RequiredAction) ReadError(
        CallToolResult result)
    {
        if (result.IsError != true || result.StructuredContent is not { } content)
        {
            throw new InvalidOperationException(
                "The controlled conflict unexpectedly returned a successful MCP result.");
        }

        var errorCode = content
            .GetProperty("error")
            .GetProperty("code")
            .GetString()
            ?? throw new InvalidDataException(
                "The controlled conflict result contains no error code.");
        var requiredAction = content.TryGetProperty("next", out var next)
            ? next.GetString()
            : null;
        return (errorCode, requiredAction);
    }

    private static string Hash(ReadOnlySpan<byte> contents)
    {
        return Convert.ToHexString(SHA256.HashData(contents));
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
