using System.Collections.Immutable;

using Microsoft.CodeAnalysis.FindSymbols;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.References.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.References;

public sealed class ReferenceDiscoveryServiceTests
{
    private readonly Mock<IQueryCache> _queryCache;
    private readonly ReferenceDiscoveryService _target;

    public ReferenceDiscoveryServiceTests()
    {
        _queryCache = new Mock<IQueryCache>();
        _target = new ReferenceDiscoveryService(_queryCache.Object);
    }

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_FindingReferences_THEN_ShouldStopBeforeUsingCache()
    {
        using var document = RoslynTestFactory.CreateDocument("internal sealed class Target;");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Target",
            TestContext.Current.CancellationToken);

        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.FindReferencesAsync(
            "WorkspaceId",
            document.Solution,
            symbol,
            [document.Document],
            includeDefinitions: false,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _queryCache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GIVEN_CachedDiscovery_WHEN_FindingReferences_THEN_ShouldReturnCachedOccurrencesWithoutStoring()
    {
        using var document = RoslynTestFactory.CreateDocument("internal sealed class Target;");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Target",
            TestContext.Current.CancellationToken);

        var cachedEntry = new ReferenceDiscoveryCacheEntry(
            ImmutableArray<ReferencedSymbol>.Empty);

        _queryCache
            .Setup(item => item.TryGet<ReferenceDiscoveryCacheEntry>(
                "WorkspaceId",
                It.IsAny<object>(),
                out cachedEntry))
            .Returns(true);

        var result = await _target.FindReferencesAsync(
            "WorkspaceId",
            document.Solution,
            symbol,
            [document.Document],
            includeDefinitions: false,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _queryCache.Verify(item => item.Store(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<ReferenceDiscoveryCacheEntry>(),
            It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DiscoveryIsNotCached_WHEN_FindingReferences_THEN_ShouldStoreCompletedDiscovery()
    {
        using var document = RoslynTestFactory.CreateDocument("internal sealed class Target;");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Target",
            TestContext.Current.CancellationToken);

        var result = await _target.FindReferencesAsync(
            "WorkspaceId",
            document.Solution,
            symbol,
            [document.Document],
            includeDefinitions: false,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _queryCache.Verify(item => item.Store(
            "WorkspaceId",
            It.IsAny<ReferenceDiscoveryCacheKey>(),
            It.IsAny<ReferenceDiscoveryCacheEntry>(),
            It.Is<long>(size => size > 0)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DefinitionIsOutsideSelectedDocuments_WHEN_FindingReferences_THEN_ShouldExcludeDefinition()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "StateHolder.cs",
                        Source = """
                            class StateHolder
                            {
                                public int Current
                                {
                                    get;
                                    set;
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Usage.cs",
                        Source = """
                            class Usage
                            {
                                int Read(StateHolder holder)
                                {
                                    return holder.Current;
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var definitionDocument = solution.GetDocument("StateHolder.cs");
        var usageDocument = solution.GetDocument("Usage.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            definitionDocument,
            "Current",
            TestContext.Current.CancellationToken);

        var result = await _target.FindReferencesAsync(
            "WorkspaceId",
            solution.Solution,
            symbol,
            [usageDocument],
            includeDefinitions: true,
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result.Should().NotContain(item => item.IsDefinition);
        result.Single().Document.Should().BeSameAs(usageDocument);
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentsExistInMultipleProjectContexts_WHEN_FindingReferencesForOneProject_THEN_ShouldExcludeOtherProjectContexts()
    {
        const string definitionSource = """
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }
            """;
        const string usageSource = """
            class Usage
            {
                int Read(StateHolder holder)
                {
                    return holder.Current;
                }
            }
            """;

        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project (net10.0)",
                AssemblyName = "Project",
                FilePath = "/workspace/Project.csproj",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "StateHolder.cs",
                        FilePath = "/workspace/StateHolder.cs",
                        Source = definitionSource,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Usage.cs",
                        FilePath = "/workspace/Usage.cs",
                        Source = usageSource,
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project (net9.0)",
                AssemblyName = "Project",
                FilePath = "/workspace/Project.csproj",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "StateHolder.cs",
                        FilePath = "/workspace/StateHolder.cs",
                        Source = definitionSource,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Usage.cs",
                        FilePath = "/workspace/Usage.cs",
                        Source = usageSource,
                    },
                ],
            },
        ]);

        var definitionDocument = solution.GetDocument("StateHolder.cs", "Project (net10.0)");
        var selectedDocuments = solution.GetProject("Project (net10.0)").Documents.ToArray();
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            definitionDocument,
            "Current",
            TestContext.Current.CancellationToken);

        var result = await _target.FindReferencesAsync(
            "WorkspaceId",
            solution.Solution,
            symbol,
            selectedDocuments,
            includeDefinitions: true,
            TestContext.Current.CancellationToken);

        result.Should().HaveCount(5);
        result.Count(item => item.IsDefinition).Should().Be(4);
        result.Should().ContainSingle(item => !item.IsDefinition);
        result.Select(item => item.Document.Project.Id).Should().OnlyContain(
            projectId => projectId == definitionDocument.Project.Id);
    }
}
