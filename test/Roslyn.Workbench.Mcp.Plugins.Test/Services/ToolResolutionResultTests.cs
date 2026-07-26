namespace Roslyn.Workbench.Mcp.Plugins.Test.Services;

public sealed class ToolResolutionResultTests
{
    [Fact]
    public void GIVEN_NullValue_WHEN_CreatingResolvedResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => ToolResolutionResult.Resolved<Response, Response>(value: null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("value");
    }

    [Fact]
    public void GIVEN_NullRejection_WHEN_CreatingRejectedResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => ToolResolutionResult.Rejected<Response, Response>(rejection: null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("rejection");
    }

#pragma warning disable CA1812 // The response fixture is consumed only as a generic result type argument.
    private sealed record Response
    {
    }
#pragma warning restore CA1812
}
