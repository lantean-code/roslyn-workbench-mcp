namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolDependentsToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDependents()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetSymbolDependentsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-symbol-dependents", target, new GetSymbolDependentsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Dependents.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("FormatterCaller.Call", StringComparison.Ordinal));
        result.Data.Dependents.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter.Format", StringComparison.Ordinal) && symbol.DisplayName.Contains("bool", StringComparison.Ordinal));
    }
}
