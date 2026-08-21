using Roslyn.Workbench.Mcp.CodeActions.Composition;
using Roslyn.Workbench.Mcp.CodeActions.Discovery;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class BuiltInCodeActionStagingIntegrationTests
{
    [Fact]
    public async Task GIVEN_ProjectOptionRefactoring_WHEN_ListingBuiltInActions_THEN_ShouldOmitUnsupportedAction()
    {
        using var fixture = InspectionSampleFixture.Create(InspectionSampleProfile.NullableDisabled);
        var composition = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        composition.RefactoringProviders.Should().ContainSingle(provider =>
            CodeActionProviderIdentity.GetId(provider) == "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider");

        var workspaceOptions = new ComponentWorkspaceOptions
        {
            Boundary = ComponentWorkspaceBoundary.CodeActions,
            IncludeBuiltInCodeActions = true,
        };

        await using var coordinator = ComponentWorkspace.Create(workspaceOptions, composition);
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var location = fixture.GetCursorInDocument("EnableNullable.cs", "#nullable enable");
        if (location.Span is not { Document: not null } span)
        {
            throw new InvalidOperationException("The nullable-refactoring fixture location must be span-backed.");
        }

        var range = new TextSpanRange
        {
            Start = span.Start,
            Length = span.Length,
        };

        var request = new ListCodeActionsRequest
        {
            Document = span.Document,
            Range = range,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateTransactionStartSnapshot(open),
            Kinds = CodeActionKindSelection.Refactorings,
        };

        var result = await session.ListAsync(request, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Actions.Items.Should().NotContain(static action =>
            action.Title == "Enable nullable reference types in project");
    }

    [Fact]
    public async Task GIVEN_BuiltInCompilerFix_WHEN_ListingDocumentActions_THEN_ShouldPublishConciseDiagnosticAction()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateBuiltInCodeActionWorkspace();
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);

        var result = await session.ListAsync(new ListCodeActionsRequest
        {
            Document = new DocumentSelector
            {
                Path = "CandidateCodeFixes.cs",
            },
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open),
            Kinds = CodeActionKindSelection.CodeFixes,
            DiagnosticIds = ["CS0266"],
        }, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Actions.Items.Should().NotBeEmpty();
        result.Data.Actions.Items.Should().OnlyContain(static action =>
            action.Kind == CodeActionKind.CodeFix
            && action.ActionId != Guid.Empty
            && action.Location.Span.Length > 0
            && action.Diagnostics != null
            && action.Diagnostics.Items.Any(diagnostic => diagnostic.Id == "CS0266"));

        result.Data.Actions.TotalCount.Should().BeGreaterThanOrEqualTo(result.Data.Actions.Items.Count);
    }

    [Fact]
    public async Task GIVEN_BuiltInCodeFixProvider_WHEN_StagingDiscoveredAction_THEN_ShouldStageRepresentativeBuiltInMutation()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateBuiltInCodeActionWorkspace();
        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);

        var listed = await session.ListAsync(new ListCodeActionsRequest
        {
            Document = new DocumentSelector
            {
                Path = "CandidateCodeFixes.cs",
            },
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateTransactionStartSnapshot(open),
            Kinds = CodeActionKindSelection.CodeFixes,
            DiagnosticIds = ["CS0266"],
        }, TestContext.Current.CancellationToken);

        var actionId = listed.Data!.Actions.Items[0].ActionId;

        var result = await session.StageCodeActionAsync(new StageCodeActionRequest
        {
            ActionId = actionId,
            ExpectedSnapshot = BundledComponentWorkspaceFactory.CreateTransactionStartSnapshot(open),
        }, TestContext.Current.CancellationToken);

        var preview = await coordinator.PreviewTransactionAsync(
            TestContext.Current.CancellationToken,
            document: new DocumentSelector
            {
                Path = "CandidateCodeFixes.cs",
            },
            includeDiff: true);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "CandidateCodeFixes.cs");
        preview.Data.Diff.Should().NotBeNull();
        preview.Data.Diff!.Hunks.Should().NotBeEmpty();
    }
}
