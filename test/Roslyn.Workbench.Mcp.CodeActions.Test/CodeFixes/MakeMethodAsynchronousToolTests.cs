namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeFixes;

public sealed class MakeMethodAsynchronousToolTests
{
    private static readonly IReadOnlyList<string> _expectedDiagnosticIds = ["CS0246", "CS4032", "CS4033", "CS4034"];

    [Theory]
    [InlineData((int)MakeMethodAsynchronousStrategy.ReturnTask, "Make method async")]
    [InlineData((int)MakeMethodAsynchronousStrategy.StayVoid, "Make method async (stay void)")]
    public async Task GIVEN_AsynchronousMethodStrategy_WHEN_CallingExecuteAsync_THEN_ShouldStageMatchingAction(
        int strategyValue,
        string expectedTitle)
    {
        var strategy = (MakeMethodAsynchronousStrategy)strategyValue;
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new MakeMethodAsynchronousRequest
        {
            Location = new LocationSelector(),
            Strategy = strategy,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        var locationFixStager = new Mock<ILocationCodeFixStager>();
        locationFixStager
            .Setup(item => item.StageLocationCodeFixAsync(
                It.IsAny<LocationCodeFixRequest>(),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var target = new MakeMethodAsynchronousTool(locationFixStager.Object);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            It.Is<LocationCodeFixRequest>(stageRequest =>
                stageRequest.Location == request.Location
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider"
                && stageRequest.DiagnosticIds.SequenceEqual(_expectedDiagnosticIds)
                && stageRequest.Title == expectedTitle),
            context.Object,
            CancellationToken.None), Times.Once);
    }
}
