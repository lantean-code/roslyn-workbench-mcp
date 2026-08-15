namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceProjectCompatibilityInspectorIntegrationTests
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

            var target = new WorkspaceProjectCompatibilityInspector();

            var result = target.Inspect(projectPath, null);

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

            var target = new WorkspaceProjectCompatibilityInspector();

            var result = target.Inspect(projectPath, null);

            result.IsSdkStyle.Should().BeFalse();
            result.Diagnostics.Should().BeEmpty();
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    public void GIVEN_MalformedProject_WHEN_InspectingCompatibility_THEN_ShouldReturnLoadDiagnostic()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Malformed.csproj");
            File.WriteAllText(projectPath, "<Project>");
            var target = new WorkspaceProjectCompatibilityInspector();

            var result = target.Inspect(projectPath, null);

            result.IsSdkStyle.Should().BeFalse();
            result.Diagnostics.Should().ContainSingle().Which.Should().Match<DiagnosticInfo>(diagnostic =>
                diagnostic.Id == "WorkspaceLoad"
                && diagnostic.Severity == Results.DiagnosticSeverity.Error
                && !string.IsNullOrWhiteSpace(diagnostic.Message));
        }
        finally
        {
            DeleteDirectory(directoryPath);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_ConfigurationDisablesMissingImport_WHEN_InspectingCompatibility_THEN_ShouldUseConfiguredProperties()
    {
        MsBuildTestRegistration.EnsureRegistered();
        var directoryPath = CreateDirectoryPath();

        try
        {
            var projectPath = Path.Combine(directoryPath, "Conditional.csproj");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="Missing.props" Condition="'$(Configuration)' != 'Release'" />
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var properties = new WorkspaceMsBuildProperties
            {
                Configuration = "Release",
            };

            var target = new WorkspaceProjectCompatibilityInspector();

            var result = target.Inspect(projectPath, properties);

            result.IsSdkStyle.Should().BeTrue();
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
