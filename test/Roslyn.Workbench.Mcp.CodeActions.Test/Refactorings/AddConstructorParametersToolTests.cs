namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddConstructorParametersToolTests
{
    [Theory]
    [InlineData((int)AddConstructorParametersKind.Required, 0)]
    [InlineData((int)AddConstructorParametersKind.Optional, 1)]
    public async Task GIVEN_ParameterKind_WHEN_CallingExecuteAsync_THEN_ShouldStageSelectedConstructorAction(
        int kindValue,
        int expectedActionIndex)
    {
        var kind = (AddConstructorParametersKind)kindValue;
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new AddConstructorParametersRequest
        {
            Members = new LocationSelector(),
            Kind = kind,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        var selectionStager = new Mock<ICodeActionSelectionStager>();
        var target = new AddConstructorParametersTool(selectionStager.Object);

        selectionStager
            .Setup(item => item.StageSelectionAsync(
                request.Members,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.AddConstructorParametersFromMembers.AddConstructorParametersFromMembersCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>>(path => path.SequenceEqual(new[] { expectedActionIndex }))))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        selectionStager.Verify(item => item.StageSelectionAsync(
                request.Members,
                request.ExpectedSnapshot,
                CancellationToken.None,
                context.Object,
                "Microsoft.CodeAnalysis.AddConstructorParametersFromMembers.AddConstructorParametersFromMembersCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                It.Is<IReadOnlyList<int>>(path => path.SequenceEqual(new[] { expectedActionIndex })))
            , Times.Once);
    }
}
