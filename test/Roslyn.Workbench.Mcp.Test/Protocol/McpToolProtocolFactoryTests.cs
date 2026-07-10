namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class McpToolProtocolFactoryTests
{
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

        var result = McpToolProtocolFactory.CreatePluginTool<TestRequest>(tool, ToolOutputSchemaMode.Full);

        result.Description.Should().Be("Description Result: Summary");
        result.OutputSchema.Should().NotBeNull();
        result.Annotations!.ReadOnlyHint.Should().BeTrue();
        result.Annotations.IdempotentHint.Should().BeTrue();
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

        var result = McpToolProtocolFactory.CreateCodeActionTool<TestRequest>(
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

    public sealed record TestRequest : WorkspaceBoundRequest
    {
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
