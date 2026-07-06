using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class DefaultInspectionContextServiceTests
{
    [Fact]
    public async Task GIVEN_NullDocument_WHEN_ReadingContext_THEN_ShouldReturnNull()
    {
        var target = new DefaultInspectionContextService();

        var result = await target.ReadContextAsync(null, new TextSpan(0, 0), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_DocumentAndSpan_WHEN_ReadingContext_THEN_ShouldReturnTrimmedLine()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
                        return value.Trim();
                }
            }
            """);
        var target = new DefaultInspectionContextService();
        var document = workspace.Solution.Projects.Single().Documents.Single();

        var result = await target.ReadContextAsync(document, new TextSpan(workspace.GetLocationSelector("return value.Trim();").Span!.Start, 1), TestContext.Current.CancellationToken);

        result.Should().Be("return value.Trim();");
    }

    [Fact]
    public async Task GIVEN_PositionWithoutResolvableSymbol_WHEN_TryingToCreateContainingSymbol_THEN_ShouldReturnNull()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp(string.Empty);
        var target = new DefaultInspectionContextService();
        var document = workspace.Solution.Projects.Single().Documents.Single();

        var result = await target.TryCreateContainingSymbolAsync(document, 0, workspace.Solution, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GIVEN_SymbolAtPosition_WHEN_TryingToCreateContainingSymbol_THEN_ShouldReturnResolvedSymbol()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
                    return value.Trim();
                }
            }
            """);
        var target = new DefaultInspectionContextService();
        var document = workspace.Solution.Projects.Single().Documents.Single();
        var selector = workspace.GetLocationSelector("Trim");

        var result = await target.TryCreateContainingSymbolAsync(document, selector.Span!.Start, workspace.Solution, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Trim");
    }
}
