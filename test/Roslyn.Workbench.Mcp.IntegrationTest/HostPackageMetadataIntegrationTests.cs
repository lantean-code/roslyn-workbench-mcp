using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

namespace Roslyn.Workbench.Mcp.Test;

[Trait("Category", "Integration")]
public sealed class HostPackageMetadataIntegrationTests
{
    private const string _packageId = "Lantean.Roslyn.Workbench.Mcp";
    private const string _packageVersion = "1.2.3-beta.4";
    private const string _serverName = "io.github.lantean-code/roslyn-workbench-mcp";

    [Fact]
    public async Task GIVEN_PackedHost_WHEN_InspectingPackageMetadata_THEN_ShouldSupportDotnetToolAndMcpServerAcquisition()
    {
        var repositoryRoot = GetRepositoryRoot();
        var packageDirectory = Path.Combine(
            Path.GetTempPath(),
            "roslyn-workbench-mcp-host-package-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(packageDirectory);

        try
        {
            var output = await PackHostAsync(repositoryRoot, packageDirectory);

            output.ExitCode.Should().Be(
                0,
                $"packing the Host should succeed:{Environment.NewLine}{output.Output}");

            var packagePath = Path.Combine(packageDirectory, $"{_packageId}.{_packageVersion}.nupkg");
            ValidatePackage(packagePath);
        }
        finally
        {
            Directory.Delete(packageDirectory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> PackHostAsync(string repositoryRoot, string packageDirectory)
    {
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "Roslyn.Workbench.Mcp",
            "Roslyn.Workbench.Mcp.csproj");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot,
        };

        startInfo.ArgumentList.Add("pack");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(packageDirectory);
        startInfo.ArgumentList.Add($"-p:RoslynWorkbenchVersion={_packageVersion}");
        if (OperatingSystem.IsLinux() && File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop"))
        {
            startInfo.ArgumentList.Add("--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp");
        }

        RemoveReleaseEnvironment(startInfo);

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        process.Start().Should().BeTrue();
        var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        var output = await standardOutput + await standardError;

        return (process.ExitCode, output);
    }

    private static void ValidatePackage(string packagePath)
    {
        File.Exists(packagePath).Should().BeTrue();
        using var archive = ZipFile.OpenRead(packagePath);

        var nuspecEntry = archive.Entries.Single(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var nuspecStream = nuspecEntry.Open();
        var nuspec = XDocument.Load(nuspecStream);
        var packageTypes = nuspec
            .Descendants()
            .Where(static element => element.Name.LocalName == "packageType")
            .Select(static element => element.Attribute("name")?.Value)
            .ToArray();

        var nuspecPackageId = nuspec.Descendants().Single(static element => element.Name.LocalName == "id").Value;
        var nuspecVersion = nuspec.Descendants().Single(static element => element.Name.LocalName == "version").Value;

        packageTypes.Should().BeEquivalentTo("DotnetTool", "McpServer");
        nuspecPackageId.Should().Be(_packageId);
        nuspecVersion.Should().Be(_packageVersion);

        var readmePath = nuspec.Descendants().Single(static element => element.Name.LocalName == "readme").Value;

        ValidateOwnershipMarker(archive, readmePath);
        ValidateRegistryMetadata(archive);
    }

    private static void ValidateOwnershipMarker(ZipArchive archive, string readmePath)
    {
        var readmeEntry = archive.Entries.Single(entry => entry.FullName == readmePath);
        using var readmeStream = readmeEntry.Open();
        using var readmeReader = new StreamReader(readmeStream);
        var readme = readmeReader.ReadToEnd();

        readme.Should().Contain($"<!-- mcp-name: {_serverName} -->");
    }

    private static void ValidateRegistryMetadata(ZipArchive archive)
    {
        var manifestEntry = archive.Entries.Single(static entry => entry.FullName == ".mcp/server.json");
        using var manifestStream = manifestEntry.Open();
        using var manifestReader = new StreamReader(manifestStream);
        var manifestJson = manifestReader.ReadToEnd();

        manifestJson.Should().NotContain("{{PACKAGE_VERSION}}");

        using var manifest = JsonDocument.Parse(manifestJson);
        var root = manifest.RootElement;
        var package = root.GetProperty("packages").EnumerateArray().Single();

        root.GetProperty("name").GetString().Should().Be(_serverName);
        root.GetProperty("version").GetString().Should().Be(_packageVersion);
        root.GetProperty("$schema").GetString().Should().Be("https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json");
        root.GetProperty("websiteUrl").GetString().Should().Be($"https://lantean-code.github.io/roslyn-workbench-mcp/{_packageVersion}/");
        package.GetProperty("registryType").GetString().Should().Be("nuget");
        package.GetProperty("registryBaseUrl").GetString().Should().Be("https://api.nuget.org/v3/index.json");
        package.GetProperty("identifier").GetString().Should().Be(_packageId);
        package.GetProperty("version").GetString().Should().Be(_packageVersion);
        package.GetProperty("runtimeHint").GetString().Should().Be("dnx");
        package.GetProperty("transport").GetProperty("type").GetString().Should().Be("stdio");
        package.GetProperty("packageArguments").GetArrayLength().Should().Be(0);

        ValidateRegistryIcon(root);
    }

    private static void ValidateRegistryIcon(JsonElement root)
    {
        var icon = root.GetProperty("icons").EnumerateArray().Single();

        icon.GetProperty("src").GetString().Should().Be($"https://raw.githubusercontent.com/lantean-code/roslyn-workbench-mcp/{_packageVersion}/assets/icons/roslyn-workbench-mcp-128.png");
        icon.GetProperty("mimeType").GetString().Should().Be("image/png");
        icon.GetProperty("sizes").EnumerateArray().Select(static size => size.GetString()).Should().Equal("128x128");
    }

    private static void RemoveReleaseEnvironment(ProcessStartInfo startInfo)
    {
        startInfo.Environment.Remove("RoslynWorkbenchReleaseBuild");
        startInfo.Environment.Remove("RoslynWorkbenchVersion");
        startInfo.Environment.Remove("RoslynWorkbenchFullSemVer");
        startInfo.Environment.Remove("RoslynWorkbenchCommitSha");
        startInfo.Environment.Remove("RoslynWorkbenchVersionSourceDistance");
        startInfo.Environment.Remove("RoslynWorkbenchSourceTag");
    }

    private static string GetRepositoryRoot()
    {
        var repositoryRoot = typeof(HostPackageMetadataIntegrationTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value;

        return repositoryRoot ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
