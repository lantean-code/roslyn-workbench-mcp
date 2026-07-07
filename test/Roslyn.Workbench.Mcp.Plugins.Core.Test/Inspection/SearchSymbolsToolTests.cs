namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class SearchSymbolsToolTests
{
    [Fact]
    public async Task GIVEN_MultipleMatches_WHEN_ExecutingTool_THEN_ShouldReportHasMore()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new SearchSymbolsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "search-symbols", target, new SearchSymbolsRequest
        {
            Query = "Format",
            SymbolsLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(1);
        result.Data.Symbols.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_EmptyQuery_WHEN_ExecutingTool_THEN_ShouldRejectRequest()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new SearchSymbolsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "search-symbols", target, new SearchSymbolsRequest());

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }
}
