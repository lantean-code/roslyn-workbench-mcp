namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class ReplaceMethodWithPropertyToolTests
{
    [Theory]
    [InlineData((int)ReplaceMethodWithPropertyKind.GetterOnly, 0)]
    [InlineData((int)ReplaceMethodWithPropertyKind.GetterAndSetter, 1)]
    public async Task GIVEN_ReplacementKind_WHEN_CallingExecuteAsync_THEN_ShouldStageSelectedMethodReplacement(
        int kindValue,
        int expectedActionIndex)
    {
        var kind = (ReplaceMethodWithPropertyKind)kindValue;
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new ReplaceMethodWithPropertyRequest
        {
            Method = new LocationSelector(),
            Kind = kind,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new ReplaceMethodWithPropertyTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Method,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.ReplaceMethodWithProperty.ReplaceMethodWithPropertyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>>(path => path.SequenceEqual(new[] { expectedActionIndex }))))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
                request.Method,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.ReplaceMethodWithProperty.ReplaceMethodWithPropertyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>>(path => path.SequenceEqual(new[] { expectedActionIndex })))
            , Times.Once);
    }
}
