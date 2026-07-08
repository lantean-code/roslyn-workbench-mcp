namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

[Trait("Category", "Integration")]
public sealed class SortUsingsToolTests
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
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var target = new SortUsingsTool();

        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "sort-usings", target, new SortUsingsRequest
        {
            Document = new DocumentSelector
            {
                Path = "Usings.cs",
            },
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Operation.Should().Be("sort-usings");
        preview.Data!.Documents.Should().Contain(static change => change.Document!.Path == "Usings.cs");
    }
}
