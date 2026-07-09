namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeActions;

[Trait("Category", "Integration")]
public sealed class StageCodeFixToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_TestProviders_WHEN_ExecutingTool_THEN_ShouldStageCodeFix()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var listTarget = new ListCodeActionsTool();
        var target = new StageCodeFixTool();

        var list = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", listTarget, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("unused"),
            IncludeRefactorings = false,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var actionId = list.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId;
        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "stage-code-fix", target, new StageCodeFixRequest
        {
            ActionId = actionId,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });

        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Operation.Should().Be("stage-code-fix");
    }
}
