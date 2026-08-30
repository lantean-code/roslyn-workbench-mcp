namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginExecutionResultTests
{
    [Fact]
    public void GIVEN_NullData_WHEN_CreatingSuccessfulResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => PluginExecutionResult.Success<Response?>(data: null);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("data");
    }

    [Fact]
    public void GIVEN_NullError_WHEN_CreatingRejectedResult_THEN_ShouldRejectInvalidInvariant()
    {
        var action = () => PluginExecutionResult.Rejected<Response>(error: null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }

    [Fact]
    public void GIVEN_ConflictMetadata_WHEN_CreatingResult_THEN_ShouldPreserveFailureInvariant()
    {
        var diagnostics = new[] { new DiagnosticInfo { Id = "Id", Message = "Message" } };
        var warnings = new[] { new WarningInfo { Code = "Code", Message = "Message" } };
        var error = new PluginExecutionError
        {
            Code = "Conflict",
            Message = "Message",
        };

        var result = PluginExecutionResult.Conflict<Response>(
            error,
            RequiredAction.Retry,
            diagnostics,
            warnings);

        result.HasError.Should().BeTrue();
        result.IsSucceeded.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        result.Diagnostics.Should().BeSameAs(diagnostics);
        result.Warnings.Should().BeSameAs(warnings);
    }

    [Fact]
    public void GIVEN_FaultMetadata_WHEN_CreatingResult_THEN_ShouldPreserveFailureInvariant()
    {
        var diagnostics = new[] { new DiagnosticInfo { Id = "Id", Message = "Message" } };
        var warnings = new[] { new WarningInfo { Code = "Code", Message = "Message" } };
        var error = new PluginExecutionError
        {
            Code = "Faulted",
            Message = "Message",
        };

        var result = PluginExecutionResult.Faulted<Response>(
            error,
            RequiredAction.Retry,
            diagnostics,
            warnings);

        result.HasError.Should().BeTrue();
        result.IsSucceeded.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        result.Diagnostics.Should().BeSameAs(diagnostics);
        result.Warnings.Should().BeSameAs(warnings);
    }

#pragma warning disable CA1812 // The response fixture is consumed only as a generic result type argument.
    private sealed record Response
    {
    }
#pragma warning restore CA1812
}
