namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.CodeActions;

[Trait("Category", "Integration")]
public sealed class DescribeCodeActionToolTests
{
    [Fact]
    public async Task GIVEN_ParameterisedAction_WHEN_ExecutingTool_THEN_ShouldDescribeContext()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var listTarget = new ListCodeActionsTool();
        var target = new DescribeCodeActionTool();

        var list = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", listTarget, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("StateHolder"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });
        var actionId = list.Data!.Actions.Single(static action => action.Title == "Change signature test refactoring").ActionId;
        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "describe-code-action", target, new DescribeCodeActionRequest
        {
            ActionId = actionId,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Descriptor.Title.Should().Be("Change signature test refactoring");
        result.Data.Context.Kind.Should().Be(CodeActionDescriptorContextKind.SignaturePlan);
    }
}
