namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddDebuggerDisplayToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        AddDebuggerDisplayTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<LocationRefactoringRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "add-debugger-display"
                && metadata.Title == "Add Debugger Display"
                && metadata.Description == "Adds a DebuggerDisplay attribute through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<CodeActionMutationToolHandler<LocationRefactoringRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LocationRefactoringRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithDebuggerDisplayProvider()
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
        var target = new AddDebuggerDisplayTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider",
                "Add 'DebuggerDisplay' attribute",
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
            "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider",
            "Add 'DebuggerDisplay' attribute",
            null,
            null,
            null,
            null), Times.Once);
    }
}
