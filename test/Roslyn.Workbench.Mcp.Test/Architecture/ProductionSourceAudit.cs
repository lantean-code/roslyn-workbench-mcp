using System.Reflection;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

internal static class ProductionSourceAudit
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static IEnumerable<string> EnumerateSourceFiles()
    {
        var sourceRoot = Path.Combine(RepositoryRoot, "src");
        return Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"));
    }

    private static bool ContainsDirectory(string path, string directoryName)
    {
        var segment = $"{Path.DirectorySeparatorChar}{directoryName}{Path.DirectorySeparatorChar}";
        return path.Contains(segment, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = typeof(ProductionSourceAudit).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(static attribute => string.Equals(attribute.Key, "RepositoryRoot", StringComparison.Ordinal))
            ?.Value;
        var repositoryRoot = FindRepositoryRootFrom(configuredRoot)
            ?? FindRepositoryRootFrom(Directory.GetCurrentDirectory())
            ?? FindRepositoryRootFrom(AppContext.BaseDirectory);
        return repositoryRoot
            ?? throw new InvalidOperationException("The repository root could not be found.");
    }

    private static string? FindRepositoryRootFrom(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var directory = new DirectoryInfo(path);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Roslyn.Workbench.Mcp.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
