namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindCalleesToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnCallees()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new FindCalleesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "find-callees", target, new FindCalleesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FormatterCaller.Call",
            },
        });

        result.Data!.Callees.Should().Contain(static callee => callee.DisplayName.Contains("GreetingFormatter.Format", StringComparison.Ordinal));
    }
}
