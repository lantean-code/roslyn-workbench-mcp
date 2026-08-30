namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Results;

public sealed class CodeActionExecutionResultTests
{
    [Fact]
    public void GIVEN_FaultMetadata_WHEN_CreatingResult_THEN_ShouldPreserveFailureInvariant()
    {
        var diagnostics = new[] { new DiagnosticInfo { Id = "Id", Message = "Message" } };
        var warnings = new[] { new WarningInfo { Code = "Code", Message = "Message" } };
        var error = new CodeActionExecutionError
        {
            Code = "Faulted",
            Message = "Message",
        };

        var result = CodeActionExecutionResult.Faulted<Response>(
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
