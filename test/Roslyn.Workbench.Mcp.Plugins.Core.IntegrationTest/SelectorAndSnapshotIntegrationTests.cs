using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SelectorAndSnapshotIntegrationTests
{
    [Fact]
    public async Task GIVEN_AmbiguousTextSelection_WHEN_ResolvingSymbol_THEN_ShouldRejectAmbiguousLocation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var result = await PluginToolTestHarness.InvokeAsync<ResolveSymbolData>(coordinator, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(new LocationSelector
            {
                Selection = new TextSelectionSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Formatting.cs",
                    },
                    SelectedText = "Format",
                },
            }),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationAmbiguous");
    }

    [Fact]
    public async Task GIVEN_StaleSnapshot_WHEN_ResolvingSymbol_THEN_ShouldRejectSnapshotMismatch()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var result = await PluginToolTestHarness.InvokeAsync<ResolveSymbolData>(coordinator, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("GreetingFormatter")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value + 1,
            }),
        }, expectProtocolSuccess: false);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_MetadataSymbolAndBoundedSearch_WHEN_InspectingSelectors_THEN_ShouldProjectMetadataAndTruncation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        var resolved = await PluginToolTestHarness.InvokeAsync<ResolveSymbolData>(coordinator, registry, "resolve-symbol", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("ToUpperInvariant")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            }),
        });
        var definition = await PluginToolTestHarness.InvokeAsync<DefinitionData>(coordinator, registry, "go-to-definition", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(resolved.Data!.Selector),
        });
        var search = await PluginToolTestHarness.InvokeAsync<SymbolSearchData>(coordinator, registry, "search-symbols", new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("Format"),
            ["symbolsLimit"] = JsonSerializer.SerializeToElement(new CollectionLimit
            {
                MaxResults = 1,
            }),
        });

        definition.Data!.Definitions.Should().ContainSingle(static location => location.IsMetadata);
        search.Data!.Symbols.Items.Should().HaveCount(1);
        search.Data.Symbols.HasMore.Should().BeTrue();
    }
}
