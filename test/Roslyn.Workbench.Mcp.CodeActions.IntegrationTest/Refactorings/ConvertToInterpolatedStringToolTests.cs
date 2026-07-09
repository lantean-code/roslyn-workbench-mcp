namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

[Trait("Category", "Integration")]
public sealed class ConvertToInterpolatedStringToolIntegrationTests
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
        var target = new ConvertToInterpolatedStringTool();

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "convert-to-interpolated-string", target, new ConvertToInterpolatedStringRequest
        {
            Selection = fixture.GetLocation("formatted + \"!\""),
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest(), CancellationToken.None);

        result.ProposalResult.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.ProposalResult.Data!.CandidateSolution.Should().NotBeNull();
        result.StagedResult!.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.StagedResult.Data!.Operation.Should().Be("convert-to-interpolated-string");
        preview.Data!.Documents.Should().Contain(static change => change.Document!.Path == "Formatting.cs");
    }

    [Fact]
    public async Task GIVEN_AmbiguousSelection_WHEN_ExecutingTool_THEN_ShouldRejectLocation()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateBuiltInCodeActionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), CancellationToken.None);
        var target = new ConvertToInterpolatedStringTool();

        var result = await BundledCoreToolTestHarness.ExecuteMutationAsync(coordinator, "convert-to-interpolated-string", target, new ConvertToInterpolatedStringRequest
        {
            Selection = new LocationSelector
            {
                Selection = new TextSelectionSelector
                {
                    Document = new DocumentSelector
                    {
                        Path = "Formatting.cs",
                    },
                    SelectedText = "Format",
                },
            },
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        });

        result.ProposalResult.Outcome.Should().Be(ToolOutcome.Rejected);
        result.ProposalResult.Error!.Code.Should().Be("LocationAmbiguous");
    }
}
