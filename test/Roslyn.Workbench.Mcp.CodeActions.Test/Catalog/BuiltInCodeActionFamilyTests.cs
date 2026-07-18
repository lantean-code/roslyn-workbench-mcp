namespace Roslyn.Workbench.Mcp.CodeActions.Test.Catalog;

public sealed class BuiltInCodeActionFamilyTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, false)]
    public void GIVEN_SupportState_WHEN_ReadingExecutionProperties_THEN_ShouldMapVisibilityAndExecutionMode(
        int stateValue,
        int expectedModeValue,
        bool expectedVisible)
    {
        var target = new BuiltInCodeActionFamily
        {
            State = (BuiltInCodeActionSupportState)stateValue,
        };

        target.ExecutionMode.Should().Be((CodeActionExecutionMode)expectedModeValue);
        target.IsVisible.Should().Be(expectedVisible);
    }

    [Theory]
    [InlineData("ToolName", 0, true)]
    [InlineData("ToolName", 1, true)]
    [InlineData("ToolName", 2, false)]
    [InlineData("", 0, false)]
    [InlineData(" ", 1, false)]
    [InlineData(null, 0, false)]
    public void GIVEN_ToolNameAndSupportState_WHEN_CheckingDedicatedVisibility_THEN_ShouldReturnExpectedResult(
        string? toolName,
        int stateValue,
        bool expected)
    {
        var target = new BuiltInCodeActionFamily
        {
            ToolName = toolName,
            State = (BuiltInCodeActionSupportState)stateValue,
        };

        target.IsDedicatedToolVisible.Should().Be(expected);
    }
}
