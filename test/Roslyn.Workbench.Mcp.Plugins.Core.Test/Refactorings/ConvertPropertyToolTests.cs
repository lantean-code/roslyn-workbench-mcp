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
        var request = new ConvertPropertyRequest
        {
            Selection = new LocationSelector(),
            Direction = ConvertPropertyDirection.ToAutoWhenSafe,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var context = new MutationContextBuilder()
            .WithStageLocationCodeFixAsync((stageRequest, cancellationToken) =>
            {
                stageRequest.Location.Should().Be(request.Selection);
                stageRequest.ExpectedSnapshot.Should().Be(request.ExpectedSnapshot);
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider");
                stageRequest.AnalyzerTypeName.Should().Be("Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyAnalyzer");
                stageRequest.SyntheticDiagnosticId.Should().Be("IDE0032");
                stageRequest.DiagnosticIds.Should().ContainSingle().Which.Should().Be("IDE0032");
                stageRequest.Title.Should().Be("Use auto property");
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new ConvertPropertyTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
