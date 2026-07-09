namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class FindCallersToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnCallers()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindCallersTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-callers", target, new FindCallersRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.GreetingFormatter.Format(System.String)",
            },
        });

        result.Data!.Callers.Items.Should().Contain(static caller => caller.Caller!.DisplayName.Contains("Call", StringComparison.Ordinal));
    }
}
