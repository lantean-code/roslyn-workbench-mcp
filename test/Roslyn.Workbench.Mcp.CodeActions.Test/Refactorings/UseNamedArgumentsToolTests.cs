namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class UseNamedArgumentsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        UseNamedArgumentsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<UseNamedArgumentsRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "use-named-arguments"
                && metadata.Title == "Use Named Arguments"
                && metadata.Description == "Adds a supported argument name through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<UseNamedArgumentsRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UseNamedArgumentsRequestWithoutTrailingArguments_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithoutTrailingArgumentsTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new UseNamedArgumentsRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            IncludeTrailingArguments = false,
        };
        var target = new UseNamedArgumentsTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                "including trailing arguments",
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                "including trailing arguments",
                null,
                null)
            , Times.Once);
    }

    [Fact]
    public async Task GIVEN_UseNamedArgumentsRequestWithTrailingArguments_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithTrailingArgumentsTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new UseNamedArgumentsRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            IncludeTrailingArguments = true,
        };
        var target = new UseNamedArgumentsTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
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
                "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
                null,
                "Add argument name '",
                null,
                null,
                null)
            , Times.Once);
    }
}
