namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

[Trait("Category", "Integration")]
public sealed class StageCodeActionToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_TestProviders_WHEN_ExecutingTool_THEN_ShouldStageRefactoring()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var listTarget = new ListCodeActionsTool();
        var target = new StageCodeActionTool();

        var list = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", listTarget, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("StateHolder"),
            IncludeCodeFixes = false,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var actionId = list.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId;
        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "stage-code-action", target, new StageCodeActionRequest
        {
            ActionId = actionId,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });

        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Operation.Should().Be("stage-code-action");
    }

    [Fact]
    public async Task GIVEN_ParameterisedAction_WHEN_ExecutingTool_THEN_ShouldRejectParametersRequired()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var listTarget = new ListCodeActionsTool();
        var target = new StageCodeActionTool();

        var list = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", listTarget, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("StateHolder"),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var actionId = list.Data!.Actions.Single(static action => action.Title == "Change signature test refactoring").ActionId;
        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "stage-code-action", target, new StageCodeActionRequest
        {
            ActionId = actionId,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });

        result.ProposalResult.Outcome.Should().Be(ToolOutcome.Rejected);
        result.ProposalResult.Error!.Code.Should().Be("ActionRequiresParameters");
    }
}
