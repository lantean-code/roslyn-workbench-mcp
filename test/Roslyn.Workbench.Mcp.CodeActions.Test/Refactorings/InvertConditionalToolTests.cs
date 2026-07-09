namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class InvertConditionalToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        InvertConditionalTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<LocationRefactoringRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "invert-conditional"
                && metadata.Title == "Invert Conditional"
                && metadata.Description == "Inverts a supported conditional expression through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<LocationRefactoringRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_LocationRefactoringRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithInvertConditionalProvider()
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
        var target = new InvertConditionalTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider",
                "Invert conditional",
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
                "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider",
                "Invert conditional",
                null,
                null,
                null,
                null)
            , Times.Once);
    }
}
