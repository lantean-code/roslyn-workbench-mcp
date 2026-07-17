namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class BuiltInCodeActionStagingIntegrationTests
{
    [Fact]
    public async Task GIVEN_BuiltInCodeFixProvider_WHEN_RemovingUnusedUsings_THEN_ShouldStageRepresentativeBuiltInMutation()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        await using var coordinator = BundledCoreToolTestHarness.CreateBuiltInCodeActionCoordinator();
        await using var session = CodeActionComponentTestSession.Create(coordinator);
        var open = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(new TransactionStartRequest(), TestContext.Current.CancellationToken);
        var result = await session.RemoveUnusedUsingsAsync(new RemoveUnusedUsingsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Usings.cs",
                },
            },
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);
        var preview = await coordinator.PreviewTransactionAsync(new TransactionPreviewRequest
        {
            IncludeDiff = true,
            Document = new DocumentSelector
            {
                Path = "Usings.cs",
            },
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Usings.cs");
        preview.Data.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().NotBeEmpty();
    }
}
