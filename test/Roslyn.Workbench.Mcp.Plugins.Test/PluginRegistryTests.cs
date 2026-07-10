using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginRegistryTests
{
    [Fact]
    public void GIVEN_QueryToolRegistration_WHEN_BuildingRegistry_THEN_ShouldCaptureValidatedRegisteredTool()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var target = new PluginRegistry(metadata);

        target.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "test-query",
                Title = "Test Query",
                Description = "Query description.",
            },
            new TestQueryHandler());

        var tool = target.RegisteredTools.Should().ContainSingle().Subject;

        tool.Plugin.PluginId.Should().Be("plugin.test");
        tool.Metadata.Name.Should().Be("test-query");
        tool.Kind.Should().Be(ToolKind.Query);
        tool.RequestType.Should().Be(typeof(TestRequest));
        tool.ResponseType.Should().Be(typeof(TestResponse));
    }

    [Fact]
    public void GIVEN_DuplicateToolName_WHEN_RegisteringSecondTool_THEN_ShouldThrowInvalidOperationException()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var target = new PluginRegistry(metadata);
        var registration = new ToolRegistrationMetadata
        {
            Name = "test-query",
            Title = "Test Query",
            Description = "Query description.",
        };

        target.RegisterQueryTool(registration, new TestQueryHandler());

        var action = () => target.RegisterQueryTool(registration, new TestQueryHandler());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_UnsupportedPluginApiVersion_WHEN_CreatingRegistry_THEN_ShouldThrowInvalidOperationException()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = "9.9",
        };

        var action = () => _ = new PluginRegistry(metadata);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_InterfaceResponseType_WHEN_RegisteringTool_THEN_ShouldThrowInvalidOperationException()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var target = new PluginRegistry(metadata);

        var action = () => target.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "test-invalid-response",
                Title = "Test Invalid Response",
                Description = "Invalid response description.",
            },
            new InvalidResponseHandler());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_MutationToolRegistration_WHEN_BuildingRegistry_THEN_ShouldCaptureTypedMutationTool()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var target = new PluginRegistry(metadata);

        target.RegisterMutationTool(
            new ToolRegistrationMetadata
            {
                Name = "test-mutation",
                Title = "Test Mutation",
                Description = "Mutation description.",
            },
            new TestMutationHandler());

        var tool = target.RegisteredTools.Should().ContainSingle().Subject;

        tool.Kind.Should().Be(ToolKind.Mutation);
        tool.RequestType.Should().Be(typeof(TestRequest));
        tool.ResponseType.Should().Be(typeof(MutationProposal));
    }

    [Fact]
    public void GIVEN_QueryRegistration_WHEN_AcceptingVisitor_THEN_ShouldDispatchWithClosedGenericTypes()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "plugin.test",
            DisplayName = "Plugin Test",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };

        var target = new PluginRegistry(metadata);

        target.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "test-query",
                Title = "Test Query",
                Description = "Query description.",
            },
            new TestQueryHandler());

        var registration = target.GetRegisteredPluginTool("test-query");
        var visitor = new Mock<IPluginToolRegistrationVisitor<bool>>();
        visitor
            .Setup(targetVisitor => targetVisitor.VisitQuery(It.IsAny<PluginQueryRegistration<TestRequest, TestResponse>>()))
            .Returns(true);

        var result = registration.Accept(visitor.Object);

        result.Should().BeTrue();
        visitor.Verify(
            targetVisitor => targetVisitor.VisitQuery(It.Is<PluginQueryRegistration<TestRequest, TestResponse>>(
                candidate => ReferenceEquals(candidate, registration))),
            Times.Once);
    }

    public sealed record TestRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class TestQueryHandler : IQueryToolHandler<TestRequest, TestResponse>
    {
        public ValueTask<PluginExecutionResult<TestResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<TestResponse>.Success(new TestResponse
            {
                Value = "Value",
            }));
        }
    }

    private interface IInvalidResponse
    {
    }

    private sealed class TestMutationHandler : IMutationToolHandler<TestRequest>
    {
        public ValueTask<PluginExecutionResult<MutationProposal>> ExecuteAsync(TestRequest request, IMutationContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<MutationProposal>.Success(new MutationProposal
            {
                Summary = "Summary",
            }));
        }
    }

    private sealed class InvalidResponseHandler : IQueryToolHandler<TestRequest, IInvalidResponse>
    {
        public ValueTask<PluginExecutionResult<IInvalidResponse>> ExecuteAsync(TestRequest request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<IInvalidResponse>.NoChange());
        }
    }
}
