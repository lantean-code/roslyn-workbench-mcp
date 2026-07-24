using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;

namespace Roslyn.Workbench.Mcp.Test;

[Collection(PluginPackageIntegrationCollectionDefinition.Name)]
public sealed class PluginAnalyzerPackageIntegrationTests
{
    private const string _packageVersion = "0.0.0-analyzer-test";
    private const string _nuGetSource = "https://api.nuget.org/v3/index.json";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_PackedPluginsPackage_WHEN_InspectingAndBuildingConsumer_THEN_ShouldIncludeAndActivateAnalyzer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var scenarioRoot = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-plugin-package-tests",
            Guid.NewGuid().ToString("N"));

        var feedDirectory = Path.Combine(scenarioRoot, "feed");
        var consumerDirectory = Path.Combine(scenarioRoot, "consumer");
        Directory.CreateDirectory(feedDirectory);
        Directory.CreateDirectory(consumerDirectory);

        try
        {
            await PackProjectAsync(
                repositoryRoot,
                "Roslyn.Workbench.Mcp.Workspace",
                feedDirectory);

            await PackProjectAsync(
                repositoryRoot,
                "Roslyn.Workbench.Mcp.Plugins",
                feedDirectory);

            var packagePath = Path.Combine(
                feedDirectory,
                $"Roslyn.Workbench.Mcp.Plugins.{_packageVersion}.nupkg");

            ValidatePackageLayout(packagePath);

            var projectPath = await CreateConsumerProjectAsync(
                consumerDirectory,
                TestContext.Current.CancellationToken);

            await RestoreConsumerAsync(projectPath, feedDirectory);
            await ValidateAnalyzerActivationAsync(projectPath, consumerDirectory);
        }
        finally
        {
            if (Directory.Exists(scenarioRoot))
            {
                Directory.Delete(scenarioRoot, recursive: true);
            }
        }
    }

    private static async Task PackProjectAsync(
        string repositoryRoot,
        string projectName,
        string feedDirectory)
    {
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            projectName,
            $"{projectName}.csproj");

        var arguments = new List<string>
        {
            "pack",
            projectPath,
            "--configuration",
            "Release",
            "--no-restore",
            "--output",
            feedDirectory,
            $"-p:PackageVersion={_packageVersion}",
        };

        AddRepositoryArtifactsPath(arguments);
        var (exitCode, output) = await RunDotNetAsync(
            repositoryRoot,
            arguments,
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(
            0,
            $"packing {projectName} should succeed:{Environment.NewLine}{output}");
    }

    private static void ValidatePackageLayout(string packagePath)
    {
        File.Exists(packagePath).Should().BeTrue();
        using var archive = ZipFile.OpenRead(packagePath);

        const string analyzerPath =
            "analyzers/dotnet/cs/Roslyn.Workbench.Mcp.Plugins.Analyzers.dll";

        archive.Entries.Should().ContainSingle(
            static entry => string.Equals(
                entry.FullName,
                analyzerPath,
                StringComparison.Ordinal));

        archive.Entries.Should().ContainSingle(
            static entry => string.Equals(
                entry.FullName,
                "README.md",
                StringComparison.Ordinal));

        archive.Entries.Should().NotContain(
            static entry => entry.FullName.StartsWith(
                "lib/",
                StringComparison.Ordinal)
                && entry.Name.StartsWith(
                    "Microsoft.CodeAnalysis",
                    StringComparison.Ordinal));

        var nuspecEntries = archive.Entries
            .Where(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal))
            .ToArray();

        nuspecEntries.Should().ContainSingle();
        var nuspecEntry = nuspecEntries[0];

        using var nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        var dependencyIds = ReadDependencyIds(nuspec);

        dependencyIds.Should().Contain("Roslyn.Workbench.Mcp.Workspace");
        dependencyIds.Should().NotContain("Microsoft.CodeAnalysis.Analyzers");
        dependencyIds.Should().NotContain("Microsoft.CodeAnalysis.CSharp");
    }

    private static List<string> ReadDependencyIds(XDocument nuspec)
    {
        var dependencyIds = new List<string>();
        foreach (var element in nuspec.Descendants())
        {
            if (!string.Equals(element.Name.LocalName, "dependency", StringComparison.Ordinal))
            {
                continue;
            }

            var id = element.Attribute("id")?.Value;
            if (!string.IsNullOrWhiteSpace(id))
            {
                dependencyIds.Add(id);
            }
        }

        return dependencyIds;
    }

    private static async Task<string> CreateConsumerProjectAsync(
        string consumerDirectory,
        CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(consumerDirectory, "ExternalPlugin.csproj");
        var project = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Roslyn.Workbench.Mcp.Plugins" Version="{_packageVersion}" />
              </ItemGroup>
            </Project>
            """;

        await File.WriteAllTextAsync(projectPath, project, cancellationToken);

        var sourcePath = Path.Combine(consumerDirectory, "Plugin.cs");
        await File.WriteAllTextAsync(
            sourcePath,
            CreateInvalidPluginSource(),
            cancellationToken);

        return projectPath;
    }

    private static async Task RestoreConsumerAsync(
        string projectPath,
        string feedDirectory)
    {
        var consumerDirectory = Path.GetDirectoryName(projectPath);
        if (consumerDirectory is null)
        {
            throw new InvalidOperationException("The consumer project must have a parent directory.");
        }

        var arguments = new List<string>
        {
            "restore",
            projectPath,
            "--source",
            feedDirectory,
            "--source",
            _nuGetSource,
            "-p:NuGetAudit=false",
        };

        AddConsumerArtifactsPath(arguments, consumerDirectory);
        var (exitCode, output) = await RunDotNetAsync(
            consumerDirectory,
            arguments,
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(
            0,
            $"restoring the clean package consumer should succeed:{Environment.NewLine}{output}");
    }

    private static async Task ValidateAnalyzerActivationAsync(
        string projectPath,
        string consumerDirectory)
    {
        var invalidBuildArguments = new List<string>
        {
            "build",
            projectPath,
            "--no-restore",
        };

        AddConsumerArtifactsPath(invalidBuildArguments, consumerDirectory);
        var (invalidExitCode, invalidOutput) = await RunDotNetAsync(
            consumerDirectory,
            invalidBuildArguments,
            TestContext.Current.CancellationToken);

        invalidExitCode.Should().NotBe(0);
        invalidOutput.Should().Contain("RWMCP015");

        var sourcePath = Path.Combine(consumerDirectory, "Plugin.cs");
        await File.WriteAllTextAsync(
            sourcePath,
            CreateValidPluginSource(),
            TestContext.Current.CancellationToken);

        var validBuildArguments = new List<string>
        {
            "build",
            projectPath,
            "--no-restore",
        };

        AddConsumerArtifactsPath(validBuildArguments, consumerDirectory);
        var (validExitCode, validOutput) = await RunDotNetAsync(
            consumerDirectory,
            validBuildArguments,
            TestContext.Current.CancellationToken);

        validExitCode.Should().Be(
            0,
            $"the valid clean package consumer should build without diagnostics:{Environment.NewLine}{validOutput}");

        validOutput.Should().NotContain("RWMCP");
    }

    private static string CreateInvalidPluginSource()
    {
        return """
            using Roslyn.Workbench.Mcp.Plugins;

            public sealed class ExamplePlugin : IRoslynPlugin
            {
                public void Configure(IPluginConfiguration configuration)
                {
                }
            }
            """;
    }

    private static string CreateValidPluginSource()
    {
        return """
            using Roslyn.Workbench.Mcp.Plugins;
            using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

            [RoslynPlugin("example.tools", "Example Tools", PluginApiVersions.V1)]
            public sealed class ExamplePlugin : IRoslynPlugin
            {
                public void Configure(IPluginConfiguration configuration)
                {
                    _ = configuration.AddQueryTool<ExampleQueryTool>();
                }
            }

            public sealed record ExampleQueryRequest : WorkspaceBoundRequest
            {
                public string Value { get; init; } = string.Empty;
            }

            public sealed record ExampleQueryData
            {
                public string Value { get; init; } = string.Empty;
            }

            [RoslynTool(
                "example-query",
                "Example Query",
                "Returns an example response.")]
            internal sealed class ExampleQueryTool :
                IQueryToolHandler<ExampleQueryRequest, ExampleQueryData>
            {
                public ValueTask<PluginExecutionResult<ExampleQueryData>> ExecuteAsync(
                    ExampleQueryRequest request,
                    IQueryContext context,
                    CancellationToken cancellationToken)
                {
                    _ = context;
                    cancellationToken.ThrowIfCancellationRequested();

                    var data = new ExampleQueryData
                    {
                        Value = request.Value,
                    };

                    var executionResult = PluginExecutionResult<ExampleQueryData>.Success(data);
                    var result = ValueTask.FromResult(executionResult);
                    return result;
                }
            }
            """;
    }

    private static void AddRepositoryArtifactsPath(List<string> arguments)
    {
        if (!IsWsl())
        {
            return;
        }

        arguments.Add("--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp");
    }

    private static void AddConsumerArtifactsPath(
        List<string> arguments,
        string consumerDirectory)
    {
        if (!IsWsl())
        {
            return;
        }

        var artifactsPath = Path.Combine(consumerDirectory, "artifacts");
        arguments.Add($"--artifacts-path={artifactsPath}");
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

    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
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

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("The dotnet process could not be started.");
        }

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
        return (process.ExitCode, output);
    }

    private static string FindRepositoryRoot()
    {
        var assembly = typeof(PluginAnalyzerPackageIntegrationTests).Assembly;
        foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (!string.Equals(attribute.Key, "RepositoryRoot", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(attribute.Value))
            {
                var repositoryRoot = Path.GetFullPath(attribute.Value);
                return repositoryRoot;
            }
        }

        throw new InvalidOperationException("The RepositoryRoot assembly metadata was not configured.");
    }
}
