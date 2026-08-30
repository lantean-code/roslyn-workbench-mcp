namespace Roslyn.Workbench.Mcp.Workspace.Test.ExecutionContexts;

public sealed class WorkspaceMutationStagerTests
{
    [Fact]
    public async Task GIVEN_MutationCandidate_WHEN_Staging_THEN_ShouldDelegateAllArguments()
    {
        using var workspace = new AdhocWorkspace();
        var stagingService = new Mock<IMutationStagingService>();
        var proposal = new WorkspaceMutationCandidate
        {
            CandidateSolution = workspace.CurrentSolution,
            Summary = "Summary",
        };

        var diagnostic = new DiagnosticInfo { Id = "Id", Message = "Message" };
        var warning = new WarningInfo { Code = "Code", Message = "Message" };
        var expected = WorkspaceOperationResult.NoChange<MutationStagingOutcome>();
        stagingService.Setup(item => item.StageAsync(
            "OperationName",
            proposal,
            It.Is<IReadOnlyList<DiagnosticInfo>>(items => items.SequenceEqual(new[] { diagnostic })),
            It.Is<IReadOnlyList<WarningInfo>>(items => items.SequenceEqual(new[] { warning })),
            TestContext.Current.CancellationToken)).ReturnsAsync(expected);

        var target = new WorkspaceMutationStager(stagingService.Object);

        var result = await target.StageAsync(
            "OperationName",
            proposal,
            [diagnostic],
            [warning],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }
}
