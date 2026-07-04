namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.CodeActions;

public sealed class ListCodeActionsToolTests
{
    [Fact]
    public async Task GIVEN_DefaultCoordinator_WHEN_ExecutingTool_THEN_ShouldRejectAsUnavailable()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new ListCodeActionsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", target, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("StateHolder"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("CodeActionsUnavailable");
    }

    [Fact]
    public async Task GIVEN_TestProviders_WHEN_ExecutingTool_THEN_ShouldReturnDeterministicActions()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new ListCodeActionsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", target, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("StateHolder"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Actions.Select(static action => action.Title).Should().ContainInOrder(["Apply test refactoring", "Change signature test refactoring", "Option gathering test refactoring", "Retain test state", "Unsupported test refactoring"]);
    }
}
