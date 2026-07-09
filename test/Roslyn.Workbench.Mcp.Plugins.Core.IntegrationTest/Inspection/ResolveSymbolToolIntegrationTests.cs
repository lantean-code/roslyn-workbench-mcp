namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class ResolveSymbolToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldResolveSymbol()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new ResolveSymbolTool();

        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "resolve-symbol", target, new ResolveSymbolRequest
        {
            Location = fixture.GetLocation("GreetingFormatter"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
        result.Data.Selector.Should().NotBeNull();
    }
}
