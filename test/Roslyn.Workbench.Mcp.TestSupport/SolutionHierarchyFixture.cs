namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class SolutionHierarchyFixture : IDisposable
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
        var projectDirectoryPath = Path.Combine(directoryPath, "Lib");
        var projectPath = Path.Combine(projectDirectoryPath, "Lib.csproj");
        var documentPath = Path.Combine(projectDirectoryPath, "Class1.cs");

        Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(projectDirectoryPath);

        await File.WriteAllTextAsync(solutionPath, """
            <Solution>
              <Folder Name="/src/" />
              <Folder Name="/src/core/">
                <Project Path="Lib/Lib.csproj" />
              </Folder>
            </Solution>
            """);
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

        return new SolutionHierarchyFixture(directoryPath, solutionPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }
}
