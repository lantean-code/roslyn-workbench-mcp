namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

[Trait("Category", "Integration")]
public sealed class RenameSymbolToolTests
{
    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingTool_THEN_ShouldStageMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var startResult = await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var target = new RenameSymbolTool();

        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "rename-symbol", target, new RenameSymbolRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.StateHolder",
            },
            NewName = "SessionState",
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, startResult.Data!.Transaction!.Revision),
        });

        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Operation.Should().Be("rename-symbol");
    }
}
