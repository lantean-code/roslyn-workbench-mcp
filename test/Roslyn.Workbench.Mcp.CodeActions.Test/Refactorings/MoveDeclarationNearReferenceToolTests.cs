namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class MoveDeclarationNearReferenceToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        MoveDeclarationNearReferenceTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<LocationRefactoringRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "move-declaration-near-reference"
                && metadata.Title == "Move Declaration Near Reference"
                && metadata.Description == "Moves a supported local declaration nearer to its first use through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<LocationRefactoringRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LocationRefactoringRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithMoveDeclarationProvider()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new MoveDeclarationNearReferenceTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider",
                null,
                "Move declaration near reference",
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
                "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider",
                null,
                "Move declaration near reference",
                null,
                null,
                null)
            , Times.Once);
    }
}
