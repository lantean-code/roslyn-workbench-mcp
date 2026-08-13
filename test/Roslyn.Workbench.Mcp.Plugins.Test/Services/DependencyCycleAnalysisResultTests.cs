namespace Roslyn.Workbench.Mcp.Plugins.Test.Services;

public sealed class DependencyCycleAnalysisResultTests
{
    [Fact]
    public void GIVEN_CompletedAnalysis_WHEN_CreatingResult_THEN_ShouldExposeCompleteState()
    {
        var cycles = new[] { new DependencyCycle { Nodes = [] } };

        var result = DependencyCycleAnalysisResult.Completed(cycles, 2);

        result.Status.Should().Be(DependencyCycleAnalysisStatus.Completed);
        result.IsCompleted.Should().BeTrue();
        result.Cycles.Should().BeSameAs(cycles);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void GIVEN_SelectedCountExceedsTotal_WHEN_CreatingCompletedResult_THEN_ShouldRejectInvalidState()
    {
        var cycles = new[] { new DependencyCycle { Nodes = [] } };

        var action = () => DependencyCycleAnalysisResult.Completed(cycles, 0);

        action.Should().Throw<ArgumentException>().WithParameterName("cycles");
    }

    [Fact]
    public void GIVEN_NullCycles_WHEN_CreatingCompletedResult_THEN_ShouldRejectInvalidState()
    {
        var action = () => DependencyCycleAnalysisResult.Completed(null!, 0);

        action.Should().Throw<ArgumentNullException>().WithParameterName("cycles");
    }

    [Fact]
    public void GIVEN_NegativeTotal_WHEN_CreatingCompletedResult_THEN_ShouldRejectInvalidState()
    {
        var action = () => DependencyCycleAnalysisResult.Completed([], -1);

        action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("totalCount");
    }

    [Theory]
    [InlineData(DependencyCycleAnalysisStatus.NodeLimitExceeded)]
    [InlineData(DependencyCycleAnalysisStatus.EdgeLimitExceeded)]
    public void GIVEN_LimitExceeded_WHEN_CreatingResult_THEN_ShouldNotExposePartialAnalysis(DependencyCycleAnalysisStatus status)
    {
        var result = status == DependencyCycleAnalysisStatus.NodeLimitExceeded
            ? DependencyCycleAnalysisResult.NodeLimitExceeded()
            : DependencyCycleAnalysisResult.EdgeLimitExceeded();

        result.Status.Should().Be(status);
        result.IsCompleted.Should().BeFalse();
        result.Cycles.Should().BeNull();
        result.TotalCount.Should().BeNull();
    }
}
