using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

internal static class RunStateValidator
{
    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static IReadOnlySet<string> CaptureWorkspaceStateFiles(string repositoryRoot)
    {
        var root = Path.Combine(repositoryRoot, ".vs", "roslyn-workbench-mcp");
        if (!Directory.Exists(root))
        {
            return new HashSet<string>(PathComparer);
        }

        return Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToHashSet(PathComparer);
    }

    public static void RestoreWorkspaceStateFiles(
        string repositoryRoot,
        IReadOnlySet<string> initialWorkspaceStateFiles)
    {
        var root = Path.Combine(repositoryRoot, ".vs", "roslyn-workbench-mcp");
        if (!Directory.Exists(root))
        {
            return;
        }

        var currentFiles = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToArray();
        foreach (var path in currentFiles)
        {
            if (!initialWorkspaceStateFiles.Contains(path))
            {
                File.Delete(path);
            }
        }

        var directories = Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderDescending()
            .ToArray();
        foreach (var directory in directories)
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(root).Any())
        {
            Directory.Delete(root);
        }
    }

    public static async Task<RunValidationResult> ValidateAsync(
        RepositoryDefinition repository,
        string repositoryRoot,
        string stateDirectory,
        IReadOnlySet<string> initialWorkspaceStateFiles,
        HostShutdownResult shutdown,
        CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var head = await GitCommand.RunAsync(
            ["rev-parse", "HEAD"],
            repositoryRoot,
            cancellationToken);
        var actualCommit = head.ExitCode == 0 ? head.StandardOutput.Trim() : null;
        if (!string.Equals(actualCommit, repository.Commit, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Repository HEAD is '{actualCommit ?? "unavailable"}', expected '{repository.Commit}'.");
        }

        var status = await GitCommand.RunAsync(
            ["status", "--porcelain", "--untracked-files=normal"],
            repositoryRoot,
            cancellationToken);
        if (status.ExitCode != 0)
        {
            issues.Add("Unable to validate the repository state.");
        }
        else if (!string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            issues.Add("The repository contains tracked or untracked changes after the run.");
        }

        var stateFiles = Directory.Exists(stateDirectory)
            ? Directory.EnumerateFiles(stateDirectory, "*", SearchOption.AllDirectories).Order(PathComparer).ToArray()
            : [];
        if (stateFiles.Length > 0)
        {
            issues.Add("The Host state directory contains unfinished recovery state.");
        }

        var finalWorkspaceStateFiles = CaptureWorkspaceStateFiles(repositoryRoot);
        var newWorkspaceStateFiles = finalWorkspaceStateFiles
            .Except(initialWorkspaceStateFiles, PathComparer)
            .Order(PathComparer)
            .ToArray();
        if (newWorkspaceStateFiles.Length > 0)
        {
            issues.Add("The run left new workspace coordination or locking files behind.");
        }

        if (shutdown.ForcedTermination)
        {
            issues.Add("The Host did not stop within the shutdown timeout and was terminated.");
        }

        if (shutdown.ExitCode is not 0)
        {
            issues.Add($"The Host exited with code {shutdown.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}.");
        }

        return new RunValidationResult
        {
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ExpectedCommit = repository.Commit,
            ActualCommit = actualCommit,
            HostShutdown = shutdown,
            StateFiles = stateFiles,
            NewWorkspaceStateFiles = newWorkspaceStateFiles,
            Issues = issues,
        };
    }
}
