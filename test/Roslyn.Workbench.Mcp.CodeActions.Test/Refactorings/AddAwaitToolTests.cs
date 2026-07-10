namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddAwaitToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        AddAwaitTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<AddAwaitRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "add-await"
                && metadata.Title == "Add Await"
                && metadata.Description == "Stages one supported add-await refactoring through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<CodeActionMutationToolHandler<AddAwaitRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AwaitKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithAwaitActionPath()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
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
        var target = new AddAwaitTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
                "Add 'await'",
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 0)))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
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
        var expected = CodeActionExecutionResult<WorkspaceMutationProposal>.Success(new WorkspaceMutationProposal());
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
        var target = new AddAwaitTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
                "Add 'await' and 'ConfigureAwait(false)'",
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 1)))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            "Add 'await' and 'ConfigureAwait(false)'",
            null,
            null,
            null,
            It.Is<IReadOnlyList<int>?>(path => path != null && path.Count == 1 && path[0] == 1)), Times.Once);
    }
}
