namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddAwaitToolTests
{
    [Fact]
    public async Task GIVEN_AwaitKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithAwaitActionPath()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddAwaitRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Kind = AddAwaitKind.Await,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new AddAwaitTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
                "Add 'await'",
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 0)))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            "Add 'await'",
            null,
            null,
            null,
            It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 0)), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AwaitConfigureAwaitFalseKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithConfigureAwaitActionPath()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddAwaitRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Kind = AddAwaitKind.AwaitConfigureAwaitFalse,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new AddAwaitTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
                "Add 'await' and 'ConfigureAwait(false)'",
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 1)))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            context.Object,
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            "Add 'await' and 'ConfigureAwait(false)'",
            null,
            null,
            null,
            It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 1)), Times.Once);
    }
}
