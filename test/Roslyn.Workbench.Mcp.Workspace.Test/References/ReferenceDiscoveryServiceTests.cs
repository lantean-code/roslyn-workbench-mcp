using System.Collections.Immutable;

using Microsoft.CodeAnalysis.FindSymbols;

using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.References.Caching;

namespace Roslyn.Workbench.Mcp.Workspace.Test.References;

public sealed class ReferenceDiscoveryServiceTests
{
    private readonly Mock<IWorkspaceQueryCacheScope> _queryCacheScope;
    private readonly Mock<IWorkspaceQueryCacheScopeFactory> _queryCacheScopeFactory;
    private readonly ReferenceDiscoveryService _target;

    public ReferenceDiscoveryServiceTests()
    {
        _queryCacheScope = new Mock<IWorkspaceQueryCacheScope>();
        _queryCacheScopeFactory = new Mock<IWorkspaceQueryCacheScopeFactory>();
        _queryCacheScopeFactory
            .Setup(item => item.CreateScope(
                It.IsAny<Guid>(),
                It.IsAny<Solution>(),
                It.IsAny<string>()))
            .Returns(_queryCacheScope.Object);

        _queryCacheScope
            .Setup(item => item.GetOrCreateAsync<ReferenceDiscoveryCacheKey, ReferenceDiscoveryCacheEntry>(
                It.IsAny<ReferenceDiscoveryCacheKey>(),
                It.IsAny<Func<CancellationToken, ValueTask<ReferenceDiscoveryCacheEntry?>>>(),
                It.IsAny<Func<ReferenceDiscoveryCacheEntry, long>>(),
                It.IsAny<Func<ReferenceDiscoveryCacheEntry, bool>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                ReferenceDiscoveryCacheKey _,
                Func<CancellationToken, ValueTask<ReferenceDiscoveryCacheEntry?>> factory,
                Func<ReferenceDiscoveryCacheEntry, long> _,
                Func<ReferenceDiscoveryCacheEntry, bool> _,
                CancellationToken cancellationToken) => factory(cancellationToken));

        _target = new ReferenceDiscoveryService(_queryCacheScopeFactory.Object);
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
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            document.Solution,
            symbol,
            [document.Document],
            includeDefinitions: false,
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        _queryCacheScopeFactory.VerifyNoOtherCalls();
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

        _queryCacheScope
            .Setup(item => item.GetOrCreateAsync<ReferenceDiscoveryCacheKey, ReferenceDiscoveryCacheEntry>(
                It.IsAny<ReferenceDiscoveryCacheKey>(),
                It.IsAny<Func<CancellationToken, ValueTask<ReferenceDiscoveryCacheEntry?>>>(),
                It.IsAny<Func<ReferenceDiscoveryCacheEntry, long>>(),
                It.IsAny<Func<ReferenceDiscoveryCacheEntry, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEntry);

        var result = await _target.FindReferencesAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            document.Solution,
            symbol,
            [document.Document],
            includeDefinitions: false,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _queryCacheScope.Verify(item => item.GetOrCreateAsync<ReferenceDiscoveryCacheKey, ReferenceDiscoveryCacheEntry>(
            It.IsAny<ReferenceDiscoveryCacheKey>(),
            It.IsAny<Func<CancellationToken, ValueTask<ReferenceDiscoveryCacheEntry?>>>(),
            It.IsAny<Func<ReferenceDiscoveryCacheEntry, long>>(),
            It.IsAny<Func<ReferenceDiscoveryCacheEntry, bool>>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            document.Solution,
            symbol,
            [document.Document],
            includeDefinitions: false,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _queryCacheScope.Verify(item => item.GetOrCreateAsync<ReferenceDiscoveryCacheKey, ReferenceDiscoveryCacheEntry>(
            It.IsAny<ReferenceDiscoveryCacheKey>(),
            It.IsAny<Func<CancellationToken, ValueTask<ReferenceDiscoveryCacheEntry?>>>(),
            It.IsAny<Func<ReferenceDiscoveryCacheEntry, long>>(),
            It.IsAny<Func<ReferenceDiscoveryCacheEntry, bool>>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
