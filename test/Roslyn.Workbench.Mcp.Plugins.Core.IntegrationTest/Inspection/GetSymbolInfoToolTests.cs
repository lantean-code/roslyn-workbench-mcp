namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetSymbolInfoToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDocumentation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var resolveTarget = new ResolveSymbolTool();
        var target = new GetSymbolInfoTool();

        var resolve = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "resolve-symbol", resolveTarget, new ResolveSymbolRequest
        {
            Location = fixture.GetLocation("GreetingFormatter"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });
        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "get-symbol-info", target, new GetSymbolInfoRequest
        {
            Symbol = resolve.Data!.Selector!,
            IncludeDocumentation = true,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Documentation.Should().NotBeNull();
        result.Data.Symbol!.DisplayName.Should().Contain("GreetingFormatter");
    }
}
