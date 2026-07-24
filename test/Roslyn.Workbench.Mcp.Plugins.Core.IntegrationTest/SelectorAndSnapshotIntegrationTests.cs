namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SelectorAndSnapshotIntegrationTests
{
    [Fact]
    public async Task GIVEN_AmbiguousTextSelection_WHEN_ResolvingSymbol_THEN_ShouldRejectAmbiguousLocation()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var result = await session.ExecuteQueryAsync<ResolveSymbolRequest, ResolveSymbolData>(
            "resolve-symbol",
            new ResolveSymbolRequest
            {
                Location = new LocationSelector
                {
                    Selection = new TextSelectionSelector
                    {
                        Document = new DocumentSelector
                        {
                            Path = "Formatting.cs",
                        },
                        SelectedText = "Format",
                    },
                },
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
                },
            }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationAmbiguous");
    }

    [Fact]
    public async Task GIVEN_MetadataSymbolAndBoundedSearch_WHEN_InspectingSelectors_THEN_ShouldProjectMetadataAndTruncation()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var resolved = await session.ExecuteQueryAsync<ResolveSymbolRequest, ResolveSymbolData>(
            "resolve-symbol",
            new ResolveSymbolRequest
            {
                Location = fixture.GetLocation("ToUpperInvariant"),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
                },
            }, TestContext.Current.CancellationToken);

        var resolvedSelector = resolved.Data?.Selector
            ?? throw new InvalidOperationException("Resolve symbol did not return its canonical selector.");

        var definition = await session.ExecuteQueryAsync<GoToDefinitionRequest, DefinitionData>(
            "go-to-definition",
            new GoToDefinitionRequest
            {
                Symbol = resolvedSelector,
            }, TestContext.Current.CancellationToken);

        var search = await session.ExecuteQueryAsync<SearchSymbolsRequest, SymbolSearchData>(
            "search-symbols",
            new SearchSymbolsRequest
            {
                Query = "Format",
                SymbolsLimit = 1,
            }, TestContext.Current.CancellationToken);

        definition.Data!.Definitions.Should().ContainSingle(static location => location.IsMetadata);
        search.Data!.Symbols.Items.Should().HaveCount(1);
        search.Data.Symbols.HasMore.Should().BeTrue();
    }
}
