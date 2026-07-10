namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ConvertAnonymousTypeToClassToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        ConvertAnonymousTypeToClassTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<ConvertAnonymousTypeToClassRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "convert-anonymous-type-to-class"
                && metadata.Title == "Convert Anonymous Type To Class"
                && metadata.Description == "Converts a supported anonymous type to a generated class or record through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<ConvertAnonymousTypeToClassRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ClassKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithClassTitle()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertAnonymousTypeToClassRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Kind = ConvertAnonymousTypeToClassKind.Class,
        };
        var target = new ConvertAnonymousTypeToClassTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
                "Convert to class",
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
            "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            "Convert to class",
            null,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GIVEN_RecordKind_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithRecordTitle()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ConvertAnonymousTypeToClassRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Kind = ConvertAnonymousTypeToClassKind.Record,
        };
        var target = new ConvertAnonymousTypeToClassTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
                "Convert to record",
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
            "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            "Convert to record",
            null,
            null,
            null,
            null), Times.Once);
    }
}
