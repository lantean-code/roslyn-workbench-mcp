namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetTypeHierarchyToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnHierarchy()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetTypeHierarchyTool();

        var result = await BundledCoreToolTestHarness.ExecuteSingletonQueryAsync(coordinator, "get-type-hierarchy", target, new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.GreetingFormatter",
            },
            IncludeDerived = true,
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.BaseTypes.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("FormatterBase", StringComparison.Ordinal));
        result.Data.Interfaces.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("IMessageFormatter", StringComparison.Ordinal));
        result.Data.DerivedTypes!.Items.Should().Contain(static node => node.Type!.DisplayName.Contains("DerivedGreetingFormatter", StringComparison.Ordinal));
    }
}
