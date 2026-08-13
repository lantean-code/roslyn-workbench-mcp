namespace Roslyn.Workbench.Mcp.Plugins.Test.Registration;

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
            Services =
            [
                new PluginServiceDefinition
                {
                    ServiceType = typeof(IPluginService),
                    ImplementationType = typeof(PluginService),
                },
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

        var queryRegistration = result.Tools[0].Should().BeOfType<PluginQueryRegistration<Request, Response>>().Which;
        var mutationRegistration = result.Tools[1].Should().BeOfType<PluginMutationRegistration<Request>>().Which;
        var queryHandler = queryRegistration.Handler.Should().BeOfType<QueryHandler>().Which;
        var mutationHandler = mutationRegistration.Handler.Should().BeOfType<MutationHandler>().Which;
        queryHandler.PluginService.Should().BeSameAs(mutationHandler.PluginService);

        result.ServiceProviderLifetime.Should().NotBeNull();
        result.ServiceProviderLifetime!.Dispose();
        queryHandler.PluginService.IsDisposed.Should().BeTrue();

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
    public void GIVEN_HandlerConstructionAndCleanupThrow_WHEN_Materialising_THEN_ShouldPreserveBothFailures()
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<CleanupQueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
                CreatePreparedTool<ThrowingQueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
            ],
            Services =
            [
                new PluginServiceDefinition
                {
                    ServiceType = typeof(IThrowingDisposeService),
                    ImplementationType = typeof(ThrowingDisposeService),
                },
            ],
        };

        var action = () => _target.Materialize(preparation);

        var exception = action.Should().Throw<AggregateException>().Which;
        exception.InnerExceptions.Should().HaveCount(2);
        exception.InnerExceptions[0].Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("could not be constructed: Construction failed.");
        exception.InnerExceptions[1].Should().BeOfType<IOException>()
            .Which.Message.Should().Be("Cleanup failed.");
    }

    [Fact]
    public void GIVEN_HandlerDependencyIsMissing_WHEN_Materialising_THEN_ShouldReportDependencyFailure()
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<MissingDependencyQueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
            ],
        };

        var action = () => _target.Materialize(preparation);

        action.Should().Throw<AggregateException>().WithMessage("*IMissingService*");
    }

    [Fact]
    public void GIVEN_EquivalentPluginRegistrations_WHEN_MaterialisingSeparately_THEN_ShouldIsolateServiceProviders()
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<QueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
            ],
            Services =
            [
                new PluginServiceDefinition
                {
                    ServiceType = typeof(IPluginService),
                    ImplementationType = typeof(PluginService),
                },
            ],
        };

        var first = _target.Materialize(preparation);
        var second = _target.Materialize(preparation);
        var firstRegistration = first.Tools.Single().Should().BeOfType<PluginQueryRegistration<Request, Response>>().Which;
        var secondRegistration = second.Tools.Single().Should().BeOfType<PluginQueryRegistration<Request, Response>>().Which;
        var firstHandler = firstRegistration.Handler.Should().BeOfType<QueryHandler>().Which;
        var secondHandler = secondRegistration.Handler.Should().BeOfType<QueryHandler>().Which;

        firstHandler.PluginService.Should().NotBeSameAs(secondHandler.PluginService);

        first.ServiceProviderLifetime!.Dispose();
        firstHandler.PluginService.IsDisposed.Should().BeTrue();
        secondHandler.PluginService.IsDisposed.Should().BeFalse();

        second.ServiceProviderLifetime!.Dispose();
        secondHandler.PluginService.IsDisposed.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_AsyncDisposablePluginService_WHEN_DisposingProvider_THEN_ShouldDisposeService(
        bool disposeAsynchronously)
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<AsyncQueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
            ],
            Services =
            [
                new PluginServiceDefinition
                {
                    ServiceType = typeof(IAsyncPluginService),
                    ImplementationType = typeof(AsyncPluginService),
                },
            ],
        };

        var result = _target.Materialize(preparation);
        var registration = result.Tools.Single().Should().BeOfType<PluginQueryRegistration<Request, Response>>().Which;
        var handler = registration.Handler.Should().BeOfType<AsyncQueryHandler>().Which;
        var lifetime = result.ServiceProviderLifetime;
        lifetime.Should().NotBeNull();

        if (disposeAsynchronously)
        {
            var asyncLifetime = lifetime.Should().BeAssignableTo<IAsyncDisposable>().Which;
            await asyncLifetime.DisposeAsync();
        }
        else
        {
            lifetime!.Dispose();
        }

        handler.PluginService.IsDisposed.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_DualDisposablePluginService_WHEN_DisposingProvider_THEN_ShouldPreferAsyncDisposal(
        bool disposeAsynchronously)
    {
        var preparation = new PluginPreparationResult
        {
            Tools =
            [
                CreatePreparedTool<DualDisposableQueryHandler>(typeof(IQueryToolHandler<Request, Response>), ToolKind.Query),
            ],
            Services =
            [
                new PluginServiceDefinition
                {
                    ServiceType = typeof(IDualDisposablePluginService),
                    ImplementationType = typeof(DualDisposablePluginService),
                },
            ],
        };

        var result = _target.Materialize(preparation);
        var registration = result.Tools.Single().Should().BeOfType<PluginQueryRegistration<Request, Response>>().Which;
        var handler = registration.Handler.Should().BeOfType<DualDisposableQueryHandler>().Which;
        var lifetime = result.ServiceProviderLifetime;
        lifetime.Should().NotBeNull();

        if (disposeAsynchronously)
        {
            var asyncLifetime = lifetime.Should().BeAssignableTo<IAsyncDisposable>().Which;
            await asyncLifetime.DisposeAsync();
        }
        else
        {
            lifetime!.Dispose();
        }

        handler.PluginService.WasDisposedAsynchronously.Should().BeTrue();
        handler.PluginService.WasDisposedSynchronously.Should().BeFalse();
    }

    private static PreparedPluginTool CreatePreparedTool<THandler>(Type handlerContract, ToolKind kind)
        where THandler : class
    {
        return new PreparedPluginTool
        {
            HandlerType = typeof(THandler),
            HandlerContract = handlerContract,
            Tool = new RegisteredTool
            {
                Kind = kind,
                RequestType = typeof(Request),
                ResponseType = kind == ToolKind.Query ? typeof(Response) : typeof(MutationData),
            },
        };
    }

#pragma warning disable CA1812 // Request fixture is consumed as closed generic registration metadata.
    private sealed record Request : WorkspaceMutationRequest;
#pragma warning restore CA1812

    private sealed record Response : IQueryResponse;

#pragma warning disable CA1812 // Fixture services and handlers are constructed by DI from runtime Type metadata.

    private interface IPluginService
    {
        bool IsDisposed { get; }
    }

    private sealed class PluginService : IPluginService, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public PluginService()
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private interface IAsyncPluginService
    {
        bool IsDisposed { get; }
    }

    private sealed class AsyncPluginService : IAsyncPluginService, IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public AsyncPluginService()
        {
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private interface IDualDisposablePluginService
    {
        bool WasDisposedAsynchronously { get; }

        bool WasDisposedSynchronously { get; }
    }

    private sealed class DualDisposablePluginService : IDualDisposablePluginService, IDisposable, IAsyncDisposable
    {
        public bool WasDisposedAsynchronously { get; private set; }

        public bool WasDisposedSynchronously { get; private set; }

        public DualDisposablePluginService()
        {
        }

        public void Dispose()
        {
            WasDisposedSynchronously = true;
        }

        public ValueTask DisposeAsync()
        {
            WasDisposedAsynchronously = true;
            return ValueTask.CompletedTask;
        }
    }

    private interface IThrowingDisposeService
    {
    }

    private sealed class ThrowingDisposeService : IThrowingDisposeService, IDisposable
    {
        public ThrowingDisposeService()
        {
        }

        public void Dispose()
        {
            throw new IOException("Cleanup failed.");
        }
    }

    private sealed class QueryHandler : IQueryToolHandler<Request, Response>
    {
        public IPluginService PluginService { get; }

        public QueryHandler(IPluginService pluginService)
        {
            PluginService = pluginService;
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutationHandler : IMutationToolHandler<Request>
    {
        public IPluginService PluginService { get; }

        public MutationHandler(IPluginService pluginService)
        {
            PluginService = pluginService;
        }

        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
            Request request,
            IMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(PluginExecutionResult.NoChange<MutationCandidate>());
        }
    }

    private sealed class AsyncQueryHandler : IQueryToolHandler<Request, Response>
    {
        public IAsyncPluginService PluginService { get; }

        public AsyncQueryHandler(IAsyncPluginService pluginService)
        {
            PluginService = pluginService;
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DualDisposableQueryHandler : IQueryToolHandler<Request, Response>
    {
        public IDualDisposablePluginService PluginService { get; }

        public DualDisposableQueryHandler(IDualDisposablePluginService pluginService)
        {
            PluginService = pluginService;
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CleanupQueryHandler : IQueryToolHandler<Request, Response>
    {
        private readonly IThrowingDisposeService _service;

        public CleanupQueryHandler(IThrowingDisposeService service)
        {
            _service = service;
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            _ = _service;
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
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
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

    private interface IMissingService
    {
    }

    private sealed class MissingDependencyQueryHandler : IQueryToolHandler<Request, Response>
    {
        private readonly IMissingService _missingService;

        public MissingDependencyQueryHandler(IMissingService missingService)
        {
            _missingService = missingService;
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            _ = _missingService;
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }

#pragma warning restore CA1812
}
