namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class AnalyzeDataFlowToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDataFlow()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new AnalyzeDataFlowTool();

        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "analyze-data-flow", target, new AnalyzeDataFlowRequest
        {
            Location = fixture.GetLocation("var upper = trimmed.ToUpperInvariant();"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Data!.DataFlowsOut.Should().Contain(static symbol => symbol.DisplayName.Contains("upper", StringComparison.Ordinal));
    }
}
