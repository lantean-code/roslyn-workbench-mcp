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

        target.Services.AddSingleton<IPluginService, PluginService>();
        target.Services.AddSingleton<PluginService>();

        queryBuilder.Should().BeOfType<QueryToolConfigurationBuilder>();
        mutationBuilder.Should().BeOfType<MutationToolConfigurationBuilder>();
        target.Definitions.Select(static definition => definition.HandlerType).Should().Equal(typeof(QueryHandler), typeof(MutationHandler));
        target.Definitions.Select(static definition => definition.Kind).Should().Equal(ToolKind.Query, ToolKind.Mutation);
        target.ServiceDefinitions.Select(static definition => definition.ServiceType).Should().Equal(typeof(IPluginService), typeof(PluginService));
        target.ServiceDefinitions.Select(static definition => definition.ImplementationType).Should().OnlyContain(static type => type == typeof(PluginService));
    }

    [Fact]
    public void GIVEN_FrozenConfiguration_WHEN_MutatingConfigurationOrBuilders_THEN_ShouldRejectEveryMutation()
    {
        var target = new PluginConfiguration();
        var queryBuilder = target.AddQueryTool<QueryHandler>();
        var mutationBuilder = target.AddMutationTool<MutationHandler>();
        target.Services.AddSingleton<IPluginService, PluginService>();
        target.Freeze();

        var addQuery = () => target.AddQueryTool<QueryHandler>();
        var addMutation = () => target.AddMutationTool<MutationHandler>();
        var changeQuery = () => queryBuilder.WithName("query");
        var changeMutation = () => mutationBuilder.IsDestructive();
        var addService = () => target.Services.AddSingleton<PluginService>();

        addQuery.Should().Throw<InvalidOperationException>();
        addMutation.Should().Throw<InvalidOperationException>();
        changeQuery.Should().Throw<InvalidOperationException>();
        changeMutation.Should().Throw<InvalidOperationException>();
        addService.Should().Throw<InvalidOperationException>();
    }

#pragma warning disable CA1812 // These fixtures are consumed through closed generic registration metadata.
    private sealed record Request : WorkspaceMutationRequest;

    private sealed record Response : IQueryResponse;

    private interface IPluginService
    {
    }

    private sealed class PluginService : IPluginService
    {
    }

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutationHandler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.NoChange<MutationCandidate>());
        }
    }
#pragma warning restore CA1812
}
