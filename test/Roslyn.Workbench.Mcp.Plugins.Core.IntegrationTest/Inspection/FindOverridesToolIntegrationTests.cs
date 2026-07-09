namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class FindOverridesToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnOverrides()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindOverridesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-overrides", target, new FindOverridesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterBase.Decorate(System.String)",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Overrides.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter.Decorate", StringComparison.Ordinal));
        result.Data.Overrides.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("DerivedGreetingFormatter.Decorate", StringComparison.Ordinal));
    }
}
