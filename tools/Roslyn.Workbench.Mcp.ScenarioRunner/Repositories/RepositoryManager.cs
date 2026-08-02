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
        if (!Directory.Exists(Path.Combine(repositoryRoot, ".git")))
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

        var head = await GitCommand.RunAsync(
            ["rev-parse", "HEAD"],
            repositoryRoot,
            cancellationToken);
        var actualCommit = head.StandardOutput.Trim();
        if (head.ExitCode != 0 || !string.Equals(actualCommit, repository.Commit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Repository cache '{repositoryRoot}' is at '{actualCommit}', not pinned commit '{repository.Commit}'. Remove that cache directory explicitly before retrying.");
        }

        var status = await GitCommand.RunAsync(
            ["status", "--porcelain", "--untracked-files=no"],
            repositoryRoot,
            cancellationToken);
        if (status.ExitCode != 0 || !string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            throw new InvalidOperationException(
                $"Repository cache '{repositoryRoot}' contains tracked changes. Use a clean cache so the pinned scenario inputs remain reproducible.");
        }

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
        }

        return repositoryRoot;
    }

    private void EnsureNuGetConfigurationIsolation()
    {
        Directory.CreateDirectory(_cacheDirectory);

        var configurationPath = Path.Combine(_cacheDirectory, "NuGet.Config");
        File.WriteAllText(configurationPath, NuGetIsolationConfiguration);
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

    public static string GetNuGetPackagesDirectory(string repositoryRoot)
    {
        return Path.Combine(repositoryRoot, ".performance", "nuget-packages");
    }
}
