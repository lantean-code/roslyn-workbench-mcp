namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class TestWorkspaceFixture : IAsyncDisposable
{
    private readonly string _directoryPath;

    private TestWorkspaceFixture(
        string directoryPath,
        string projectPath,
        string documentPath,
        string directoryBuildPropsPath,
        string editorConfigPath,
        string? sharedDocumentPath = null)
    {
        _directoryPath = directoryPath;
        ProjectPath = projectPath;
        DocumentPath = documentPath;
        DirectoryBuildPropsPath = directoryBuildPropsPath;
        EditorConfigPath = editorConfigPath;
        SharedDocumentPath = sharedDocumentPath;
    }

    public string DocumentPath { get; }

    public string DirectoryBuildPropsPath { get; }

    public string EditorConfigPath { get; }

    public string ProjectPath { get; }

    public string? SharedDocumentPath { get; }

    public static async Task<TestWorkspaceFixture> CreateAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(Path.Combine(directoryPath, ".git"));

        var projectPath = Path.Combine(directoryPath, "Sample.csproj");
        var documentPath = Path.Combine(directoryPath, "Class1.cs");
        var directoryBuildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
        var editorConfigPath = Path.Combine(directoryPath, ".editorconfig");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(documentPath, """
            namespace Sample;

            public sealed class Class1
            {
            }
            """);
        await File.WriteAllTextAsync(directoryBuildPropsPath, """
            <Project>
              <PropertyGroup>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(editorConfigPath, """
            root = true

            [*.cs]
            dotnet_diagnostic.IDE0005.severity = warning
            """);

        return new TestWorkspaceFixture(directoryPath, projectPath, documentPath, directoryBuildPropsPath, editorConfigPath);
    }

    public static async Task<TestWorkspaceFixture> CreateLegacyProjectAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(Path.Combine(directoryPath, ".git"));

        var projectPath = Path.Combine(directoryPath, "Legacy.csproj");
        var documentPath = Path.Combine(directoryPath, "Class1.cs");
        var directoryBuildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
        var editorConfigPath = Path.Combine(directoryPath, ".editorconfig");

        await File.WriteAllTextAsync(projectPath, """
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Class1.cs" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(documentPath, """
            public class Class1
            {
            }
            """);
        await File.WriteAllTextAsync(directoryBuildPropsPath, "<Project />");
        await File.WriteAllTextAsync(editorConfigPath, "root = true");

        return new TestWorkspaceFixture(directoryPath, projectPath, documentPath, directoryBuildPropsPath, editorConfigPath);
    }

    public static async Task<TestWorkspaceFixture> CreateMalformedProjectAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(Path.Combine(directoryPath, ".git"));

        var projectPath = Path.Combine(directoryPath, "Broken.csproj");
        var documentPath = Path.Combine(directoryPath, "Class1.cs");
        var directoryBuildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
        var editorConfigPath = Path.Combine(directoryPath, ".editorconfig");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
            """);
        await File.WriteAllTextAsync(documentPath, """
            namespace Sample;

            public sealed class Class1
            {
            }
            """);
        await File.WriteAllTextAsync(directoryBuildPropsPath, "<Project />");
        await File.WriteAllTextAsync(editorConfigPath, "root = true");

        return new TestWorkspaceFixture(directoryPath, projectPath, documentPath, directoryBuildPropsPath, editorConfigPath);
    }

    public static async Task<TestWorkspaceFixture> CreateAmbiguousAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-tests", Guid.NewGuid().ToString("n"));
        var projectOneDirectoryPath = Path.Combine(directoryPath, "ProjectOne");
        var projectTwoDirectoryPath = Path.Combine(directoryPath, "ProjectTwo");
        var sharedDirectoryPath = Path.Combine(directoryPath, "Shared");
        Directory.CreateDirectory(projectOneDirectoryPath);
        Directory.CreateDirectory(projectTwoDirectoryPath);
        Directory.CreateDirectory(sharedDirectoryPath);

        var projectPath = Path.Combine(projectOneDirectoryPath, "Sample.csproj");
        var projectTwoPath = Path.Combine(projectTwoDirectoryPath, "Sample.csproj");
        var documentPath = Path.Combine(projectOneDirectoryPath, "Class1.cs");
        var projectTwoDocumentPath = Path.Combine(projectTwoDirectoryPath, "Class1.cs");
        var sharedDocumentPath = Path.Combine(sharedDirectoryPath, "SharedClass.cs");
        var directoryBuildPropsPath = Path.Combine(directoryPath, "Directory.Build.props");
        var editorConfigPath = Path.Combine(directoryPath, ".editorconfig");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\ProjectTwo\Sample.csproj" />
                <Compile Include="..\Shared\SharedClass.cs" Link="SharedClass.cs" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(projectTwoPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="..\Shared\SharedClass.cs" Link="SharedClass.cs" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(documentPath, """
            namespace Sample.ProjectOne;

            public sealed class Class1
            {
            }
            """);
        await File.WriteAllTextAsync(projectTwoDocumentPath, """
            namespace Sample.ProjectTwo;

            public sealed class Class1
            {
            }
            """);
        await File.WriteAllTextAsync(sharedDocumentPath, """
            namespace Sample.Shared;

            public sealed class SharedClass
            {
            }
            """);
        await File.WriteAllTextAsync(directoryBuildPropsPath, """
            <Project>
              <PropertyGroup>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(editorConfigPath, """
            root = true

            [*.cs]
            dotnet_diagnostic.IDE0005.severity = warning
            """);

        return new TestWorkspaceFixture(directoryPath, projectPath, documentPath, directoryBuildPropsPath, editorConfigPath, sharedDocumentPath);
    }

    public IWorkspaceRuntime CreateCoordinator()
    {
        return WorkspaceCoordinatorFactory.Create();
    }

    public ValueTask DisposeAsync()
    {
        return TemporaryDirectory.Attach(_directoryPath).DisposeAsync();
    }
}
