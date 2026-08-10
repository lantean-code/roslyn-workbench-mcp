using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Test.Architecture;

public sealed class PluginExecutionReflectionAuditTests
{
    private static readonly HashSet<string> _forbiddenMethods = new(StringComparer.Ordinal)
    {
        "CreateInstance",
        "DynamicInvoke",
        "Invoke",
        "MakeGenericMethod",
        "MakeGenericType",
    };

    [Fact]
    public void GIVEN_PluginRequestExecutionSources_WHEN_InspectingInvocations_THEN_ShouldNotUseReflectionDispatch()
    {
        var findings = EnumeratePluginExecutionFiles()
            .SelectMany(FindForbiddenInvocations)
            .OrderBy(static finding => finding, StringComparer.Ordinal)
            .ToArray();

        findings.Should().BeEmpty(string.Join(Environment.NewLine, findings));
    }

    private static IEnumerable<string> EnumeratePluginExecutionFiles()
    {
        var hostRoot = Path.Combine(
            ProductionSourceAudit.RepositoryRoot,
            "src",
            "Roslyn.Workbench.Mcp");
        var adapterRoot = Path.Combine(hostRoot, "ToolExecution", "Plugins");

        return Directory.EnumerateFiles(adapterRoot, "*.cs", SearchOption.AllDirectories)
            .Append(Path.Combine(hostRoot, "Hosting", "PluginMcpRequestHandler.cs"));
    }

    private static IEnumerable<string> FindForbiddenInvocations(string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
        var invocations = syntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var methodName = GetMethodName(invocation.Expression);
            if (methodName is null || !_forbiddenMethods.Contains(methodName))
            {
                continue;
            }

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"{Path.GetRelativePath(ProductionSourceAudit.RepositoryRoot, path)}:{line}: {invocation}";
        }
    }

    private static string? GetMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
    }
}
