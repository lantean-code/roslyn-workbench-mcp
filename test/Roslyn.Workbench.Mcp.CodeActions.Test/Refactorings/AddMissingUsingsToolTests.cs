namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddMissingUsingsToolTests
{
    [Fact]
    public async Task GIVEN_PreferGlobalUsingsIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnsupportedOption()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddMissingUsingsRequest
        {
            PreferGlobalUsings = true,
        };

        var scopedFixStager = new Mock<IScopedCodeFixStager>();
        var target = new AddMissingUsingsTool(scopedFixStager.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("UnsupportedOption");
        scopedFixStager.Verify(item => item.StageScopedCodeFixAsync(
            It.IsAny<ScopedCodeFixRequest>(),
            context.Object,
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreferGlobalUsingsIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldStageScopedCodeFix()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddMissingUsingsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            PreferGlobalUsings = false,
        };

        var scopedFixStager = new Mock<IScopedCodeFixStager>();
        var target = new AddMissingUsingsTool(scopedFixStager.Object);

        scopedFixStager
            .Setup(item => item.StageScopedCodeFixAsync(
                It.Is<ScopedCodeFixRequest>(stageRequest =>
                    stageRequest.Scope == request.Scope
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider"
                    && stageRequest.DiagnosticIds.Count == 2
                    && stageRequest.DiagnosticIds[0] == "CS0103"
                    && stageRequest.DiagnosticIds[1] == "CS0246"),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        scopedFixStager.Verify(item => item.StageScopedCodeFixAsync(
            It.Is<ScopedCodeFixRequest>(stageRequest =>
                stageRequest.Scope == request.Scope
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider"
                && stageRequest.DiagnosticIds.Count == 2
                && stageRequest.DiagnosticIds[0] == "CS0103"
                && stageRequest.DiagnosticIds[1] == "CS0246"),
            context.Object,
            CancellationToken.None), Times.Once);
    }
}
