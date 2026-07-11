using Microsoft.CodeAnalysis.CSharp;

using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class ProductionNullForgivingOperatorAuditTests
{
    [Fact]
    [Trait("Category", "Audit")]
    public void GIVEN_ProductionSource_WHEN_InspectingNullableSuppressionSyntax_THEN_ShouldContainNoNullForgivingOperators()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var findings = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"))
            .SelectMany(path => FindSuppressions(repositoryRoot, path))
            .OrderBy(static finding => finding, StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(string.Join(Environment.NewLine, findings));
    }

    private static bool ContainsDirectory(string path, string directoryName)
    {
        var segment = $"{Path.DirectorySeparatorChar}{directoryName}{Path.DirectorySeparatorChar}";
        return path.Contains(segment, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = typeof(ProductionNullForgivingOperatorAuditTests).Assembly
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

    private static IEnumerable<string> FindSuppressions(string repositoryRoot, string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
        return syntaxTree
            .GetRoot()
            .DescendantNodes()
            .Where(static node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            .Select(node =>
            {
                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                return $"{Path.GetRelativePath(repositoryRoot, path)}:{line}: {node}";
            });
    }
}
