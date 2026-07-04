namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetChangeImpactToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnImpactSummary()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetChangeImpactTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-change-impact", target, new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Impact!.ReferenceCount.Should().BeGreaterThan(0);
        result.Data.Impact.CallerCount.Should().BeGreaterThan(0);
        result.Data.Locations.Should().Contain(static location => location.Context!.Contains("formatter.Format(\"hi\")", StringComparison.Ordinal));
    }
}
