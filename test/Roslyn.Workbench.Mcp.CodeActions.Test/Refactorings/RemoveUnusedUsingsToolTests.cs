namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class RemoveUnusedUsingsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        RemoveUnusedUsingsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<RemoveUnusedUsingsRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "remove-unused-usings"
                && metadata.Title == "Remove Unused Usings"
                && metadata.Description == "Removes unused using directives across a selected scope through Roslyn code-fix composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<RemoveUnusedUsingsRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RemoveUnusedUsingsRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageScopedCodeFixForUnusedUsings()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
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
        var target = new RemoveUnusedUsingsTool();

        context
            .Setup(item => item.StageScopedCodeFixAsync(
                It.Is<ScopedCodeFixRequest>(stageRequest =>
                    stageRequest.Scope == request.Scope
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.DiagnosticIds.Count == 1
                    && stageRequest.DiagnosticIds[0] == "RemoveUnnecessaryImportsFixable"
                    && stageRequest.Title == "Remove unnecessary usings"
                    && stageRequest.SyntheticDiagnosticId == "RemoveUnnecessaryImportsFixable"),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageScopedCodeFixAsync(
            It.Is<ScopedCodeFixRequest>(stageRequest =>
                stageRequest.Scope == request.Scope
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.DiagnosticIds.Count == 1
                && stageRequest.DiagnosticIds[0] == "RemoveUnnecessaryImportsFixable"
                && stageRequest.Title == "Remove unnecessary usings"
                && stageRequest.SyntheticDiagnosticId == "RemoveUnnecessaryImportsFixable"),
            CancellationToken.None), Times.Once);
    }
}
