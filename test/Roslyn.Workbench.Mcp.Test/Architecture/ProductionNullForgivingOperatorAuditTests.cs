using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

public sealed class ProductionNullForgivingOperatorAuditTests
{
    [Fact]
    public void GIVEN_ProductionSource_WHEN_InspectingNullableSuppressionSyntax_THEN_ShouldContainNoNullForgivingOperators()
    {
        var findings = ProductionSourceAudit
            .EnumerateSourceFiles()
            .SelectMany(FindSuppressions)
            .OrderBy(static finding => finding, StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(string.Join(Environment.NewLine, findings));
    }

    private static IEnumerable<string> FindSuppressions(string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
        return syntaxTree
            .GetRoot()
            .DescendantNodes()
            .Where(static node => node.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            .Select(node =>
            {
                var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                return $"{Path.GetRelativePath(ProductionSourceAudit.RepositoryRoot, path)}:{line}: {node}";
            });
    }
}
