namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SearchSymbolsIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MultiProjectWorkspace_WHEN_SearchingEveryScopeKind_THEN_ShouldConstrainResultsBeforeBounding()
    {
        using var fixture = SolutionHierarchyFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.SolutionPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var solutionResult = await session.ExecuteQueryAsync<SearchSymbolsRequest, SymbolSearchData>(
            "search-symbols",
            new SearchSymbolsRequest
            {
                Query = "Format",
                Kinds = ["Method"],
                SymbolsLimit = 1,
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Solution,
                },
            },
            TestContext.Current.CancellationToken);

        var projectResult = await session.ExecuteQueryAsync<SearchSymbolsRequest, SymbolSearchData>(
            "search-symbols",
            new SearchSymbolsRequest
            {
                Query = "Format",
                Kinds = ["Method"],
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Project,
                    Project = new ProjectSelector
                    {
                        Name = "App",
                    },
                },
            },
            TestContext.Current.CancellationToken);

        var projectsResult = await session.ExecuteQueryAsync<SearchSymbolsRequest, SymbolSearchData>(
            "search-symbols",
            new SearchSymbolsRequest
            {
                Query = "Format",
                Kinds = ["Method"],
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Projects,
                    Projects =
                    [
                        new ProjectSelector
                        {
                            Name = "App",
                        },
                        new ProjectSelector
                        {
                            Name = "Lib",
                        },
                    ],
                },
            },
            TestContext.Current.CancellationToken);

        var documentResult = await session.ExecuteQueryAsync<SearchSymbolsRequest, SymbolSearchData>(
            "search-symbols",
            new SearchSymbolsRequest
            {
                Query = "Format",
                Kinds = ["Method"],
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Document,
                    Document = new DocumentSelector
                    {
                        Path = "Lib/MessageFormatter.cs",
                    },
                },
            },
            TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);

        solutionResult.Data!.Symbols.Items.Should().ContainSingle();
        solutionResult.Data.Symbols.HasMore.Should().BeTrue();
        solutionResult.Data.Symbols.TotalCount.Should().Be(2);

        projectResult.Data!.Symbols.Items.Should().ContainSingle();
        projectResult.Data.Symbols.Items[0].Location!.Document!.Path.Should().Be("App/AppFormatter.cs");
        projectResult.Data.Symbols.HasMore.Should().BeFalse();
        projectResult.Data.Symbols.TotalCount.Should().Be(1);

        projectsResult.Data!.Symbols.Items.Should().HaveCount(2);
        projectsResult.Data.Symbols.Items.Select(static item => item.Location!.Document!.Path).Should().BeEquivalentTo(
            "App/AppFormatter.cs",
            "Lib/MessageFormatter.cs");
        projectsResult.Data.Symbols.HasMore.Should().BeFalse();
        projectsResult.Data.Symbols.TotalCount.Should().Be(2);

        documentResult.Data!.Symbols.Items.Should().ContainSingle();
        documentResult.Data.Symbols.Items[0].Location!.Document!.Path.Should().Be("Lib/MessageFormatter.cs");
        documentResult.Data.Symbols.HasMore.Should().BeFalse();
        documentResult.Data.Symbols.TotalCount.Should().Be(1);
    }
}
