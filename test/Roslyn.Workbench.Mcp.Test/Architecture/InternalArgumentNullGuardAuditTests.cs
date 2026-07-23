using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

public sealed class InternalArgumentNullGuardAuditTests
{
    [Fact]
    public void GIVEN_InternalProductionTypes_WHEN_InspectingArgumentNullGuards_THEN_ShouldContainNoRedundantGuards()
    {
        var findings = ProductionSourceAudit
            .EnumerateSourceFiles()
            .SelectMany(FindArgumentNullGuards)
            .OrderBy(static finding => finding, StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(string.Join(Environment.NewLine, findings));
    }

    private static IEnumerable<string> FindArgumentNullGuards(string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
        var guards = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .Where(IsArgumentNullGuard)
            .Where(IsInsideInternalType);

        foreach (var guard in guards)
        {
            var line = guard.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"{Path.GetRelativePath(ProductionSourceAudit.RepositoryRoot, path)}:{line}: {guard}";
        }
    }

    private static bool IsArgumentNullGuard(SyntaxNode node)
    {
        if (node is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "ArgumentNullException" },
                    Name.Identifier.ValueText: "ThrowIfNull",
                },
            })
        {
            return true;
        }

        return node is ObjectCreationExpressionSyntax objectCreation
            && objectCreation.Type.ToString().EndsWith("ArgumentNullException", StringComparison.Ordinal);
    }

    private static bool IsInsideInternalType(SyntaxNode node)
    {
        var outermostType = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().LastOrDefault();
        return outermostType?.Modifiers.Any(SyntaxKind.InternalKeyword) == true;
    }
}
