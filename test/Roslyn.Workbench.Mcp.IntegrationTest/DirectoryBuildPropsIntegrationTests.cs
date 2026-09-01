using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test;

[Trait("Category", "Integration")]
public sealed class DirectoryBuildPropsIntegrationTests
{
    private const string PublicVersion = "0.1.0-alpha.1";
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static TheoryData<string, string> IndependentIdentityOverrides => new()
    {
        { "Version=9.9.9", "Version is derived from RoslynWorkbenchVersion" },
        { "Version=0.1.0-ALPHA.1", "Version is derived from RoslynWorkbenchVersion" },
        { "PackageVersion=9.9.9", "PackageVersion is derived from RoslynWorkbenchVersion" },
        { "PackageVersion=0.1.0-ALPHA.1", "PackageVersion is derived from RoslynWorkbenchVersion" },
        { "InformationalVersion=9.9.9", "InformationalVersion is derived from RoslynWorkbenchVersion" },
        { "InformationalVersion=0.1.0-ALPHA.1", "InformationalVersion is derived from RoslynWorkbenchVersion" },
        { "RoslynWorkbenchAssemblyVersion=9.9.9.0", "AssemblyVersion is derived from RoslynWorkbenchVersion" },
        { "AssemblyVersion=9.9.9.0", "AssemblyVersion is derived from RoslynWorkbenchVersion" },
        { "FileVersion=9.9.9.0", "FileVersion is derived from RoslynWorkbenchVersion" },
        { "RepositoryCommit=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "RepositoryCommit is derived from RoslynWorkbenchCommitSha" },
        { "SourceRevisionId=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "SourceRevisionId is derived from RoslynWorkbenchCommitSha" },
    };

    public static TheoryData<string, string> InvalidReleaseProvenance => new()
    {
        { "RoslynWorkbenchFullSemVer=not-a-semver", "RoslynWorkbenchFullSemVer must be a valid semantic version" },
        { "RoslynWorkbenchFullSemVer=0.1.1-alpha.1", "RoslynWorkbenchFullSemVer must equal RoslynWorkbenchVersion" },
        { "RoslynWorkbenchFullSemVer=0.1.0-ALPHA.1", "RoslynWorkbenchFullSemVer must equal RoslynWorkbenchVersion" },
        { "RoslynWorkbenchCommitSha=x", "RoslynWorkbenchCommitSha must be a full lowercase 40-character Git commit SHA" },
        { "RoslynWorkbenchSourceTag=0.1.0-ALPHA.1", "RoslynWorkbenchSourceTag must exactly match RoslynWorkbenchVersion" },
        { "RoslynWorkbenchVersionSourceDistance=banana", "RoslynWorkbenchVersionSourceDistance must be a non-negative integer" },
        { "RoslynWorkbenchVersionSourceDistance=-1", "RoslynWorkbenchVersionSourceDistance must be a non-negative integer" },
        { "RepositoryUrl=https://example.invalid", "RepositoryUrl must identify the approved Roslyn Workbench MCP repository" },
        { "RepositoryType=svn", "RepositoryType must be git" },
        { "PublishRepositoryUrl=false", "PublishRepositoryUrl must remain enabled" },
        { "EnableSourceLink=false", "EnableSourceLink must remain enabled" },
        { "DebugType=embedded", "DebugType must remain portable" },
        { "Deterministic=false", "Deterministic must remain enabled" },
        { "ContinuousIntegrationBuild=false", "ContinuousIntegrationBuild must remain enabled" },
        { "IncludeSourceRevisionInInformationalVersion=true", "IncludeSourceRevisionInInformationalVersion must remain disabled" },
        { "EmbedUntrackedSources=false", "EmbedUntrackedSources must remain enabled" },
    };

    [Fact]
    public async Task GIVEN_ValidReleaseIdentity_WHEN_ValidatingBuildProperties_THEN_ShouldSucceed()
    {
        var result = await ValidateReleaseIdentityAsync([]);

        result.ExitCode.Should().Be(0, result.Output);
    }

    [Theory]
    [MemberData(nameof(IndependentIdentityOverrides))]
    public async Task GIVEN_IndependentIdentityOverride_WHEN_ValidatingBuildProperties_THEN_ShouldRejectOverride(string property, string expectedMessage)
    {
        var result = await ValidateReleaseIdentityAsync([property]);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain(expectedMessage);
    }

    [Theory]
    [MemberData(nameof(InvalidReleaseProvenance))]
    public async Task GIVEN_InvalidReleaseProvenance_WHEN_ValidatingBuildProperties_THEN_ShouldRejectValue(string property, string expectedMessage)
    {
        var result = await ValidateReleaseIdentityAsync([property]);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain(expectedMessage);
    }

    [Fact]
    public async Task GIVEN_FullSemVerIdentityAndDerivedPropertyOverrides_WHEN_ValidatingBuildProperties_THEN_ShouldRejectValue()
    {
        var result = await ValidateReleaseIdentityAsync(
        [
            "RoslynWorkbenchFullSemVer=0.1.0-ALPHA.1",
            "_RoslynWorkbenchFullSemVerPublicIdentity=0.1.0-alpha.1",
        ]);

        result.ExitCode.Should().NotBe(0);
        result.Output.Should().Contain("RoslynWorkbenchFullSemVer must equal RoslynWorkbenchVersion");
    }

    private static async Task<(int ExitCode, string Output)> ValidateReleaseIdentityAsync(string[] propertyOverrides)
    {
        var repositoryRoot = GetRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "Roslyn.Workbench.Mcp.Abstractions",
            "Roslyn.Workbench.Mcp.Abstractions.csproj");
        var artifactsPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp", "release-identity-tests", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot,
        };

        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--disable-build-servers");
        startInfo.ArgumentList.Add($"--artifacts-path={artifactsPath}");
        startInfo.ArgumentList.Add("-t:ValidateRoslynWorkbenchIdentity");
        startInfo.ArgumentList.Add("-p:RoslynWorkbenchReleaseBuild=true");
        startInfo.ArgumentList.Add($"-p:RoslynWorkbenchVersion={PublicVersion}");
        startInfo.ArgumentList.Add($"-p:RoslynWorkbenchFullSemVer={PublicVersion}+17");
        startInfo.ArgumentList.Add($"-p:RoslynWorkbenchCommitSha={CommitSha}");
        startInfo.ArgumentList.Add("-p:RoslynWorkbenchVersionSourceDistance=17");
        startInfo.ArgumentList.Add($"-p:RoslynWorkbenchSourceTag={PublicVersion}");

        foreach (var propertyOverride in propertyOverrides)
        {
            startInfo.ArgumentList.Add($"-p:{propertyOverride}");
        }

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

    private static string GetRepositoryRoot()
    {
        var repositoryRoot = typeof(DirectoryBuildPropsIntegrationTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == "RepositoryRoot")
            .Value;

        return repositoryRoot ?? throw new InvalidOperationException("RepositoryRoot assembly metadata was not configured.");
    }
}
