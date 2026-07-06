namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class ConvertPropertyToolTests
{
    [Fact]
    public async Task GIVEN_UnsupportedDirection_WHEN_CallingExecute_THEN_ShouldReturnInvalidRequest()
    {
        var target = new ConvertPropertyTool();
        var context = new MutationContextBuilder().Build();

        var result = await target.ExecuteAsync(new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            Direction = (ConvertPropertyDirection)999,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ToFullDirection_WHEN_CallingExecute_THEN_ShouldDelegateToReplayExecutor()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var replayExecutor = new Mock<IReplayCodeActionExecutor>();
        var services = new ToolExecutionServicesBuilder()
            .WithReplayCodeActionExecutor(replayExecutor.Object)
            .Build();
        var target = new ConvertPropertyTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(services)
            .Build();
        var request = new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            Direction = ConvertPropertyDirection.ToFull,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        replayExecutor
            .Setup(executor => executor.StageReplaySelectionAsync(
                It.IsAny<LocationSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IMutationContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<int>?>()))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayExecutor.Verify(executor => executor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
            "Convert to full property",
            null,
            null,
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ToAutoWhenSafeDirection_WHEN_CallingExecute_THEN_ShouldDelegateToLocationCodeFixService()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var codeActionService = new Mock<ICodeActionService>();
        var context = new MutationContextBuilder()
            .WithCodeActionService(codeActionService.Object)
            .Build();
        var target = new ConvertPropertyTool();
        var request = new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            Direction = ConvertPropertyDirection.ToAutoWhenSafe,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        codeActionService
            .Setup(service => service.StageLocationCodeFixAsync(
                It.IsAny<LocationCodeFixRequest>(),
                It.IsAny<IMutationContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        codeActionService.Verify(service => service.StageLocationCodeFixAsync(
            It.Is<LocationCodeFixRequest>(stageRequest =>
                stageRequest.Location == request.Selection
                && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                && stageRequest.ProviderId == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider"
                && stageRequest.AnalyzerTypeName == "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer"
                && stageRequest.SyntheticDiagnosticId == "IDE0032"
                && stageRequest.DiagnosticIds.Count == 1
                && stageRequest.DiagnosticIds[0] == "IDE0032"
                && stageRequest.Title == "Use auto property"),
            context,
            CancellationToken.None), Times.Once);
    }
}
