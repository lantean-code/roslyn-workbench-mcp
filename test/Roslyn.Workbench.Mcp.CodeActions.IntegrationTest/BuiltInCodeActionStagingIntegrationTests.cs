namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class BuiltInCodeActionStagingIntegrationTests
{
    [Fact]
    public async Task GIVEN_BuiltInCompilerFix_WHEN_ListingDocumentActions_THEN_ShouldPublishConciseDiagnosticAction()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateBuiltInCodeActionWorkspace();
        var session = new CodeActionComponentTestSession(coordinator);
        await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var result = await session.ListAsync(new ListCodeActionsRequest
        {
            Document = new DocumentSelector
            {
                Path = "CandidateCodeFixes.cs",
            },
            Kinds = CodeActionKindSelection.CodeFixes,
            DiagnosticIds = ["CS0266"],
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Actions.Should().NotBeEmpty();
        result.Data.Actions.Should().OnlyContain(static action =>
            action.Kind == CodeActionKind.CodeFix
            && action.ActionId != Guid.Empty
            && action.Location.Span.Length > 0
            && action.Diagnostics != null
            && action.Diagnostics.Any(diagnostic => diagnostic.Id == "CS0266"));
        result.Data.ReturnedCount.Should().Be(result.Data.Actions.Count);
    }

    [Fact]
    public async Task GIVEN_BuiltInCodeFixProvider_WHEN_RemovingUnusedUsings_THEN_ShouldStageRepresentativeBuiltInMutation()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateBuiltInCodeActionWorkspace();
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
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
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var preview = await coordinator.PreviewTransactionAsync(
            TestContext.Current.CancellationToken,
            document: new DocumentSelector
            {
                Path = "Usings.cs",
            },
            includeDiff: true);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Usings.cs");
        preview.Data.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().NotBeEmpty();
    }
}
