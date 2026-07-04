namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class ToolResponseShaperTests
{
    [Fact]
    public void GIVEN_IncompatibleSingletonPayload_WHEN_ShapingResponse_THEN_ShouldThrowInvalidOperationException()
    {
        var descriptor = new ToolResponseDescriptor
        {
            Kind = ToolResponseShapeKind.Singleton,
        };
        var result = new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Succeeded,
            Data = "Value",
        };

        var action = () => ToolResponseShaper.Shape(descriptor, typeof(TestResponse), result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Published response data type mismatch. Expected 'Roslyn.Workbench.Mcp.Plugins.Test.ToolResponseShaperTests+TestResponse' but got 'System.String'.");
    }

    [Fact]
    public void GIVEN_IncompatibleMutationPayload_WHEN_ShapingResponse_THEN_ShouldThrowInvalidOperationException()
    {
        var descriptor = new ToolResponseDescriptor
        {
            Kind = ToolResponseShapeKind.Mutation,
        };
        var result = new PluginExecutionResultBox
        {
            Outcome = ToolOutcome.Succeeded,
            Data = new TestResponse
            {
                Value = "Value",
            },
        };

        var action = () => ToolResponseShaper.Shape(descriptor, typeof(MutationData), result);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Published response data type mismatch. Expected 'Roslyn.Workbench.Mcp.Contracts.Results.MutationData' but got 'Roslyn.Workbench.Mcp.Plugins.Test.ToolResponseShaperTests+TestResponse'.");
    }

    private sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
