using Roslyn.Workbench.Mcp.CodeActions.Composition;
using Roslyn.Workbench.Mcp.CodeActions.References;
using Roslyn.Workbench.Mcp.Workspace.Results;

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
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        var listed = await session.ListAsync(
            CreateListRequest(
                fixture.GetLocation("StateHolder"),
                CodeActionKindSelection.All,
                BundledComponentWorkspaceFactory.CreateSnapshot(open, 0)),
            TestContext.Current.CancellationToken);

        listed.Data!.Actions.Items.Should().ContainSingle(static action => action.Title == "Apply test refactoring");
        listed.Data.Actions.Items.Should().NotContain(static action => action.Title == "Change signature test refactoring");
        listed.Data.Actions.Items.Should().NotContain(static action => action.Title == "Option gathering test refactoring");
        listed.Data.Actions.Items.Should().OnlyContain(static action => action.ActionId != Guid.Empty);
    }

    [Fact]
    public async Task GIVEN_ControlledRefactoringAndCodeFix_WHEN_StagingBoth_THEN_ShouldAdvanceRevisionsAndPreviewChanges()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        var refactorings = await ListActionsAsync(
            session,
            fixture.GetLocation("StateHolder"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
            includeCodeFixes: false);
        var refactoringActionId = refactorings.Data!.Actions.Items
            .Single(static action => action.Title == "Apply test refactoring")
            .ActionId;

        var stagedRefactoring = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = refactoringActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var codeFixes = await ListActionsAsync(
            session,
            fixture.GetLocation("unused"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
            includeRefactorings: false);
        var codeFixActionId = codeFixes.Data!.Actions.Items
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
    public async Task GIVEN_ControlledCodeFix_WHEN_PreparingSolutionFixAll_THEN_ShouldStageSolutionScope()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var codeFixes = await ListActionsAsync(
            session,
            fixture.GetLocation("unused"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
            includeRefactorings: false);

        var prepared = await session.PrepareFixAllAsync(new PrepareFixAllRequest
        {
            ActionId = codeFixes.Data!.Actions.Items.Single(static action => action.Title == "Apply test code fix").ActionId,
            Scope = CodeActionFixAllScope.Solution,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var result = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = prepared.Data!.ActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ControlledCodeFix_WHEN_PreparingAndStagingFixAll_THEN_ShouldUseStandardTransactionPath()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var originalSource = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        var codeFixes = await ListActionsAsync(
            session,
            fixture.GetLocation("unused"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
            includeRefactorings: false);
        var originActionId = codeFixes.Data!.Actions.Items
            .Single(static action => action.Title == "Apply test code fix")
            .ActionId;

        var prepared = await session.PrepareFixAllAsync(new PrepareFixAllRequest
        {
            ActionId = originActionId,
            Scope = CodeActionFixAllScope.Solution,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var sourceAfterPreparation = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        var staged = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = prepared.Data!.ActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);
        await coordinator.RollbackTransactionAsync(TestContext.Current.CancellationToken);
        var sourceAfterRollback = await File.ReadAllTextAsync(
            fixture.DocumentPath,
            TestContext.Current.CancellationToken);

        prepared.Data.Scope.Should().Be(CodeActionFixAllScope.Solution);
        prepared.Data.AffectedDocuments.TotalCount.Should().Be(2);
        prepared.Data.AffectedDocuments.Items.Should().HaveCount(2);
        prepared.Data.AffectedDocuments.HasMore.Should().BeFalse();
        sourceAfterPreparation.Should().Be(originalSource);
        staged.Data!.Transaction!.Revision.Should().Be(1);
        staged.Data.Summary.Should().Be("Fix all: Apply test code fix");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
        sourceAfterRollback.Should().Be(originalSource);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ReachableHistoryAndRedoBranch_WHEN_UndoingAndRestaging_THEN_ShouldRestoreAndActivelyEvictReferences()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();

        var revisionZeroSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0);
        var firstListing = await ListActionsAsync(session, fixture.GetLocation("StateHolder"), revisionZeroSnapshot, includeCodeFixes: false);
        var firstActionId = firstListing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;
        var retainedListing = await ListActionsAsync(session, fixture.GetLocation("StateHolder"), revisionZeroSnapshot, includeCodeFixes: false);
        var retainedActionId = retainedListing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;

        await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = firstActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var redoBranchListing = await ListActionsAsync(
            session,
            fixture.GetLocation("unused"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
            includeRefactorings: false);
        var redoBranchActionId = redoBranchListing.Data!.Actions.Items.Single(static action => action.Title == "Apply test code fix").ActionId;
        var nonCurrentResult = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = retainedActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
        }, TestContext.Current.CancellationToken);

        nonCurrentResult.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        nonCurrentResult.Error!.Code.Should().Be("SnapshotMismatch");
        referenceStore.TryGet(retainedActionId, out _).Should().BeTrue();
        referenceStore.TryGet(redoBranchActionId, out _).Should().BeTrue();

        await coordinator.MoveTransactionHistoryAsync(
            TransactionHistoryDirection.Undo,
            TestContext.Current.CancellationToken);

        var restoredResult = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = retainedActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        restoredResult.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        referenceStore.TryGet(redoBranchActionId, out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_UnconsumedTransactionReference_WHEN_RollingBack_THEN_ShouldActivelyEvictReference()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();

        var listing = await ListActionsAsync(
            session,
            fixture.GetLocation("StateHolder"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
            includeCodeFixes: false);
        var actionId = listing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;

        await coordinator.RollbackTransactionAsync(TestContext.Current.CancellationToken);

        referenceStore.TryGet(actionId, out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_UnconsumedTransactionReference_WHEN_Committing_THEN_ShouldActivelyEvictReference()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();

        var revisionZeroSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0);
        var firstListing = await ListActionsAsync(session, fixture.GetLocation("StateHolder"), revisionZeroSnapshot, includeCodeFixes: false);
        var stagedActionId = firstListing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;
        var retainedListing = await ListActionsAsync(session, fixture.GetLocation("StateHolder"), revisionZeroSnapshot, includeCodeFixes: false);
        var retainedActionId = retainedListing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;

        await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = stagedActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        referenceStore.TryGet(retainedActionId, out _).Should().BeTrue();

        await coordinator.CommitTransactionAsync(
            TestContext.Current.CancellationToken,
            expectedSnapshot: BundledComponentWorkspaceFactory.CreateSnapshot(open, 1));

        referenceStore.TryGet(retainedActionId, out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ReferenceForReachableRevision_WHEN_UndoingAndRedoing_THEN_ShouldRejectThenRestoreReference()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();

        var refactorings = await ListActionsAsync(
            session,
            fixture.GetLocation("StateHolder"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
            includeCodeFixes: false);
        await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = refactorings.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var codeFixes = await ListActionsAsync(
            session,
            fixture.GetLocation("unused"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
            includeRefactorings: false);
        var revisionOneActionId = codeFixes.Data!.Actions.Items.Single(static action => action.Title == "Apply test code fix").ActionId;

        await coordinator.MoveTransactionHistoryAsync(
            TransactionHistoryDirection.Undo,
            TestContext.Current.CancellationToken);

        var nonCurrentResult = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = revisionOneActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        nonCurrentResult.Outcome.Should().Be(CodeActionExecutionOutcome.Conflict);
        nonCurrentResult.Error!.Code.Should().Be("SnapshotMismatch");
        referenceStore.TryGet(revisionOneActionId, out _).Should().BeTrue();

        await coordinator.MoveTransactionHistoryAsync(
            TransactionHistoryDirection.Redo,
            TestContext.Current.CancellationToken);

        var restoredResult = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = revisionOneActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
        }, TestContext.Current.CancellationToken);

        restoredResult.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        restoredResult.Data!.Transaction!.Revision.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_CommittedSnapshotReference_WHEN_StartingTransaction_THEN_ShouldActivelyEvictReference()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();

        var listing = await ListActionsAsync(
            session,
            fixture.GetLocation("StateHolder"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open),
            includeCodeFixes: false);
        var actionId = listing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;

        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        referenceStore.TryGet(actionId, out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_CommittedSnapshotReference_WHEN_UnloadingWorkspace_THEN_ShouldActivelyEvictReference()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();

        var listing = await ListActionsAsync(
            session,
            fixture.GetLocation("StateHolder"),
            BundledComponentWorkspaceFactory.CreateSnapshot(open),
            includeCodeFixes: false);
        var actionId = listing.Data!.Actions.Items.Single(static action => action.Title == "Apply test refactoring").ActionId;

        await coordinator.CloseAsync(TestContext.Current.CancellationToken);

        referenceStore.TryGet(actionId, out _).Should().BeFalse();
    }

    private static async Task<CodeActionExecutionResult<CodeActionListData>> ListActionsAsync(
        CodeActionComponentTestSession session,
        LocationSelector location,
        SnapshotPrecondition expectedSnapshot,
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

        var request = CreateListRequest(location, kinds, expectedSnapshot);

        return await session.ListAsync(request, TestContext.Current.CancellationToken);
    }

    private static ListCodeActionsRequest CreateListRequest(
        LocationSelector location,
        CodeActionKindSelection kinds,
        SnapshotPrecondition expectedSnapshot)
    {
        var span = location.Span
            ?? throw new InvalidOperationException("The controlled provider location must be span-backed.");

        var document = span.Document
            ?? throw new InvalidOperationException("The controlled provider location must identify a document.");

        var range = new TextSpanRange
        {
            Start = span.Start,
            Length = span.Length,
        };

        return new ListCodeActionsRequest
        {
            Document = document,
            Range = range,
            ExpectedSnapshot = expectedSnapshot,
            Kinds = kinds,
        };
    }
}
