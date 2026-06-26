using System.Text.Json;
using AwesomeAssertions;
using ModelContextProtocol.Protocol;
using Moq;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Xunit;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolExecutorTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GIVEN_ToolExecutionContextContract_WHEN_InspectingPublicProperties_THEN_ShouldExposeTransactionRevision()
    {
        typeof(IToolExecutionContext).GetProperty("TransactionRevision").Should().NotBeNull();
        typeof(IToolExecutionContext).GetProperty("CodeActionService").Should().NotBeNull();
    }

    [Fact]
    public async Task GIVEN_QueryHandlerSuccess_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredSucceededResult()
    {
        var registeredTool = CreateQueryTool(new SuccessQueryHandler());
        var executor = new ToolExecutor(CreateExecutionContextFactory());

        var result = await executor.ExecuteAsync(registeredTool, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent.Should().NotBeNull();

        var payload = JsonSerializer.Deserialize<ToolResult<TestResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        payload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        payload.Data!.Value.Should().Be("Value");
        payload.WorkspaceEpoch.Should().Be(42);
        payload.TransactionRevision.Should().Be(7);
    }

    [Fact]
    public async Task GIVEN_QueryHandlerNoChange_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredNoChangeResult()
    {
        var registeredTool = CreateQueryTool(new NoChangeQueryHandler());
        var executor = new ToolExecutor(CreateExecutionContextFactory());

        var result = await executor.ExecuteAsync(registeredTool, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<TestResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeFalse();
        payload!.Outcome.Should().Be(ToolOutcome.NoChange);
    }

    [Fact]
    public async Task GIVEN_QueryHandlerRejected_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredRejectedResult()
    {
        var registeredTool = CreateQueryTool(new RejectedQueryHandler());
        var executor = new ToolExecutor(CreateExecutionContextFactory());

        var result = await executor.ExecuteAsync(registeredTool, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<TestResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Rejected);
        payload.Error!.Code.Should().Be("Rejected");
    }

    [Fact]
    public async Task GIVEN_QueryHandlerConflict_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredConflictResult()
    {
        var registeredTool = CreateQueryTool(new ConflictQueryHandler());
        var executor = new ToolExecutor(CreateExecutionContextFactory());

        var result = await executor.ExecuteAsync(registeredTool, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<TestResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Conflict);
        payload.Error!.Code.Should().Be("Conflict");
    }

    [Fact]
    public async Task GIVEN_QueryHandlerThrows_WHEN_ExecutingRegisteredTool_THEN_ShouldNormalizeFaultedResult()
    {
        var registeredTool = CreateQueryTool(new ThrowingQueryHandler());
        var executor = new ToolExecutor(CreateExecutionContextFactory());

        var result = await executor.ExecuteAsync(registeredTool, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<TestResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Faulted);
        payload.Error!.Code.Should().Be("UnhandledException");
        payload.Error.CorrelationId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GIVEN_HostRejectedContext_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredRejectedResultWithoutInvokingPlugin()
    {
        var registeredTool = CreateQueryTool(new SuccessQueryHandler());
        var executor = new ToolExecutor(CreateRejectedExecutionContextFactory());

        var result = await executor.ExecuteAsync(registeredTool, new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<TestResponse>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeTrue();
        payload!.Outcome.Should().Be(ToolOutcome.Rejected);
        payload.Error!.Code.Should().Be("WorkspaceBusy");
    }

    private static RegisteredTool CreateQueryTool(IQueryToolHandler<TestRequest, TestResponse> handler)
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var registry = new PluginRegistry(metadata);

        registry.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "test-query",
                Title = "Test Query",
                Description = "Query description.",
            },
            handler);

        return registry.RegisteredTools.Single();
    }

    private sealed record TestRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private static IToolExecutionContextFactory CreateExecutionContextFactory()
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceEpoch = 42,
            LoadedPath = "/workspace",
        };
        var queryContext = new Mock<IQueryContext>();
        var mutationContext = new Mock<IMutationContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var factory = new Mock<IToolExecutionContextFactory>();

        queryContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        queryContext.SetupGet(static context => context.TransactionRevision).Returns(7);
        queryContext.SetupGet(static context => context.Resolver).Returns(resolver.Object);
        mutationContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        mutationContext.SetupGet(static context => context.TransactionRevision).Returns(7);
        mutationContext.SetupGet(static context => context.Resolver).Returns(resolver.Object);
        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContextAsync(It.IsAny<RegisteredTool>(), It.IsAny<CancellationToken>()))
            .Returns((RegisteredTool _, CancellationToken _) => ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Acquired(queryContext.Object)));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContextAsync(It.IsAny<RegisteredTool>(), It.IsAny<CancellationToken>()))
            .Returns((RegisteredTool _, CancellationToken _) => ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Acquired(mutationContext.Object)));

        return factory.Object;
    }

    private static IToolExecutionContextFactory CreateRejectedExecutionContextFactory()
    {
        var factory = new Mock<IToolExecutionContextFactory>();

        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContextAsync(It.IsAny<RegisteredTool>(), It.IsAny<CancellationToken>()))
            .Returns((RegisteredTool _, CancellationToken _) => ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Rejected(new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = new ToolError
                {
                    Code = "WorkspaceBusy",
                    Message = "Workspace is busy.",
                },
                RequiredAction = RequiredAction.Retry,
            })));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContextAsync(It.IsAny<RegisteredTool>(), It.IsAny<CancellationToken>()))
            .Returns((RegisteredTool _, CancellationToken _) => ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Rejected(new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = new ToolError
                {
                    Code = "WorkspaceBusy",
                    Message = "Workspace is busy.",
                },
                RequiredAction = RequiredAction.Retry,
            })));

        return factory.Object;
    }

    private sealed class SuccessQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<TestResponse>.Success(new TestResponse { Value = "Value" }));
        }
    }

    private sealed class NoChangeQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<TestResponse>.NoChange());
        }
    }

    private sealed class RejectedQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<TestResponse>.Rejected(new ToolError
            {
                Code = "Rejected",
                Message = "Rejected message.",
            }));
        }
    }

    private sealed class ConflictQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<TestResponse>.Conflict(new ToolError
            {
                Code = "Conflict",
                Message = "Conflict message.",
            }));
        }
    }

    private sealed class ThrowingQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            throw new InvalidOperationException("Boom");
        }
    }
}
