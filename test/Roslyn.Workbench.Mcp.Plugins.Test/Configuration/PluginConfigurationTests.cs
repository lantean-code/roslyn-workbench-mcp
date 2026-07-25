namespace Roslyn.Workbench.Mcp.Plugins.Test.Configuration;

public sealed class PluginConfigurationTests
{
    [Fact]
    public void GIVEN_MutableConfiguration_WHEN_AddingAndCustomisingTools_THEN_ShouldReturnConcreteChainableBuilders()
    {
        var target = new PluginConfiguration();

        var queryBuilder = target.AddQueryTool<QueryHandler>()
            .WithName("query")
            .WithTitle("Query")
            .WithDescription("Query description")
            .WithResultSummary("Query result");

        var mutationBuilder = target.AddMutationTool<MutationHandler>()
            .WithName("mutation")
            .WithTitle("Mutation")
            .WithDescription("Mutation description")
            .WithResultSummary("Mutation result")
            .IsDestructive();

        queryBuilder.Should().BeOfType<QueryToolConfigurationBuilder>();
        mutationBuilder.Should().BeOfType<MutationToolConfigurationBuilder>();
        target.Definitions.Select(static definition => definition.HandlerType).Should().Equal(typeof(QueryHandler), typeof(MutationHandler));
        target.Definitions.Select(static definition => definition.Kind).Should().Equal(ToolKind.Query, ToolKind.Mutation);
        target.Definitions.Select(static definition => definition.HandlerFactory().GetType()).Should().Equal(typeof(QueryHandler), typeof(MutationHandler));
    }

    [Fact]
    public void GIVEN_FrozenConfiguration_WHEN_MutatingConfigurationOrBuilders_THEN_ShouldRejectEveryMutation()
    {
        var target = new PluginConfiguration();
        var queryBuilder = target.AddQueryTool<QueryHandler>();
        var mutationBuilder = target.AddMutationTool<MutationHandler>();
        target.Freeze();

        var addQuery = () => target.AddQueryTool<QueryHandler>();
        var addMutation = () => target.AddMutationTool<MutationHandler>();
        var changeQuery = () => queryBuilder.WithName("query");
        var changeMutation = () => mutationBuilder.IsDestructive();

        addQuery.Should().Throw<InvalidOperationException>();
        addMutation.Should().Throw<InvalidOperationException>();
        changeQuery.Should().Throw<InvalidOperationException>();
        changeMutation.Should().Throw<InvalidOperationException>();
    }

#pragma warning disable CA1812 // Request fixture is consumed as closed generic registration metadata.
    private sealed record Request : WorkspaceMutationRequest;
#pragma warning restore CA1812

    private sealed record Response;

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult<Response>.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutationHandler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<MutationCandidate>.NoChange());
        }
    }
}
