namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Results;

public sealed class CodeActionExecutionOutcomeExtensionsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void GIVEN_ExecutionOutcome_WHEN_CheckingForError_THEN_ShouldReturnExpectedResult(
        int outcomeValue,
        bool expected)
    {
        var outcome = (CodeActionExecutionOutcome)outcomeValue;

        var result = outcome.IsError();

        result.Should().Be(expected);
    }
}
