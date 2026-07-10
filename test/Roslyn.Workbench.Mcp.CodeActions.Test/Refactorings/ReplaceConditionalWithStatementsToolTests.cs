namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ReplaceConditionalWithStatementsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        ReplaceConditionalWithStatementsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<LocationRefactoringRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "replace-conditional-with-statements"
                && metadata.Title == "Replace Conditional With Statements"
                && metadata.Description == "Rewrites a supported conditional expression into statements through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<CodeActionMutationToolHandler<LocationRefactoringRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LocationRefactoringRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithConditionalStatementsProvider()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ReplaceConditionalWithStatementsTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider",
                "Replace conditional expression with statements",
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider",
                "Replace conditional expression with statements",
                null,
                null,
                null,
                null)
            , Times.Once);
    }
}
