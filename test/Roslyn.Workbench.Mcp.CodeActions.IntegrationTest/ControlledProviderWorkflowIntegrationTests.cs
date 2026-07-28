using Roslyn.Workbench.Mcp.CodeActions.Composition;
using Roslyn.Workbench.Mcp.CodeActions.References;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class ControlledProviderWorkflowIntegrationTests
{
    private static readonly ICodeActionComposition _composition = BundledComponentWorkspaceFactory.CreateTestCodeActionComposition();

    [Fact]
    public async Task GIVEN_ControlledProviderHasOptionBackedActions_WHEN_ListingActions_THEN_ShouldOmitOptionBackedLeaves()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        var listed = await session.ListAsync(
            CreateListRequest(fixture.GetLocation("StateHolder"), CodeActionKindSelection.All),
            TestContext.Current.CancellationToken);

        listed.Data!.Actions.Should().ContainSingle(static action => action.Title == "Apply test refactoring");
        listed.Data.Actions.Should().NotContain(static action => action.Title == "Change signature test refactoring");
        listed.Data.Actions.Should().NotContain(static action => action.Title == "Option gathering test refactoring");
        listed.Data.Actions.Should().OnlyContain(static action => action.ActionId != Guid.Empty);
    }

    [Fact]
    public async Task GIVEN_ControlledRefactoringAndCodeFix_WHEN_StagingBoth_THEN_ShouldAdvanceRevisionsAndPreviewChanges()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        var refactorings = await ListActionsAsync(session, fixture.GetLocation("StateHolder"), includeCodeFixes: false);
        var refactoringActionId = refactorings.Data!.Actions
            .Single(static action => action.Title == "Apply test refactoring")
            .ActionId;

        var stagedRefactoring = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = refactoringActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var codeFixes = await ListActionsAsync(session, fixture.GetLocation("unused"), includeRefactorings: false);
        var codeFixActionId = codeFixes.Data!.Actions
            .Single(static action => action.Title == "Apply test code fix")
            .ActionId;

        var stagedCodeFix = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = codeFixActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
        }, TestContext.Current.CancellationToken);

        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        stagedRefactoring.Data!.Transaction!.Revision.Should().Be(1);
        stagedCodeFix.Data!.Transaction!.Revision.Should().Be(2);
        preview.Data!.Transaction!.Revision.Should().Be(2);
        preview.Data.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();
        referenceStore.TryGet(refactoringActionId, out _).Should().BeFalse();
        referenceStore.TryGet(codeFixActionId, out _).Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_ControlledCodeFix_WHEN_StagingSolutionFixAll_THEN_ShouldStageSolutionScope()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var codeFixes = await ListActionsAsync(session, fixture.GetLocation("unused"), includeRefactorings: false);

        var result = await session.StageFixAllAsync(new StageFixAllRequest
        {
            ActionId = codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId,
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            },
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Summary.Should().Be("Fix all: Apply test code fix");
        result.Data.Transaction!.Revision.Should().Be(1);
    }

    private static async Task<CodeActionExecutionResult<CodeActionListData>> ListActionsAsync(
        CodeActionComponentTestSession session,
        LocationSelector location,
        bool includeRefactorings = true,
        bool includeCodeFixes = true)
    {
        var kinds = (includeRefactorings, includeCodeFixes) switch
        {
            (true, true) => CodeActionKindSelection.All,
            (true, false) => CodeActionKindSelection.Refactorings,
            (false, true) => CodeActionKindSelection.CodeFixes,
            _ => throw new InvalidOperationException("At least one action kind must be selected."),
        };

        var request = CreateListRequest(location, kinds);

        return await session.ListAsync(request, TestContext.Current.CancellationToken);
    }

    private static ListCodeActionsRequest CreateListRequest(
        LocationSelector location,
        CodeActionKindSelection kinds)
    {
        var span = location.Span
            ?? throw new InvalidOperationException("The controlled provider location must be span-backed.");
        var document = span.Document
            ?? throw new InvalidOperationException("The controlled provider location must identify a document.");

        return new ListCodeActionsRequest
        {
            Document = document,
            Range = new TextSpanRange
            {
                Start = span.Start,
                Length = span.Length,
            },
            Kinds = kinds,
        };
    }
}
