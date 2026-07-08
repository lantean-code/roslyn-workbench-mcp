namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetSymbolDependenciesToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDependencies()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetSymbolDependenciesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-symbol-dependencies", target, new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Dependencies.Items.Should().Contain(static dependency => dependency.Symbol!.DisplayName.Contains("ToUpperInvariant", StringComparison.Ordinal));
        result.Data.Dependencies.Items.Should().Contain(static dependency => dependency.Symbol!.DisplayName.Contains("Decorate", StringComparison.Ordinal));
    }
}
