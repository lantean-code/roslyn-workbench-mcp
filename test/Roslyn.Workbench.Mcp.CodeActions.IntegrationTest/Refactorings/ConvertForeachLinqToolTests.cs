namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

[Trait("Category", "Integration")]
public sealed class ConvertForeachLinqToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_ActiveTransaction_WHEN_ExecutingTool_THEN_ShouldStageMutation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateBuiltInCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var target = new ConvertForeachLinqTool();

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "convert-foreach-linq", target, new ConvertForeachLinqRequest
        {
            Selection = fixture.GetLocation("foreach (var number in numbers)"),
            ConversionKind = ConvertForeachLinqKind.ForeachToQuery,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.ProposalResult.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.ProposalResult.Data!.CandidateSolution.Should().NotBeNull();
        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Operation.Should().Be("convert-foreach-linq");
        preview.Data!.Documents.Should().Contain(static change => change.Document!.Path == "Formatting.cs");
    }
}
