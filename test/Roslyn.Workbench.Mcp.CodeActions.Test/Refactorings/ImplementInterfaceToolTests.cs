namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ImplementInterfaceToolTests
{
    [Fact]
    public async Task GIVEN_LocationRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageImplementInterfaceAction()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ImplementInterfaceTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.ImplementInterface.ImplementInterfaceCodeRefactoringProvider",
                "Implement interface",
                null,
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
                "Microsoft.CodeAnalysis.ImplementInterface.ImplementInterfaceCodeRefactoringProvider",
                "Implement interface",
                null,
                null,
                null,
                null)
            , Times.Once);
    }
}
