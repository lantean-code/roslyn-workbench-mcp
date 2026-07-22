namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class UseNamedArgumentsToolTests
{
    [Fact]
    public async Task GIVEN_UseNamedArgumentsRequestWithoutTrailingArguments_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithoutTrailingArgumentsTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new UseNamedArgumentsRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            IncludeTrailingArguments = false,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new UseNamedArgumentsTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                "including trailing arguments",
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                "including trailing arguments",
                null,
                null)
            , Times.Once);
    }

    [Fact]
    public async Task GIVEN_UseNamedArgumentsRequestWithTrailingArguments_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithTrailingArgumentsTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new UseNamedArgumentsRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            IncludeTrailingArguments = true,
        };
        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new UseNamedArgumentsTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                null,
                null,
                null)
            , Times.Once);
    }
}
