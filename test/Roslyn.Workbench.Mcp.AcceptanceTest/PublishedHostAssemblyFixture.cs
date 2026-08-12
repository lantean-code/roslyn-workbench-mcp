using System.Diagnostics;
using System.Reflection;

[assembly: AssemblyFixture(typeof(Roslyn.Workbench.Mcp.AcceptanceTest.PublishedHostAssemblyFixture))]

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed class PublishedHostAssemblyFixture : IAsyncLifetime
{
    private const string _configuration = "Release";
    private const string _hostProjectRelativePath = "src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj";
    private const string _repositoryRootMetadataName = "RepositoryRoot";
    private const string _sentryDsnEnvironmentVariableName = "ROSLYN_WORKBENCH_SENTRY_DSN";
    private const string _wslArtifactsPath = "/tmp/artifacts/roslyn-workbench-mcp";
    private static readonly TimeSpan _publishTimeout = TimeSpan.FromMinutes(5);

    private string? _previousHostPath;
    private string? _previousSentryDsn;
    private string? _publishRoot;

    public PublishedHostAssemblyFixture()
    {
    }

    public async ValueTask InitializeAsync()
    {
        _previousHostPath = Environment.GetEnvironmentVariable(PublishedHostExecutable.EnvironmentVariableName);
        _previousSentryDsn = Environment.GetEnvironmentVariable(_sentryDsnEnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(_sentryDsnEnvironmentVariableName, string.Empty);
            if (!string.IsNullOrWhiteSpace(_previousHostPath))
            {
                PublishedHostExecutable.Resolve(_previousHostPath);
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            _publishRoot = CreatePublishRoot();
            var hostOutput = Path.Combine(_publishRoot, "host");

            await PublishHostAsync(repositoryRoot, hostOutput, TestContext.Current.CancellationToken);

            var executableName = OperatingSystem.IsWindows()
                ? "Roslyn.Workbench.Mcp.exe"
                : "Roslyn.Workbench.Mcp";
            var executablePath = Path.Combine(hostOutput, executableName);
            PublishedHostExecutable.Resolve(executablePath);
            Environment.SetEnvironmentVariable(PublishedHostExecutable.EnvironmentVariableName, executablePath);
        }
        catch
        {
            await RestoreEnvironmentAndDeletePublishAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await RestoreEnvironmentAndDeletePublishAsync();
    }

    private static string CreatePublishRoot()
    {
        var publishRoot = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp",
            "acceptance",
            "publish",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(publishRoot);
        return publishRoot;
    }

    private static string ResolveRepositoryRoot()
    {
        var repositoryRoot = typeof(PublishedHostAssemblyFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(attribute.Key, _repositoryRootMetadataName, StringComparison.Ordinal))
            ?.Value;

        if (string.IsNullOrWhiteSpace(repositoryRoot)
            || !File.Exists(Path.Combine(repositoryRoot, "Roslyn.Workbench.Mcp.slnx")))
        {
            throw new DirectoryNotFoundException(
                "The repository root embedded in the acceptance test assembly is unavailable. "
                + $"Set {PublishedHostExecutable.EnvironmentVariableName} to use an existing published Host.");
        }

        return Path.GetFullPath(repositoryRoot);
    }

    private static async Task PublishHostAsync(
        string repositoryRoot,
        string hostOutput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveDotNetPath(),
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, _hostProjectRelativePath));
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(_configuration);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(hostOutput);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--disable-build-servers");
        if (IsWsl())
        {
            startInfo.ArgumentList.Add($"--artifacts-path={_wslArtifactsPath}");
        }

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("The .NET SDK process could not be started to publish the acceptance Host.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_publishTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException exception)
        {
            TryTerminate(process);
            await process.WaitForExitAsync(CancellationToken.None);

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            var timedOutStandardOutput = await standardOutputTask;
            var timedOutStandardError = await standardErrorTask;
            throw new TimeoutException(
                $"Publishing the acceptance Host did not complete within {_publishTimeout}."
                + Environment.NewLine
                + FormatProcessOutput(timedOutStandardOutput, timedOutStandardError),
                exception);
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Publishing the acceptance Host failed with exit code {process.ExitCode}."
                + Environment.NewLine
                + FormatProcessOutput(standardOutput, standardError));
        }
    }

    private static string ResolveDotNetPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var installedPath = Path.Combine(programFiles, "dotnet", "dotnet.exe");
            if (File.Exists(installedPath))
            {
                return installedPath;
            }
        }

        return "dotnet";
    }

    private static bool IsWsl()
    {
        const string osReleasePath = "/proc/sys/kernel/osrelease";

        return OperatingSystem.IsLinux()
            && File.Exists(osReleasePath)
            && File.ReadAllText(osReleasePath).Contains("microsoft", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatProcessOutput(string standardOutput, string standardError)
    {
        return $"Standard output:{Environment.NewLine}{standardOutput}{Environment.NewLine}"
            + $"Standard error:{Environment.NewLine}{standardError}";
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task RestoreEnvironmentAndDeletePublishAsync()
    {
        Environment.SetEnvironmentVariable(PublishedHostExecutable.EnvironmentVariableName, _previousHostPath);
        Environment.SetEnvironmentVariable(_sentryDsnEnvironmentVariableName, _previousSentryDsn);

        var publishRoot = Interlocked.Exchange(ref _publishRoot, null);
        if (publishRoot is not null)
        {
            await AcceptanceScenarioCleanup.DeleteAsync(publishRoot);
        }
    }
}
