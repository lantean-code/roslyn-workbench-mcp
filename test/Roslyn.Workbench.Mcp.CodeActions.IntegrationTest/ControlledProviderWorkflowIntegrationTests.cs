using Roslyn.Workbench.Mcp.CodeActions.Composition;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class ControlledProviderWorkflowIntegrationTests
{
    private static readonly ICodeActionProviderCatalog _providerCatalog = BundledComponentWorkspaceFactory.CreateTestCodeActionProviderCatalog();

    [Fact]
    public async Task GIVEN_ControlledProviderActions_WHEN_ListingDescribingAndStagingParameterisedAction_THEN_ShouldPreserveWorkflowContracts()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_providerCatalog);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var snapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0);

        var listed = await session.ListAsync(new ListCodeActionsRequest
        {
            Location = fixture.GetLocation("StateHolder"),
            ExpectedSnapshot = snapshot,
        }, TestContext.Current.CancellationToken);

        var parameterisedAction = listed.Data!.Actions.Single(static action => action.Title == "Change signature test refactoring");
        var described = await session.DescribeAsync(new DescribeCodeActionRequest
        {
            ActionId = parameterisedAction.ActionId,
            ExpectedSnapshot = snapshot,
        }, TestContext.Current.CancellationToken);

        var staged = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = parameterisedAction.ActionId,
            ExpectedSnapshot = snapshot,
        }, TestContext.Current.CancellationToken);

        listed.Data.Actions.Should().OnlyContain(static action => !string.IsNullOrWhiteSpace(action.ActionId));
        described.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        described.Data!.Descriptor.Title.Should().Be("Change signature test refactoring");
        described.Data.Context.Kind.Should().Be(CodeActionDescriptorContextKind.SignaturePlan);
        staged.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        staged.Error!.Code.Should().Be("ActionRequiresParameters");
    }

    [Fact]
    public async Task GIVEN_ControlledRefactoringAndCodeFix_WHEN_StagingBoth_THEN_ShouldAdvanceRevisionsAndPreviewChanges()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_providerCatalog);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        var refactorings = await ListActionsAsync(session, fixture.GetLocation("StateHolder"), open, 0, includeCodeFixes: false);
        var stagedRefactoring = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = refactorings.Data!.Actions.Single(static action => action.Title == "Apply test refactoring").ActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 0),
        }, TestContext.Current.CancellationToken);

        var codeFixes = await ListActionsAsync(session, fixture.GetLocation("unused"), open, 1, includeRefactorings: false);
        var stagedCodeFix = await session.StageCodeFixAsync(new StageCodeFixRequest
        {
            ActionId = codeFixes.Data!.Actions.Single(static action => action.Title == "Apply test code fix").ActionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, 1),
        }, TestContext.Current.CancellationToken);

        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        stagedRefactoring.Data!.Transaction!.Revision.Should().Be(1);
        stagedCodeFix.Data!.Transaction!.Revision.Should().Be(2);
        preview.Data!.Transaction!.Revision.Should().Be(2);
        preview.Data.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
    }

    [Fact]
    public async Task GIVEN_ControlledCodeFix_WHEN_StagingSolutionFixAll_THEN_ShouldStageSolutionScope()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateTestCodeActionWorkspace(_providerCatalog);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var codeFixes = await ListActionsAsync(session, fixture.GetLocation("unused"), open, 0, includeRefactorings: false);

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
        WorkspaceOperationResult<WorkspaceOpenOutcome> open,
        int transactionRevision,
        bool includeRefactorings = true,
        bool includeCodeFixes = true)
    {
        var request = new ListCodeActionsRequest
        {
            Location = location,
            IncludeRefactorings = includeRefactorings,
            IncludeCodeFixes = includeCodeFixes,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, transactionRevision),
        };

        return await session.ListAsync(request, TestContext.Current.CancellationToken);
    }
}
