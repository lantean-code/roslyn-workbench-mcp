using Roslyn.Workbench.Mcp.Workspace.References.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.References.Caching;

public sealed class ReferenceDiscoveryCacheKeyTests
{
    [Fact]
    public async Task GIVEN_EquivalentSymbolAndDocuments_WHEN_Comparing_THEN_ShouldBeEqualRegardlessOfDocumentOrder()
    {
        using var solution = CreateSolution();
        var symbols = await GetSymbolsAsync(solution);
        var firstDocument = solution.GetDocument("First.cs");
        var secondDocument = solution.GetDocument("Second.cs");
        var first = new ReferenceDiscoveryCacheKey(symbols.First, [firstDocument, secondDocument]);
        var second = new ReferenceDiscoveryCacheKey(symbols.First, [secondDocument, firstDocument]);

        var typedResult = first.Equals(second);
        var objectResult = first.Equals((object)second);

        typedResult.Should().BeTrue();
        objectResult.Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public async Task GIVEN_NullOrDifferentObjectType_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var symbols = await GetSymbolsAsync(solution);
        var document = solution.GetDocument("First.cs");
        var typedTarget = new ReferenceDiscoveryCacheKey(symbols.First, [document]);
        var objectTarget = new ReferenceDiscoveryCacheKey(symbols.First, [document]);
        var differentTypeTarget = new ReferenceDiscoveryCacheKey(symbols.First, [document]);

#pragma warning disable CA1508 // Explicitly exercise both nullable equality contracts.
        var typedResult = typedTarget.Equals(other: null);
        var nullObjectResult = object.Equals(objectTarget, null);
#pragma warning restore CA1508
        var differentTypeResult = differentTypeTarget.Equals(new object());

        typedResult.Should().BeFalse();
        nullObjectResult.Should().BeFalse();
        differentTypeResult.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_DifferentSymbol_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var symbols = await GetSymbolsAsync(solution);
        var document = solution.GetDocument("First.cs");
        var target = new ReferenceDiscoveryCacheKey(symbols.First, [document]);
        var other = new ReferenceDiscoveryCacheKey(symbols.Second, [document]);

        target.Equals(other).Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_DifferentDocument_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var symbols = await GetSymbolsAsync(solution);
        var target = new ReferenceDiscoveryCacheKey(symbols.First, [solution.GetDocument("First.cs")]);
        var other = new ReferenceDiscoveryCacheKey(symbols.First, [solution.GetDocument("Second.cs")]);

        target.Equals(other).Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_DifferentDocumentCount_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        using var solution = CreateSolution();
        var symbols = await GetSymbolsAsync(solution);
        var firstDocument = solution.GetDocument("First.cs");
        var secondDocument = solution.GetDocument("Second.cs");
        var target = new ReferenceDiscoveryCacheKey(symbols.First, [firstDocument]);
        var other = new ReferenceDiscoveryCacheKey(symbols.First, [firstDocument, secondDocument]);

        target.Equals(other).Should().BeFalse();
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
                        Name = "First.cs",
                        Source = "internal sealed class First;",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Second.cs",
                        Source = "internal sealed class Second;",
                    },
                ],
            },
        ]);
    }

    private static async Task<(INamedTypeSymbol First, INamedTypeSymbol Second)> GetSymbolsAsync(InMemoryRoslynSolution solution)
    {
        var compilation = await solution
            .GetProject("Project")
            .GetCompilationAsync(TestContext.Current.CancellationToken);

        var requiredCompilation = compilation
            ?? throw new InvalidOperationException("The test compilation could not be created.");
        var first = requiredCompilation.GetTypeByMetadataName("First")
            ?? throw new InvalidOperationException("The First symbol could not be resolved.");
        var second = requiredCompilation.GetTypeByMetadataName("Second")
            ?? throw new InvalidOperationException("The Second symbol could not be resolved.");

        return (first, second);
    }
}
