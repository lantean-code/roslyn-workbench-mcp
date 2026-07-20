using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Resolution;

public sealed class CompilationTypeSymbolCacheTests
{
    [Fact]
    public void GIVEN_DistinctCompilations_WHEN_GettingTypes_THEN_ShouldKeepSymbolsIsolatedByCompilation()
    {
        var firstCompilation = CSharpCompilation.Create(
            "FirstCompilation",
            [CSharpSyntaxTree.ParseText("namespace Framework { class Type {} }", cancellationToken: TestContext.Current.CancellationToken)]);

        var secondCompilation = CSharpCompilation.Create(
            "SecondCompilation",
            [CSharpSyntaxTree.ParseText("namespace Framework { class Type {} }", cancellationToken: TestContext.Current.CancellationToken)]);

        var target = new CompilationTypeSymbolCache();
        var firstType = target.GetTypeByMetadataName(firstCompilation, "Framework.Type");
        var cachedFirstType = target.GetTypeByMetadataName(firstCompilation, "Framework.Type");
        var secondType = target.GetTypeByMetadataName(secondCompilation, "Framework.Type");
        var missingType = target.GetTypeByMetadataName(firstCompilation, "Missing.Type");
        var cachedMissingType = target.GetTypeByMetadataName(firstCompilation, "Missing.Type");

        firstType.Should().NotBeNull();
        cachedFirstType.Should().BeSameAs(firstType);
        secondType.Should().NotBeNull();
        secondType.Should().NotBeSameAs(firstType);
        missingType.Should().BeNull();
        cachedMissingType.Should().BeNull();
    }
}
