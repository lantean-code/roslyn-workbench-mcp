namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddImportToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<ICodeActionToolRegistry>();

        AddImportTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<AddImportRequest>(
            It.Is<CodeActionToolMetadata>(metadata =>
                metadata.Name == "add-import"
                && metadata.Title == "Add Import"
                && metadata.Description == "Adds a supported using directive through Roslyn refactoring composition."
                && metadata.Behavior.Destructive),
            It.IsAny<ICodeActionMutationToolHandler<AddImportRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AddImportRequestWithoutSimplifyAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithoutSimplifyAllOccurrencesTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddImportRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            SimplifyAllOccurrences = false,
        };
        var target = new AddImportTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
                "simplify all occurrences",
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
                "simplify all occurrences",
                null,
                null)
            , Times.Once);
    }

    [Fact]
    public async Task GIVEN_AddImportRequestWithSimplifyAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageReplaySelectionWithSimplifyAllOccurrencesTitle()
    {
        var expected = CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddImportRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            SimplifyAllOccurrences = true,
        };
        var target = new AddImportTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
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
                "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
                null,
                "Add 'using ",
                null,
                null,
                null)
            , Times.Once);
    }
}
