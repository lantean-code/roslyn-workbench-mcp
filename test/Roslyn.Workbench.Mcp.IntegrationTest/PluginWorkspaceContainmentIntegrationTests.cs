using Roslyn.Workbench.Mcp.Workspace.Operations;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginWorkspaceContainmentIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_QueryPluginSuppressesAnalyzerAndMutatesLiveWorkspace_WHEN_InvocationCompletes_THEN_ShouldRequireReload()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var workspace = ComponentWorkspace.Create();
        var openResult = await workspace.OpenAsync(
            fixture.ProjectPath,
            TestContext.Current.CancellationToken);

        var request = new TestRequest();

        await using var lease = workspace.CreateQueryContext(
            request,
            TestContext.Current.CancellationToken);

        var handler = new MutatingQueryHandler();
        Assert.NotNull(lease.Context);
        var context = lease.Context;

        var pluginResult = await handler.ExecuteAsync(
            request,
            context,
            TestContext.Current.CancellationToken);

        var contextFactory = workspace.PluginContextFactory;
        var containmentFailure = contextFactory.DetectUnexpectedWorkspaceChange(context);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        pluginResult.IsSucceeded.Should().BeTrue();
        Assert.NotNull(containmentFailure);
        containmentFailure.Outcome.Should().Be(PluginExecutionOutcome.Conflict);
        containmentFailure.Error.Code.Should().Be("WorkspaceOutOfDate");
        containmentFailure.RequiredAction.Should().Be(RequiredAction.ReloadWorkspace);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_MutationPluginSuppressesAnalyzerAndMutatesLiveWorkspace_WHEN_InvocationCompletes_THEN_ShouldConflictTransaction()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var workspace = ComponentWorkspace.Create();
        var openResult = await workspace.OpenAsync(
            fixture.ProjectPath,
            TestContext.Current.CancellationToken);

        var startResult = await workspace.StartTransactionAsync(TestContext.Current.CancellationToken);
        var request = new TestRequest();

        await using var lease = workspace.CreateMutationContext(
            request,
            TestContext.Current.CancellationToken);

        var handler = new MutatingMutationHandler();
        Assert.NotNull(lease.Context);
        var context = lease.Context;

        var pluginResult = await handler.ExecuteAsync(
            request,
            context,
            TestContext.Current.CancellationToken);

        var contextFactory = workspace.PluginContextFactory;
        var containmentFailure = contextFactory.DetectUnexpectedWorkspaceChange(context);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        startResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        pluginResult.Outcome.Should().Be(PluginExecutionOutcome.NoChange);
        Assert.NotNull(containmentFailure);
        containmentFailure.Outcome.Should().Be(PluginExecutionOutcome.Conflict);
        containmentFailure.Error.Code.Should().Be("TransactionConflicted");
        containmentFailure.RequiredAction.Should().Be(RequiredAction.RollbackTransaction);
    }

    private sealed record TestRequest : WorkspaceBoundRequest;

    private sealed class TestResponse;

    private sealed class MutatingQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(
            TestRequest request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var solution = context.CurrentSolution;
            var projects = solution.Projects;
            var project = projects.First();
            var candidateDocument = project.AddDocument(
                "Injected.cs",
                "internal sealed class Injected;",
                filePath: "Injected.cs");

            var candidateSolution = candidateDocument.Project.Solution;

#pragma warning disable RWMCP001 // The integration test proves Host containment when a plugin suppresses the analyser.
            var wasApplied = solution.Workspace.TryApplyChanges(candidateSolution);
#pragma warning restore RWMCP001
            if (!wasApplied)
            {
                throw new InvalidOperationException("The test plugin could not mutate the live Roslyn Workspace.");
            }

            var response = new TestResponse();
            var result = PluginExecutionResult<TestResponse>.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutatingMutationHandler : IMutationToolHandler<TestRequest>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
            TestRequest request,
            IMutationContext context,
            CancellationToken cancellationToken)
        {
            var solution = context.CurrentSolution;
            var projects = solution.Projects;
            var project = projects.First();
            var candidateDocument = project.AddDocument(
                "Injected.cs",
                "internal sealed class Injected;",
                filePath: "Injected.cs");

            var candidateSolution = candidateDocument.Project.Solution;

#pragma warning disable RWMCP001 // The integration test proves Host containment when a plugin suppresses the analyser.
            var wasApplied = solution.Workspace.TryApplyChanges(candidateSolution);
#pragma warning restore RWMCP001
            if (!wasApplied)
            {
                throw new InvalidOperationException("The test plugin could not mutate the live Roslyn Workspace.");
            }

            var result = PluginExecutionResult<MutationCandidate>.NoChange();
            return ValueTask.FromResult(result);
        }
    }
}
