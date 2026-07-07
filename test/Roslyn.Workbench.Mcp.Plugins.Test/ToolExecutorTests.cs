using System.Text.Json;

using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolExecutorTests
{
    [Fact]
    public void GIVEN_ToolExecutionContextContract_WHEN_InspectingPublicProperties_THEN_ShouldExposeTransactionRevision()
    {
        typeof(IToolExecutionContext).GetProperty("TransactionRevision").Should().NotBeNull();
        typeof(IQueryContext).GetInterfaces().Should().Contain(static type => type == typeof(ICodeActionQueryWorkflow));
        typeof(IMutationContext).GetInterfaces().Should().Contain(static type => type == typeof(ICodeActionMutationWorkflow));
    }

    [Fact]
    public async Task GIVEN_QueryHandlerSuccess_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredSucceededResult()
    {
        var registeredTool = CreateQueryTool(new SuccessQueryHandler());
        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CreateExecutionContextFactory(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent.Should().NotBeNull();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("value").GetProperty("value").GetString().Should().Be("Value");
    }

    [Fact]
    public async Task GIVEN_QueryArgumentsWithWorkspaceSelector_WHEN_ExecutingRegisteredTool_THEN_ShouldPassDeserializedRequestToContextFactory()
    {
        var registeredTool = CreateQueryTool(new SuccessQueryHandler());
        var factory = new Mock<IToolExecutionContextFactory>();
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = "workspace-42",
            WorkspaceEpoch = 42,
            LoadedPath = "/workspace",
        };
        var queryContext = new Mock<IQueryContext>();
        var resolver = new Mock<IWorkspaceResolver>();

        queryContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        queryContext.SetupGet(static context => context.TransactionRevision).Returns(7);
        queryContext.SetupGet(static context => context.WorkspaceResolver).Returns(resolver.Object);
        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContext(
                It.Is<WorkspaceBoundRequest>(request => MatchesTestRequest(request, "Name", "workspace-42")),
                It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IQueryContext>.Acquired(queryContext.Object));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => throw new InvalidOperationException("Mutation context should not be requested."));

        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
            ["workspace"] = JsonSerializer.SerializeToElement(new WorkspaceSelector
            {
                WorkspaceId = "workspace-42",
            }),
        }, factory.Object, CancellationToken.None);

        result.IsError.Should().BeFalse();
        factory.Verify(static contextFactory => contextFactory.CreateQueryContext(
            It.Is<WorkspaceBoundRequest>(request => MatchesTestRequest(request, "Name", "workspace-42")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_QueryHandlerNoChange_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredNoChangeResult()
    {
        var registeredTool = CreateQueryTool(new NoChangeQueryHandler());
        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CreateExecutionContextFactory(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GIVEN_QueryHandlerRejected_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredRejectedResult()
    {
        var registeredTool = CreateQueryTool(new RejectedQueryHandler());
        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CreateExecutionContextFactory(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("Rejected");
    }

    [Fact]
    public async Task GIVEN_QueryHandlerConflict_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredConflictResult()
    {
        var registeredTool = CreateQueryTool(new ConflictQueryHandler());
        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CreateExecutionContextFactory(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("Conflict");
    }

    [Fact]
    public async Task GIVEN_QueryHandlerThrows_WHEN_ExecutingRegisteredTool_THEN_ShouldNormalizeFaultedResult()
    {
        var registeredTool = CreateQueryTool(new ThrowingQueryHandler());
        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CreateExecutionContextFactory(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("UnhandledException");
        result.StructuredContent.Value.GetProperty("error").GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GIVEN_HostRejectedContext_WHEN_ExecutingRegisteredTool_THEN_ShouldReturnStructuredRejectedResultWithoutInvokingPlugin()
    {
        var registeredTool = CreateQueryTool(new SuccessQueryHandler());
        var result = await registeredTool.Runtime.InvokeAsync(new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        }, CreateRejectedExecutionContextFactory(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeFalse();
        result.StructuredContent.Value.GetProperty("error").GetProperty("code").GetString().Should().Be("WorkspaceBusy");
        result.StructuredContent.Value.GetProperty("next").GetString().Should().Be("Retry");
    }

    private static RegisteredPluginTool CreateQueryTool(IQueryToolHandler<TestRequest, QueryResponse<TestResponse>> handler)
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

        return registry.RegisteredPluginTools.Single();
    }

    private sealed record TestRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private static bool MatchesTestRequest(WorkspaceBoundRequest request, string name, string workspaceId)
    {
        if (request is not TestRequest typedRequest)
        {
            return false;
        }

        return typedRequest.Name == name
            && typedRequest.Workspace is not null
            && typedRequest.Workspace.WorkspaceId == workspaceId;
    }

    private static IToolExecutionContextFactory CreateExecutionContextFactory()
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = "workspace-42",
            WorkspaceEpoch = 42,
            LoadedPath = "/workspace",
        };
        var queryContext = new Mock<IQueryContext>();
        var mutationContext = new Mock<IMutationContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var factory = new Mock<IToolExecutionContextFactory>();

        queryContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        queryContext.SetupGet(static context => context.TransactionRevision).Returns(7);
        queryContext.SetupGet(static context => context.WorkspaceResolver).Returns(resolver.Object);
        mutationContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        mutationContext.SetupGet(static context => context.TransactionRevision).Returns(7);
        mutationContext.SetupGet(static context => context.WorkspaceResolver).Returns(resolver.Object);
        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IQueryContext>.Acquired(queryContext.Object));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IMutationContext>.Acquired(mutationContext.Object));

        return factory.Object;
    }

    private static IToolExecutionContextFactory CreateRejectedExecutionContextFactory()
    {
        var factory = new Mock<IToolExecutionContextFactory>();

        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IQueryContext>.Rejected(new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = new ToolError
                {
                    Code = "WorkspaceBusy",
                    Message = "Workspace is busy.",
                },
                RequiredAction = RequiredAction.Retry,
            }));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IMutationContext>.Rejected(new PluginExecutionResultBox
            {
                Outcome = ToolOutcome.Rejected,
                Error = new ToolError
                {
                    Code = "WorkspaceBusy",
                    Message = "Workspace is busy.",
                },
                RequiredAction = RequiredAction.Retry,
            }));

        return factory.Object;
    }

    private sealed class SuccessQueryHandler : IQueryToolHandler<TestRequest, QueryResponse<TestResponse>>
    {
        public ValueTask<PluginExecutionResult<QueryResponse<TestResponse>>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<QueryResponse<TestResponse>>.Success(new QueryResponse<TestResponse>
            {
                Value = new TestResponse
                {
                    Value = "Value",
                },
            }));
        }
    }

    private sealed class NoChangeQueryHandler : IQueryToolHandler<TestRequest, QueryResponse<TestResponse>>
    {
        public ValueTask<PluginExecutionResult<QueryResponse<TestResponse>>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<QueryResponse<TestResponse>>.NoChange());
        }
    }

    private sealed class RejectedQueryHandler : IQueryToolHandler<TestRequest, QueryResponse<TestResponse>>
    {
        public ValueTask<PluginExecutionResult<QueryResponse<TestResponse>>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<QueryResponse<TestResponse>>.Rejected(new ToolError
            {
                Code = "Rejected",
                Message = "Rejected message.",
            }));
        }
    }

    private sealed class ConflictQueryHandler : IQueryToolHandler<TestRequest, QueryResponse<TestResponse>>
    {
        public ValueTask<PluginExecutionResult<QueryResponse<TestResponse>>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<QueryResponse<TestResponse>>.Conflict(new ToolError
            {
                Code = "Conflict",
                Message = "Conflict message.",
            }));
        }
    }

    private sealed class ThrowingQueryHandler : IQueryToolHandler<TestRequest, QueryResponse<TestResponse>>
    {
        public ValueTask<PluginExecutionResult<QueryResponse<TestResponse>>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            throw new InvalidOperationException("Boom");
        }
    }
}
