namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class FindImplementationsToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnImplementations()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindImplementationsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-implementations", target, new FindImplementationsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.IMessageFormatter",
            },
        });

        result.Data!.Implementations.Items.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter", StringComparison.Ordinal));
    }
}
