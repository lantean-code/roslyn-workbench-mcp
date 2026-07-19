using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpToolProtocolFactoryTests
{
    private readonly Mock<IMcpSdkSchemaProvider> _schemaProvider;
    private readonly McpToolProtocolFactory _target;

    public McpToolProtocolFactoryTests()
    {
        _schemaProvider = new Mock<IMcpSdkSchemaProvider>();
        _schemaProvider.SetReturnsDefault(CreateSchema("object"));
        _target = new McpToolProtocolFactory(new ToolSchemaFactory(_schemaProvider.Object));
    }

    [Fact]
    public void GIVEN_ServerOwnedToolWithoutOutputSchema_WHEN_CreatingProtocol_THEN_ShouldPublishMetadataAndOmitOutputSchema()
    {
        var result = _target.CreateServerOwnedTool<TestRequest, TestResponse>(
            "test-tool",
            "Test Tool",
            "Description",
            true,
            false,
            null,
            ToolOutputSchemaMode.Omit);

        result.Name.Should().Be("test-tool");
        result.Title.Should().Be("Test Tool");
        result.Description.Should().Be("Description");
        result.OutputSchema.Should().BeNull();
        result.Annotations!.Title.Should().Be("Test Tool");
        result.Annotations.ReadOnlyHint.Should().BeTrue();
        result.Annotations.IdempotentHint.Should().BeTrue();
        result.Annotations.OpenWorldHint.Should().BeFalse();
        result.Annotations.DestructiveHint.Should().BeFalse();
        _schemaProvider.Verify(item => item.GetInputSchema<TestRequest>(), Times.Once);
    }

    [Fact]
    public void GIVEN_ServerOwnedToolWithFullOutputSchema_WHEN_CreatingProtocol_THEN_ShouldPublishDirectOutputSchema()
    {
        var result = _target.CreateServerOwnedTool<TestRequest, TestResponse>(
            "test-tool",
            "Test Tool",
            "Description",
            false,
            true,
            "Summary",
            ToolOutputSchemaMode.Full);

        result.Description.Should().Be("Description Result: Summary");
        result.OutputSchema.Should().NotBeNull();
        result.Annotations!.ReadOnlyHint.Should().BeFalse();
        result.Annotations.IdempotentHint.Should().BeFalse();
        result.Annotations.DestructiveHint.Should().BeTrue();
#pragma warning disable CA2263 // The protocol factory selects this overload from runtime response metadata.
        _schemaProvider.Verify(item => item.GetValueSchema(typeof(TestResponse)), Times.Once);
#pragma warning restore CA2263
    }

    [Fact]
    public void GIVEN_PluginQueryWithFullSchemaAndResultSummary_WHEN_CreatingProtocol_THEN_ShouldPublishReadOnlyMetadata()
    {
        var tool = new RegisteredTool
        {
            Metadata = new ToolRegistrationMetadata
            {
                Name = "test-query",
                Title = "Test Query",
                Description = "Description",
                ResultSummary = "Summary",
            },
            Kind = ToolKind.Query,
            ResponseType = typeof(TestResponse),
        };

        var result = _target.CreatePluginTool<TestRequest>(tool, ToolOutputSchemaMode.Full);

        result.Description.Should().Be("Description Result: Summary");
        result.OutputSchema.Should().NotBeNull();
        result.Annotations!.ReadOnlyHint.Should().BeTrue();
        result.Annotations.IdempotentHint.Should().BeTrue();
        result.Annotations.DestructiveHint.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_NonDestructivePluginMutation_WHEN_CreatingProtocol_THEN_ShouldPublishMutationMetadata()
    {
        var tool = new RegisteredTool
        {
            Metadata = new ToolRegistrationMetadata
            {
                Name = "test-mutation",
                Title = "Test Mutation",
                Description = "Description",
            },
            Kind = ToolKind.Mutation,
            ResponseType = typeof(MutationData),
        };

        var result = _target.CreatePluginTool<TestRequest>(tool, ToolOutputSchemaMode.Full);

        result.OutputSchema.Should().NotBeNull();
        result.Annotations!.ReadOnlyHint.Should().BeFalse();
        result.Annotations.IdempotentHint.Should().BeFalse();
        result.Annotations.DestructiveHint.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DestructiveCodeActionMutation_WHEN_CreatingProtocol_THEN_ShouldPublishMutationMetadata()
    {
        var metadata = new CodeActionToolMetadata
        {
            Name = "test-mutation",
            Title = "Test Mutation",
            Description = "Description",
            Behavior = new CodeActionToolBehavior
            {
                Destructive = true,
            },
        };

        var result = _target.CreateCodeActionTool<TestRequest>(
            metadata,
            CodeActionToolKind.Mutation,
            typeof(MutationData),
            ToolOutputSchemaMode.Omit);

        result.Description.Should().Be("Description");
        result.OutputSchema.Should().BeNull();
        result.Annotations!.ReadOnlyHint.Should().BeFalse();
        result.Annotations.IdempotentHint.Should().BeFalse();
        result.Annotations.DestructiveHint.Should().BeTrue();
        result.Annotations.OpenWorldHint.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_CodeActionQuery_WHEN_CreatingProtocol_THEN_ShouldPublishQueryOutputSchema()
    {
        var metadata = new CodeActionToolMetadata
        {
            Name = "test-query",
            Title = "Test Query",
            Description = "Description",
        };

        var result = _target.CreateCodeActionTool<TestRequest>(
            metadata,
            CodeActionToolKind.Query,
            typeof(TestResponse),
            ToolOutputSchemaMode.Full);

        result.OutputSchema.Should().NotBeNull();
        result.Annotations!.ReadOnlyHint.Should().BeTrue();
    }

    private static JsonElement CreateSchema(string type)
    {
        return JsonSerializer.SerializeToElement(new
        {
            type,
        });
    }

#pragma warning disable CA1812 // Protocol fixtures are consumed through schema metadata without construction.
    private sealed record TestRequest : WorkspaceBoundRequest
    {
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1812
}
