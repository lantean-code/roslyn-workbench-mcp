namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginToolRegistrationMaterializerTests
{
    private readonly PluginToolRegistrationMaterializer _target;

    public PluginToolRegistrationMaterializerTests()
    {
        _target = new PluginToolRegistrationMaterializer();
    }

    [Fact]
    public void GIVEN_PreparedQueryAndMutation_WHEN_MaterialisingAndVisiting_THEN_ShouldDispatchClosedGenericRegistrations()
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<QueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
                CreatePreparedTool<MutationHandler>(typeof(IMutationToolHandler<Request>), ToolKind.Mutation),
            ],
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "PluginHandlerState",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "Warning",
                },
            ],
        };
        var visitor = new Mock<IPluginToolRegistrationVisitor<string>>();
        visitor.Setup(static value => value.VisitQuery(It.IsAny<PluginQueryRegistration<Request, Response>>())).Returns("query");
        visitor.Setup(static value => value.VisitMutation(It.IsAny<PluginMutationRegistration<Request>>())).Returns("mutation");

        var result = _target.Materialize(preparation);
        var dispatch = result.Tools.Select(tool => tool.Accept(visitor.Object)).ToArray();

        dispatch.Should().Equal("query", "mutation");
        result.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Id == "PluginHandlerState"
            && diagnostic.Message == "Warning");
        visitor.Verify(static value => value.VisitQuery(It.IsAny<PluginQueryRegistration<Request, Response>>()), Times.Once);
        visitor.Verify(static value => value.VisitMutation(It.IsAny<PluginMutationRegistration<Request>>()), Times.Once);
    }

    [Fact]
    public void GIVEN_HandlerConstructionThrows_WHEN_Materialising_THEN_ShouldReportConstructionFailure()
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<ThrowingQueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
            ],
        };

        var action = () => _target.Materialize(preparation);

        action.Should().Throw<InvalidOperationException>().WithMessage("*could not be constructed: Construction failed.*");
    }

    [Fact]
    public void GIVEN_HandlerFactoryThrows_WHEN_Materialising_THEN_ShouldReportFactoryFailure()
    {
        var preparedTool = CreatePreparedTool<QueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query) with
        {
            HandlerFactory = static () => throw new InvalidOperationException("Factory failed."),
        };
        var preparation = new PluginPreparationResult
        {
            Tools = [preparedTool],
        };

        var action = () => _target.Materialize(preparation);

        action.Should().Throw<InvalidOperationException>().WithMessage("*could not be constructed: Factory failed.*");
    }

    private static PreparedPluginTool CreatePreparedTool<THandler>(Type handlerContract, ToolKind kind)
        where THandler : class, new()
    {
        return new PreparedPluginTool
        {
            HandlerType = typeof(THandler),
            HandlerContract = handlerContract,
            HandlerFactory = static () => new THandler(),
            Tool = new RegisteredTool
            {
                Kind = kind,
                RequestType = typeof(Request),
                ResponseType = kind == ToolKind.Query ? typeof(Response) : typeof(MutationData),
            },
        };
    }

#pragma warning disable CA1812 // Request fixture is consumed as closed generic registration metadata.
    private sealed record Request : WorkspaceBoundRequest;
#pragma warning restore CA1812

    private sealed record Response;

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }

    private sealed class MutationHandler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
            Request request,
            IMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<MutationCandidate>.NoChange());
        }
    }

    private sealed class ThrowingQueryHandler : IQueryToolHandler<Request, Response>
    {
        public ThrowingQueryHandler()
        {
            throw new InvalidOperationException("Construction failed.");
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response()));
        }
    }
}
