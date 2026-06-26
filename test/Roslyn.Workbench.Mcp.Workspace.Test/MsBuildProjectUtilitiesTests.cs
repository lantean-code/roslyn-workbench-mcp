using AwesomeAssertions;

using Roslyn.Workbench.Mcp.TestSupport;
using Roslyn.Workbench.Mcp.Workspace;

using Xunit;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class MsBuildProjectUtilitiesTests
{
    [Fact]
    public void GIVEN_ProjectWithTopLevelSdkElement_WHEN_InspectingCompatibility_THEN_ShouldReportSdkStyle()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Sample.csproj");
            File.WriteAllText(projectPath, """
                <Project>
                  <Sdk Name="Microsoft.NET.Sdk" />
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var result = MsBuildProjectUtilities.InspectCompatibility(projectPath);

            result.IsSdkStyle.Should().BeTrue();
            result.Diagnostics.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_LegacyProject_WHEN_InspectingCompatibility_THEN_ShouldReportNonSdkStyle()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Legacy.csproj");
            File.WriteAllText(projectPath, """
                <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
                  </PropertyGroup>
                </Project>
                """);

            var result = MsBuildProjectUtilities.InspectCompatibility(projectPath);

            result.IsSdkStyle.Should().BeFalse();
            result.Diagnostics.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    private static string CreateDirectoryPath()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-msbuild-project-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
