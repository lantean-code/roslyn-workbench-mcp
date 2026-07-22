using Roslyn.Workbench.Mcp.Plugins.Core.Inspection.Caching;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection.Caching;

public sealed class ReferenceDiscoveryCacheKeyTests
{
    [Fact]
    public async Task GIVEN_EquivalentInputs_WHEN_ComparingKeys_THEN_ShouldBeEqual()
    {
        using var solution = CreateSolution();
        var document = solution.GetDocument("Code.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            document,
            "Current",
            TestContext.Current.CancellationToken);
        var first = new ReferenceDiscoveryCacheKey(solution.Solution, symbol, [document]);
        var second = new ReferenceDiscoveryCacheKey(solution.Solution, symbol, [document]);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public async Task GIVEN_DifferentSolutionSnapshot_WHEN_ComparingKeys_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var document = solution.GetDocument("Code.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            document,
            "Current",
            TestContext.Current.CancellationToken);
        var first = new ReferenceDiscoveryCacheKey(solution.Solution, symbol, [document]);
        var changedSolution = solution.Solution.AddDocument(DocumentId.CreateNewId(document.Project.Id), "Other.cs", "class Other { }");
        var second = new ReferenceDiscoveryCacheKey(changedSolution, symbol, [document]);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task GIVEN_SameDocumentsInDifferentOrder_WHEN_ComparingKeys_THEN_ShouldBeEqual()
    {
        using var solution = CreateSolution();
        var firstDocument = solution.GetDocument("Code.cs");
        var secondDocument = solution.GetDocument("Usage.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            firstDocument,
            "Current",
            TestContext.Current.CancellationToken);
        var first = new ReferenceDiscoveryCacheKey(solution.Solution, symbol, [firstDocument, secondDocument]);
        var second = new ReferenceDiscoveryCacheKey(solution.Solution, symbol, [secondDocument, firstDocument]);

        first.Should().Be(second);
    }

    private static InMemoryRoslynSolution CreateSolution()
    {
        return RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Code.cs",
                        Source = "class StateHolder { public int Current { get; set; } }",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Usage.cs",
                        Source = "class Usage { int Read(StateHolder value) => value.Current; }",
                    },
                ],
            },
        ]);
    }
}
