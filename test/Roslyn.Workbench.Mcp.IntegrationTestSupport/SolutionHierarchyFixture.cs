namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class SolutionHierarchyFixture : IAsyncDisposable
{
    private readonly string _directoryPath;

    private SolutionHierarchyFixture(string directoryPath, string solutionPath)
    {
        _directoryPath = directoryPath;
        SolutionPath = solutionPath;
    }

    public string SolutionPath { get; }

    public static async Task<SolutionHierarchyFixture> CreateAsync()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-solution-tests", Guid.NewGuid().ToString("n"));
        var solutionPath = Path.Combine(directoryPath, "Sample.slnx");
        var libraryDirectoryPath = Path.Combine(directoryPath, "Lib");
        var libraryProjectPath = Path.Combine(libraryDirectoryPath, "Lib.csproj");
        var libraryDocumentPath = Path.Combine(libraryDirectoryPath, "MessageFormatter.cs");
        var applicationDirectoryPath = Path.Combine(directoryPath, "App");
        var applicationProjectPath = Path.Combine(applicationDirectoryPath, "App.csproj");
        var applicationDocumentPath = Path.Combine(applicationDirectoryPath, "AppFormatter.cs");

        Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(Path.Combine(directoryPath, ".git"));
        Directory.CreateDirectory(libraryDirectoryPath);
        Directory.CreateDirectory(applicationDirectoryPath);

        await File.WriteAllTextAsync(solutionPath, """
            <Solution>
              <Folder Name="/src/" />
              <Folder Name="/src/core/">
                <Project Path="Lib/Lib.csproj" />
              </Folder>
              <Folder Name="/src/apps/">
                <Project Path="App/App.csproj" />
              </Folder>
            </Solution>
            """);
        await File.WriteAllTextAsync(libraryProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(libraryDocumentPath, """
            namespace Sample;

            public interface IMessageFormatter
            {
                string Format(string value);
            }
            """);
        await File.WriteAllTextAsync(applicationProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Lib\Lib.csproj" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(applicationDocumentPath, """
            namespace Sample;

            public sealed class AppFormatter : IMessageFormatter
            {
                public string Format(string value)
                {
                    return value.Trim();
                }
            }

            public static class AppCaller
            {
                public static string Call()
                {
                    return new AppFormatter().Format("value");
                }
            }
            """);

        return new SolutionHierarchyFixture(directoryPath, solutionPath);
    }

    public ValueTask DisposeAsync()
    {
        return TemporaryDirectory.Attach(_directoryPath).DisposeAsync();
    }
}
