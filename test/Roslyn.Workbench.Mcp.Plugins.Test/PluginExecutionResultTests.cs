namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginExecutionResultTests
{
    [Fact]
    public void GIVEN_ConflictMetadata_WHEN_CreatingResult_THEN_ShouldPreserveFailureInvariant()
    {
        var diagnostics = new[] { new DiagnosticInfo() };
        var warnings = new[] { new WarningInfo() };
        var error = new PluginExecutionError
        {
            Code = "Conflict",
            Message = "Message",
        };

        var result = PluginExecutionResult<Response>.Conflict(
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
        var diagnostics = new[] { new DiagnosticInfo() };
        var warnings = new[] { new WarningInfo() };
        var error = new PluginExecutionError
        {
            Code = "Faulted",
            Message = "Message",
        };

        var result = PluginExecutionResult<Response>.Faulted(
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
