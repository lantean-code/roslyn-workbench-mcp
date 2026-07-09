namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertAutoPropertyToFullPropertyToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        ConvertAutoPropertyToFullPropertyTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<ConvertAutoPropertyToFullPropertyRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "convert-auto-property-to-full-property"
                && metadata.Title == "Convert Auto Property To Full Property"
                && metadata.Description == "Converts a supported auto-property to a full property through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<ConvertAutoPropertyToFullPropertyRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ConvertAutoPropertyToFullPropertyRequest_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithFullPropertyProvider()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertAutoPropertyToFullPropertyRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertAutoPropertyToFullPropertyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
                "Convert to full property",
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
                "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
                "Convert to full property",
                null,
                null,
                null,
                null)
            , Times.Once);
    }
}
