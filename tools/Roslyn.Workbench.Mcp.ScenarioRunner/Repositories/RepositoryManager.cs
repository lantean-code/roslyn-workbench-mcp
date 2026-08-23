using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

internal sealed class RepositoryManager
{
    private const string NuGetIsolationConfiguration = """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSourceMapping>
            <clear />
          </packageSourceMapping>
        </configuration>
        """;

    private readonly string _cacheDirectory;

    public RepositoryManager(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public async Task<string> PrepareAsync(
        RepositoryDefinition repository,
        bool runPreparation,
        CancellationToken cancellationToken)
    {
        EnsureNuGetConfigurationIsolation();

        var repositoryRoot = Path.Combine(_cacheDirectory, repository.Id, repository.Commit);
        await RecreateInvalidCacheAsync(
            repositoryRoot,
            repository.Commit,
            cancellationToken);

        if (!Directory.Exists(repositoryRoot))
        {
            Directory.CreateDirectory(Path.Combine(_cacheDirectory, repository.Id));
            await RunRequiredAsync(
                "git",
                GitCommand.ConfigureArguments(["clone", "--filter=blob:none", "--no-checkout", repository.Url, repositoryRoot]),
                _cacheDirectory,
                cancellationToken);
            await RunRequiredAsync(
                "git",
                GitCommand.ConfigureArguments(["checkout", "--detach", repository.Commit]),
                repositoryRoot,
                cancellationToken);
        }

        await ValidatePinnedCheckoutAsync(
            repositoryRoot,
            repository.Commit,
            "before preparation",
            cancellationToken);

        if (runPreparation)
        {
            var environment = new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = GetNuGetPackagesDirectory(repositoryRoot),
            };

            foreach (var command in repository.Preparation)
            {
                var fileName = OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(command.WindowsFileName)
                    ? command.WindowsFileName
                    : command.FileName;
                var arguments = OperatingSystem.IsWindows() && command.WindowsArguments is not null
                    ? command.WindowsArguments
                    : command.Arguments;

                await RunRequiredAsync(
                    fileName,
                    arguments,
                    repositoryRoot,
                    cancellationToken,
                    environment);
            }

            await ValidatePinnedCheckoutAsync(
                repositoryRoot,
                repository.Commit,
                "after preparation",
                cancellationToken);
        }

        return repositoryRoot;
    }

    public static string GetNuGetPackagesDirectory(string repositoryRoot)
    {
        var repositoryDirectory = new DirectoryInfo(repositoryRoot);
        var repositoryCacheDirectory = repositoryDirectory.Parent
            ?? throw new InvalidOperationException(
                $"Repository root '{repositoryRoot}' does not have a cache directory.");

        return Path.Combine(
            repositoryCacheDirectory.FullName,
            ".packages",
            repositoryDirectory.Name);
    }

    private void EnsureNuGetConfigurationIsolation()
    {
        Directory.CreateDirectory(_cacheDirectory);

        var configurationPath = Path.Combine(_cacheDirectory, "NuGet.Config");
        File.WriteAllText(configurationPath, NuGetIsolationConfiguration);
    }

    private static async Task RecreateInvalidCacheAsync(
        string repositoryRoot,
        string expectedCommit,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(repositoryRoot))
        {
            return;
        }

        var validationFailure = await GetPinnedCheckoutValidationFailureAsync(
            repositoryRoot,
            expectedCommit,
            "before preparation",
            cancellationToken);
        if (validationFailure is null)
        {
            return;
        }

        Console.WriteLine($"Recreating invalid repository cache '{repositoryRoot}'. {validationFailure}");
        RemoveReadOnlyAttributes(repositoryRoot);
        Directory.Delete(repositoryRoot, recursive: true);
    }

    private static void RemoveReadOnlyAttributes(string repositoryRoot)
    {
        var directories = new Stack<DirectoryInfo>();
        directories.Push(new DirectoryInfo(repositoryRoot));

        while (directories.TryPop(out var directory))
        {
            directory.Attributes &= ~FileAttributes.ReadOnly;

            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                entry.Attributes &= ~FileAttributes.ReadOnly;

                if (entry is DirectoryInfo childDirectory
                    && !entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    directories.Push(childDirectory);
                }
            }
        }
    }

    private static async Task RunRequiredAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        Console.WriteLine($"> {fileName} {string.Join(' ', arguments)}");
        var result = await ExternalCommand.RunAsync(
            fileName,
            arguments,
            workingDirectory,
            cancellationToken,
            environment);
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Command '{fileName}' failed with exit code {result.ExitCode}.{Environment.NewLine}{result.StandardError}{result.StandardOutput}");
    }

    private static async Task ValidatePinnedCheckoutAsync(
        string repositoryRoot,
        string expectedCommit,
        string validationPoint,
        CancellationToken cancellationToken)
    {
        var validationFailure = await GetPinnedCheckoutValidationFailureAsync(
            repositoryRoot,
            expectedCommit,
            validationPoint,
            cancellationToken);
        if (validationFailure is not null)
        {
            throw new InvalidOperationException(validationFailure);
        }
    }

    private static async Task<string?> GetPinnedCheckoutValidationFailureAsync(
        string repositoryRoot,
        string expectedCommit,
        string validationPoint,
        CancellationToken cancellationToken)
    {
        var head = await GitCommand.RunAsync(
            ["rev-parse", "HEAD"],
            repositoryRoot,
            cancellationToken);
        if (head.ExitCode != 0)
        {
            return $"Unable to inspect repository cache '{repositoryRoot}' {validationPoint}.{Environment.NewLine}{head.StandardError}";
        }

        var actualCommit = head.StandardOutput.Trim();
        if (!string.Equals(actualCommit, expectedCommit, StringComparison.OrdinalIgnoreCase))
        {
            return $"Repository cache '{repositoryRoot}' is at '{actualCommit}' {validationPoint}, not pinned commit '{expectedCommit}'.";
        }

        var status = await GitCommand.RunAsync(
            ["status", "--porcelain", "--untracked-files=normal"],
            repositoryRoot,
            cancellationToken);
        if (status.ExitCode != 0)
        {
            return $"Unable to inspect changes in repository cache '{repositoryRoot}' {validationPoint}.{Environment.NewLine}{status.StandardError}";
        }

        var changes = status.StandardOutput.Trim();
        if (!string.IsNullOrWhiteSpace(changes))
        {
            return $"Repository cache '{repositoryRoot}' contains tracked or untracked changes {validationPoint}.{Environment.NewLine}{changes}";
        }

        return null;
    }
}
