using System.Diagnostics;

namespace Roslyn.Workbench.Mcp.Performance;

internal sealed class RepositoryRestorer
{
    private const int _restoreBatchSize = 100;
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly IReadOnlySet<string> _baselineUntrackedPaths;
    private readonly string _commit;
    private readonly string _repositoryRoot;

    private RepositoryRestorer(
        string repositoryRoot,
        string commit,
        IReadOnlySet<string> baselineUntrackedPaths)
    {
        _repositoryRoot = repositoryRoot;
        _commit = commit;
        _baselineUntrackedPaths = baselineUntrackedPaths;
    }

    public static async Task<RepositoryRestorer> CreateAsync(
        string repositoryRoot,
        string commit,
        CancellationToken cancellationToken)
    {
        var trackedChanges = await GetGitPathsAsync(
            repositoryRoot,
            ["diff", "--name-only", "-z", "--no-renames", commit, "--"],
            cancellationToken);
        if (trackedChanges.Count > 0)
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryRoot}' contains tracked changes before durable commit measurement.");
        }

        var untrackedPaths = await GetUntrackedPathsAsync(repositoryRoot, cancellationToken);
        return new RepositoryRestorer(repositoryRoot, commit, untrackedPaths);
    }

    public async Task<RepositoryChangeSet> CaptureChangesAsync(CancellationToken cancellationToken)
    {
        var trackedPaths = await GetGitPathsAsync(
            _repositoryRoot,
            ["diff", "--name-only", "-z", "--no-renames", _commit, "--"],
            cancellationToken);
        var currentUntrackedPaths = await GetUntrackedPathsAsync(_repositoryRoot, cancellationToken);
        var createdPaths = currentUntrackedPaths
            .Except(_baselineUntrackedPaths, PathComparer)
            .ToArray();
        var files = new List<DurableCommitFileChange>(trackedPaths.Count + createdPaths.Length);

        foreach (var path in trackedPaths)
        {
            var originalBytes = await GetCommittedFileSizeAsync(path, cancellationToken);
            var committedBytes = GetCurrentFileSize(path);
            files.Add(new DurableCommitFileChange
            {
                Path = path,
                Operation = committedBytes is null
                    ? DurableCommitFileOperation.Delete
                    : DurableCommitFileOperation.Replace,
                OriginalBytes = originalBytes,
                CommittedBytes = committedBytes,
            });
        }

        foreach (var path in createdPaths)
        {
            files.Add(new DurableCommitFileChange
            {
                Path = path,
                Operation = DurableCommitFileOperation.Create,
                CommittedBytes = GetCurrentFileSize(path),
            });
        }

        files.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
        return new RepositoryChangeSet { Files = files };
    }

    public async Task<double> RestoreAsync(
        RepositoryChangeSet changes,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var trackedPaths = changes.Files
            .Where(static file => file.Operation != DurableCommitFileOperation.Create)
            .Select(static file => file.Path)
            .ToArray();

        for (var offset = 0; offset < trackedPaths.Length; offset += _restoreBatchSize)
        {
            var count = Math.Min(_restoreBatchSize, trackedPaths.Length - offset);
            var arguments = new List<string>(count + 4)
            {
                "restore",
                $"--source={_commit}",
                "--worktree",
                "--",
            };

            for (var index = 0; index < count; index++)
            {
                arguments.Add(trackedPaths[offset + index]);
            }

            await RunRequiredGitAsync(arguments, cancellationToken);
        }

        foreach (var file in changes.Files)
        {
            if (file.Operation != DurableCommitFileOperation.Create)
            {
                continue;
            }

            var fullPath = ResolveRepositoryPath(file.Path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            DeleteEmptyCreatedDirectories(fullPath);
        }

        await VerifyRestoredAsync(cancellationToken);
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private async Task VerifyRestoredAsync(CancellationToken cancellationToken)
    {
        var trackedPaths = await GetGitPathsAsync(
            _repositoryRoot,
            ["diff", "--name-only", "-z", "--no-renames", _commit, "--"],
            cancellationToken);
        if (trackedPaths.Count > 0)
        {
            throw new InvalidOperationException(
                $"Repository restoration left tracked changes: {string.Join(", ", trackedPaths)}.");
        }

        var currentUntrackedPaths = await GetUntrackedPathsAsync(_repositoryRoot, cancellationToken);
        var newUntrackedPaths = currentUntrackedPaths
            .Except(_baselineUntrackedPaths, PathComparer)
            .ToArray();
        if (newUntrackedPaths.Length > 0)
        {
            throw new InvalidOperationException(
                $"Repository restoration left new untracked files: {string.Join(", ", newUntrackedPaths)}.");
        }
    }

    private async Task<long> GetCommittedFileSizeAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        var result = await GitCommand.RunAsync(
            ["cat-file", "-s", $"{_commit}:{relativePath}"],
            _repositoryRoot,
            cancellationToken);
        if (result.ExitCode != 0
            || !long.TryParse(result.StandardOutput.Trim(), CultureInfo.InvariantCulture, out var size))
        {
            throw new InvalidOperationException(
                $"Unable to read the committed size of '{relativePath}'.{Environment.NewLine}{result.StandardError}");
        }

        return size;
    }

    private long? GetCurrentFileSize(string relativePath)
    {
        var fullPath = ResolveRepositoryPath(relativePath);
        return File.Exists(fullPath) ? new FileInfo(fullPath).Length : null;
    }

    private string ResolveRepositoryPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_repositoryRoot, relativePath));
        var relativeToRoot = Path.GetRelativePath(_repositoryRoot, fullPath);
        if (relativeToRoot == ".."
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativeToRoot))
        {
            throw new InvalidDataException(
                $"Repository change path '{relativePath}' resolves outside '{_repositoryRoot}'.");
        }

        return fullPath;
    }

    private void DeleteEmptyCreatedDirectories(string createdFilePath)
    {
        var directory = Path.GetDirectoryName(createdFilePath);
        while (directory is not null
            && !string.Equals(directory, _repositoryRoot, PathComparison))
        {
            if (!Directory.Exists(directory)
                || Directory.EnumerateFileSystemEntries(directory).Any())
            {
                return;
            }

            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    private async Task RunRequiredGitAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await GitCommand.RunAsync(arguments, _repositoryRoot, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git repository restoration failed.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }
    }

    private static Task<IReadOnlySet<string>> GetUntrackedPathsAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        return GetGitPathSetAsync(
            repositoryRoot,
            ["ls-files", "--others", "--exclude-standard", "-z"],
            cancellationToken);
    }

    private static async Task<IReadOnlySet<string>> GetGitPathSetAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var paths = await GetGitPathsAsync(repositoryRoot, arguments, cancellationToken);
        return paths.ToHashSet(PathComparer);
    }

    private static async Task<IReadOnlyList<string>> GetGitPathsAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await GitCommand.RunAsync(arguments, repositoryRoot, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to inspect repository state.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
        }

        return result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
