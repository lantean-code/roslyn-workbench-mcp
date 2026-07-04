namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.CodeActions;

public sealed class StageFixAllToolTests
{
    [Fact]
    public async Task GIVEN_TestProviders_WHEN_ExecutingTool_THEN_ShouldStageFixAll()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var listTarget = new ListCodeActionsTool();
        var target = new StageFixAllTool();

        var list = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", listTarget, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("unused"),
            IncludeRefactorings = false,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var actionId = list.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId;
        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "stage-fix-all", target, new StageFixAllRequest
        {
            ActionId = actionId,
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            },
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });

        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Summary.Should().Be("Fix all: Apply test code fix");
    }

    [Fact]
    public async Task GIVEN_InsufficientCap_WHEN_ExecutingTool_THEN_ShouldReject()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateTestCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var listTarget = new ListCodeActionsTool();
        var target = new StageFixAllTool();

        var list = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "list-code-actions", listTarget, new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("unused"),
            IncludeRefactorings = false,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var actionId = list.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId;
        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "stage-fix-all", target, new StageFixAllRequest
        {
            ActionId = actionId,
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            },
            MaxChanges = 0,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });

        result.ProposalResult.Outcome.Should().Be(ToolOutcome.Rejected);
        result.ProposalResult.Error!.Code.Should().Be("FixAllLimitExceeded");
    }
}
