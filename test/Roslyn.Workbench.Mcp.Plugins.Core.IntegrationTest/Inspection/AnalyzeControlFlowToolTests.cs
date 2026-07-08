namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class AnalyzeControlFlowToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnControlFlow()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new AnalyzeControlFlowTool();

        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "analyze-control-flow", target, new AnalyzeControlFlowRequest
        {
            Location = fixture.GetLocation("if (trimmed.Length == 0)"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Data!.Exits.Should().NotBeEmpty();
    }
}
