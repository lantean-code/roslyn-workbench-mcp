namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionFixAllCompatibilityTests
{
    [Fact]
    public async Task GIVEN_IDE0410CodeFixes_WHEN_PreparingDocumentFixAll_THEN_ShouldStageEveryLabeledJumpChange()
    {
        using var fixture = InspectionSampleFixture.Create(InspectionSampleProfile.CSharpPreview);
        await using var coordinator = BundledComponentWorkspaceFactory.CreateBuiltInCodeActionWorkspace();
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var transactionStartSnapshot = BundledComponentWorkspaceFactory.CreateTransactionStartSnapshot(open);
        var document = new DocumentSelector
        {
            Path = "CandidateLabeledJumps.cs",
        };

        var listed = await session.ListAsync(new ListCodeActionsRequest
        {
            Document = document,
            ExpectedSnapshot = transactionStartSnapshot,
            Kinds = CodeActionKindSelection.CodeFixes,
            DiagnosticIds = ["IDE0410"],
        }, TestContext.Current.CancellationToken);

        var matchingActions = listed.Data!.Actions.Items
            .Where(static action => action.Title == "Use labeled jump statement")
            .ToArray();
        var originAction = matchingActions[0];

        var prepared = await session.PrepareFixAllAsync(new PrepareFixAllRequest
        {
            ActionId = originAction.ActionId,
            Scope = CodeActionFixAllScope.Document,
            ExpectedSnapshot = transactionStartSnapshot,
        }, TestContext.Current.CancellationToken);

        var staged = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = prepared.Data!.ActionId,
            ExpectedSnapshot = transactionStartSnapshot,
        }, TestContext.Current.CancellationToken);

        var stagedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(staged);
        await using var queryLease = coordinator.CodeActionContextFactory.CreateQueryContext(
            new ListCodeActionsRequest
            {
                Document = document,
                ExpectedSnapshot = stagedSnapshot,
                Kinds = CodeActionKindSelection.CodeFixes,
            },
            TestContext.Current.CancellationToken);

        var stagedDocument = queryLease.Context!.CurrentSolution.Projects
            .SelectMany(static project => project.Documents)
            .Single(static candidate => candidate.Name == "CandidateLabeledJumps.cs");
        var stagedSource = (await stagedDocument.GetTextAsync(TestContext.Current.CancellationToken)).ToString();

        listed.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        matchingActions.Should().HaveCount(2);
        matchingActions.Should().OnlyContain(static action =>
            action.FixAllScopes != null
            && action.FixAllScopes.Contains(CodeActionFixAllScope.Document));
        prepared.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        prepared.Data.Scope.Should().Be(CodeActionFixAllScope.Document);
        prepared.Data.AffectedDocuments.TotalCount.Should().Be(1);
        staged.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        staged.Data!.Transaction!.Revision.Should().Be(1);
        stagedSource.Should().Contain("break foundFirst;");
        stagedSource.Should().Contain("break foundSecond;");
        stagedSource.Should().NotContain("goto foundFirst;");
        stagedSource.Should().NotContain("goto foundSecond;");
    }
}
