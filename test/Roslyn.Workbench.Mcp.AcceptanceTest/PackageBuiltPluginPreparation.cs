using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

/// <summary>
/// Builds an external query-and-mutation plugin from the packed authoring product for published-Host acceptance coverage.
/// </summary>
internal static class PackageBuiltPluginPreparation
{
    private const string _localPackageVersion = "0.0.0-acceptance";
    private const string _packageId = "Lantean.Roslyn.Workbench.Mcp.Plugins";
    private const string _nuGetSource = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// Builds and installs the clean package consumer beneath the supplied acceptance plugin root.
    /// </summary>
    /// <param name="pluginRoot">The isolated plugin directory read by the acceptance Host.</param>
    /// <param name="cancellationToken">The token used to cancel package preparation.</param>
    /// <returns>A task that completes when the plugin package is ready for Host startup.</returns>
    public static async Task PrepareAsync(
        string pluginRoot,
        CancellationToken cancellationToken)
    {
        var repositoryRoot = ResolveRepositoryRoot();
        var scenarioRoot = Path.GetDirectoryName(pluginRoot)
            ?? throw new InvalidOperationException("The plugin root must have a parent directory.");

        var feedDirectory = Path.Combine(scenarioRoot, "feed");
        var expectedVersion = Environment.GetEnvironmentVariable(
            "ROSLYN_WORKBENCH_MCP_ACCEPTANCE_EXPECTED_VERSION");

        var packageVersion = ResolveReleasePackage(repositoryRoot, feedDirectory, expectedVersion)
            ?? await PackLocalPackageAsync(repositoryRoot, feedDirectory, cancellationToken);

        var consumerDirectory = Path.Combine(scenarioRoot, "package-consumer");
        var outputDirectory = Path.Combine(scenarioRoot, "package-consumer-output");

        Directory.CreateDirectory(consumerDirectory);
        CopyConsumerAssets(repositoryRoot, consumerDirectory);
        var nuGetConfigurationPath = CreateNuGetConfiguration(
            consumerDirectory,
            feedDirectory);

        await RunDotNetAsync(
            consumerDirectory,
            [
                "restore",
                "ExternalPlugin.csproj",
                "--configfile",
                nuGetConfigurationPath,
                "--packages",
                Path.Combine(scenarioRoot, "packages"),
                "-p:NuGetAudit=false",
                $"-p:PluginPackageVersion={packageVersion}",
                .. CreateArtifactsArguments(scenarioRoot),
            ],
            cancellationToken);

        await RunDotNetAsync(
            consumerDirectory,
            [
                "build",
                "ExternalPlugin.csproj",
                "--no-restore",
                "--output",
                outputDirectory,
                $"-p:PluginPackageVersion={packageVersion}",
                .. CreateArtifactsArguments(scenarioRoot),
            ],
            cancellationToken);

        var packageDirectory = Path.Combine(pluginRoot, "package-built");
        Directory.CreateDirectory(packageDirectory);
        CopyOutput(outputDirectory, packageDirectory, "ExternalPlugin.dll");
        CopyOutput(outputDirectory, packageDirectory, "ExternalPlugin.deps.json");
    }

    public static string? ResolveReleasePackage(
        string repositoryRoot,
        string feedDirectory,
        string? expectedVersion)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            return null;
        }

        var releaseFeed = Path.Combine(repositoryRoot, "artifacts", "release", "package");
        var packagePath = Path.Combine(releaseFeed, $"{_packageId}.{expectedVersion}.nupkg");
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException(
                $"The expected plugin authoring package '{packagePath}' was not found.",
                packagePath);
        }

        Directory.CreateDirectory(feedDirectory);
        File.Copy(packagePath, Path.Combine(feedDirectory, Path.GetFileName(packagePath)));
        return expectedVersion;
    }

    private static async Task<string> PackLocalPackageAsync(
        string repositoryRoot,
        string feedDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(feedDirectory);
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "Roslyn.Workbench.Mcp.Plugins",
            "Roslyn.Workbench.Mcp.Plugins.csproj");

        var scenarioRoot = Path.GetDirectoryName(feedDirectory)
            ?? throw new InvalidOperationException("The feed directory must have a parent directory.");

        await RunDotNetAsync(
            repositoryRoot,
            [
                "restore",
                projectPath,
                "-p:NuGetAudit=false",
                .. CreateArtifactsArguments(scenarioRoot),
            ],
            cancellationToken);

        await RunDotNetAsync(
            repositoryRoot,
            [
                "pack",
                projectPath,
                "--configuration",
                "Release",
                "--no-restore",
                "--output",
                feedDirectory,
                $"-p:RoslynWorkbenchVersion={_localPackageVersion}",
                .. CreateArtifactsArguments(scenarioRoot),
            ],
            cancellationToken);

        return _localPackageVersion;
    }

    private static void CopyConsumerAssets(
        string repositoryRoot,
        string consumerDirectory)
    {
        var assetDirectory = Path.Combine(
            repositoryRoot,
            "test",
            "TestAssets",
            "PluginAnalyzerPackageConsumer");

        File.Copy(
            Path.Combine(assetDirectory, "ExternalPlugin.csproj"),
            Path.Combine(consumerDirectory, "ExternalPlugin.csproj"));
        File.Copy(
            Path.Combine(assetDirectory, "ValidPlugin.cs"),
            Path.Combine(consumerDirectory, "Plugin.cs"));
    }

    private static string CreateNuGetConfiguration(
        string consumerDirectory,
        string feedDirectory)
    {
        var packageSource = new XElement(
            "add",
            new XAttribute("key", "PackageUnderTest"),
            new XAttribute("value", feedDirectory));

        var nuGetSource = new XElement(
            "add",
            new XAttribute("key", "NuGetOrg"),
            new XAttribute("value", _nuGetSource),
            new XAttribute("protocolVersion", "3"));

        var clearSources = new XElement("clear");
        var packageSources = new XElement(
            "packageSources",
            clearSources,
            packageSource,
            nuGetSource);

        var configurationElement = new XElement("configuration", packageSources);
        var configuration = new XDocument(configurationElement);
        var configurationPath = Path.Combine(consumerDirectory, "NuGet.Config");

        configuration.Save(configurationPath);
        return configurationPath;
    }

    private static IReadOnlyList<string> CreateArtifactsArguments(string scenarioRoot)
    {
        if (!IsWsl())
        {
            return [];
        }

        return [$"--artifacts-path={Path.Combine(scenarioRoot, "artifacts")}"];
    }

    private static bool IsWsl()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/proc/version"))
        {
            return false;
        }

        var version = File.ReadAllText("/proc/version");
        return version.Contains("microsoft", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyOutput(
        string outputDirectory,
        string packageDirectory,
        string fileName)
    {
        File.Copy(
            Path.Combine(outputDirectory, fileName),
            Path.Combine(packageDirectory, fileName));
    }

    private static async Task RunDotNetAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.Environment.Remove("RoslynWorkbenchReleaseBuild");
        startInfo.Environment.Remove("RoslynWorkbenchVersion");
        startInfo.Environment.Remove("RoslynWorkbenchFullSemVer");
        startInfo.Environment.Remove("RoslynWorkbenchCommitSha");
        startInfo.Environment.Remove("RoslynWorkbenchVersionSourceDistance");
        startInfo.Environment.Remove("RoslynWorkbenchSourceTag");

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The dotnet process could not be started.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        var output = standardOutput + standardError;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}:{Environment.NewLine}{output}");
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var repositoryRoot = typeof(PackageBuiltPluginPreparation).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => string.Equals(
                attribute.Key,
                "RepositoryRoot",
                StringComparison.Ordinal))
            .Value;

        return repositoryRoot
            ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
