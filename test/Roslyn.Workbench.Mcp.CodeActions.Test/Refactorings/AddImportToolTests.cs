namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddImportToolTests
{
    [Fact]
    public async Task GIVEN_AddImportRequestWithoutSimplifyAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithoutSimplifyAllOccurrencesTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddImportRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            SimplifyAllOccurrences = false,
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new AddImportTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
                "simplify all occurrences",
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
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
                "simplify all occurrences",
                null,
                null)
            , Times.Once);
    }

    [Fact]
    public async Task GIVEN_AddImportRequestWithSimplifyAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithSimplifyAllOccurrencesTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddImportRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            SimplifyAllOccurrences = true,
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new AddImportTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
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
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
                null,
                null,
                null)
            , Times.Once);
    }
}
