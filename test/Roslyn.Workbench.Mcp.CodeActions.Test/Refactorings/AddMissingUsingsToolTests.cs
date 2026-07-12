namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddMissingUsingsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        AddMissingUsingsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<AddMissingUsingsRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "add-missing-usings"
                && metadata.Title == "Add Missing Usings"
                && metadata.Description == "Adds missing using directives across a selected scope through Roslyn code-fix composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<AddMissingUsingsRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PreferGlobalUsingsIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnsupportedOption()
    {
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddMissingUsingsRequest
        {
            PreferGlobalUsings = true,
        };
        var target = new AddMissingUsingsTool();

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("UnsupportedOption");
        context.Verify(item => item.StageScopedCodeFixAsync(
            It.IsAny<ScopedCodeFixRequest>(),
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
        var target = new AddMissingUsingsTool();

        context
            .Setup(item => item.StageScopedCodeFixAsync(
                It.Is<ScopedCodeFixRequest>(stageRequest =>
                    stageRequest.Scope == request.Scope
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider"
                    && stageRequest.DiagnosticIds.Count == 2
                    && stageRequest.DiagnosticIds[0] == "CS0103"
                    && stageRequest.DiagnosticIds[1] == "CS0246"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageScopedCodeFixAsync(
            It.Is<ScopedCodeFixRequest>(stageRequest =>
                stageRequest.Scope == request.Scope
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider"
                && stageRequest.DiagnosticIds.Count == 2
                && stageRequest.DiagnosticIds[0] == "CS0103"
                && stageRequest.DiagnosticIds[1] == "CS0246"),
            CancellationToken.None), Times.Once);
    }
}
