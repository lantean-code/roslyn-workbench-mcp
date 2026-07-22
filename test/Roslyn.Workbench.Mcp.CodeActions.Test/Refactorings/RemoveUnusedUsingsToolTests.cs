namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class RemoveUnusedUsingsToolTests
{
    [Fact]
    public async Task GIVEN_RemoveUnusedUsingsRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageScopedCodeFixForUnusedUsings()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new RemoveUnusedUsingsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var scopedFixStager = new Mock<IScopedCodeFixStager>();
        var target = new RemoveUnusedUsingsTool(scopedFixStager.Object);

        scopedFixStager
            .Setup(item => item.StageScopedCodeFixAsync(
                It.Is<ScopedCodeFixRequest>(stageRequest =>
                    stageRequest.Scope == request.Scope
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.DiagnosticIds.Count == 1
                    && stageRequest.DiagnosticIds[0] == "RemoveUnnecessaryImportsFixable"
                    && stageRequest.Title == "Remove unnecessary usings"
                    && stageRequest.SyntheticDiagnosticId == "RemoveUnnecessaryImportsFixable"),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        scopedFixStager.Verify(item => item.StageScopedCodeFixAsync(
            It.Is<ScopedCodeFixRequest>(stageRequest =>
                stageRequest.Scope == request.Scope
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.DiagnosticIds.Count == 1
                && stageRequest.DiagnosticIds[0] == "RemoveUnnecessaryImportsFixable"
                && stageRequest.Title == "Remove unnecessary usings"
                && stageRequest.SyntheticDiagnosticId == "RemoveUnnecessaryImportsFixable"),
            context.Object,
            CancellationToken.None), Times.Once);
    }
}
