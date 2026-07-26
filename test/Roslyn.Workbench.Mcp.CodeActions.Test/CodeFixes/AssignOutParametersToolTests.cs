namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeFixes;

public sealed class AssignOutParametersToolTests
{
    private const string _atStartProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAtStartCodeFixProvider";
    private const string _aboveReturnProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAboveReturnCodeFixProvider";

    [Fact]
    public async Task GIVEN_AtStartProviderSucceeds_WHEN_CallingExecuteAsync_THEN_ShouldReturnItsCandidate()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = CreateRequest();
        var locationFixStager = new Mock<ILocationCodeFixStager>();
        var target = new AssignOutParametersTool(locationFixStager.Object);

        locationFixStager
            .Setup(item => item.StageLocationCodeFixAsync(
                IsStageRequest(request, _atStartProviderId),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            IsStageRequest(request, _atStartProviderId),
            context.Object,
            CancellationToken.None), Times.Once);

        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            IsStageRequest(request, _aboveReturnProviderId),
            context.Object,
            CancellationToken.None), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AtStartProviderIsUnavailable_WHEN_CallingExecuteAsync_THEN_ShouldTryAboveReturnProvider()
    {
        var unavailable = CreateRejection("CodeFixUnavailable");
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = CreateRequest();
        var locationFixStager = new Mock<ILocationCodeFixStager>();
        var target = new AssignOutParametersTool(locationFixStager.Object);

        locationFixStager
            .Setup(item => item.StageLocationCodeFixAsync(
                IsStageRequest(request, _atStartProviderId),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(unavailable);

        locationFixStager
            .Setup(item => item.StageLocationCodeFixAsync(
                IsStageRequest(request, _aboveReturnProviderId),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            IsStageRequest(request, _atStartProviderId),
            context.Object,
            CancellationToken.None), Times.Once);

        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            IsStageRequest(request, _aboveReturnProviderId),
            context.Object,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AtStartProviderReturnsDifferentRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnThatRejection()
    {
        var expected = CreateRejection("InvalidRequest");
        var context = new Mock<ICodeActionMutationContext>();
        var request = CreateRequest();
        var locationFixStager = new Mock<ILocationCodeFixStager>();
        var target = new AssignOutParametersTool(locationFixStager.Object);

        locationFixStager
            .Setup(item => item.StageLocationCodeFixAsync(
                IsStageRequest(request, _atStartProviderId),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            IsStageRequest(request, _atStartProviderId),
            context.Object,
            CancellationToken.None), Times.Once);

        locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
            IsStageRequest(request, _aboveReturnProviderId),
            context.Object,
            CancellationToken.None), Times.Never);
    }

    private static FixedCompilerCodeFixRequest CreateRequest()
    {
        return new FixedCompilerCodeFixRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
    }

    private static CodeActionExecutionResult<WorkspaceMutationCandidate> CreateRejection(string code)
    {
        var error = new CodeActionExecutionError
        {
            Code = code,
            Message = code,
        };

        return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error);
    }

    private static LocationCodeFixRequest IsStageRequest(
        FixedCompilerCodeFixRequest request,
        string providerId)
    {
        return It.Is<LocationCodeFixRequest>(stageRequest =>
            stageRequest.Location == request.Location
            && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
            && stageRequest.ProviderId == providerId
            && stageRequest.DiagnosticIds.Count == 1
            && stageRequest.DiagnosticIds[0] == "CS0177");
    }
}
