namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionWorkspaceResultMapperTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void GIVEN_WorkspaceFailure_WHEN_MappingFailure_THEN_ShouldMapOutcomeAndDetails(
        int statusValue,
        int expectedOutcomeValue)
    {
        var status = (WorkspaceOperationStatus)statusValue;
        var expectedOutcome = (CodeActionExecutionOutcome)expectedOutcomeValue;
        var failure = new WorkspaceExecutionFailure
        {
            Status = status,
            Error = CreateError(),
        };

        var result = CodeActionWorkspaceResultMapper.MapFailure(failure);

        result!.Outcome.Should().Be(expectedOutcome);
        result.Error.Code.Should().Be("Code");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
    }

    [Fact]
    public void GIVEN_NoWorkspaceFailure_WHEN_MappingFailure_THEN_ShouldReturnNull()
    {
        CodeActionWorkspaceResultMapper.MapFailure(null).Should().BeNull();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void GIVEN_WorkspaceMutationResult_WHEN_Mapping_THEN_ShouldMapEverySupportedOutcome(
        int statusValue,
        int expectedOutcomeValue)
    {
        var status = (WorkspaceOperationStatus)statusValue;
        var expectedOutcome = (CodeActionExecutionOutcome)expectedOutcomeValue;
        var result = CodeActionWorkspaceResultMapper.MapMutation(CreateMutationResult(status));

        result.Outcome.Should().Be(expectedOutcome);
        if (status == WorkspaceOperationStatus.Succeeded)
        {
            result.Data!.Operation.Should().Be("Operation");
        }
        else if (status is WorkspaceOperationStatus.Rejected or WorkspaceOperationStatus.Conflict or WorkspaceOperationStatus.Faulted)
        {
            result.Error!.Code.Should().Be("Code");
        }
    }

    [Fact]
    public void GIVEN_UnsupportedWorkspaceStatus_WHEN_MappingMutation_THEN_ShouldThrowInvalidOperationException()
    {
        var action = () => CodeActionWorkspaceResultMapper.MapMutation(
            CreateMutationResult((WorkspaceOperationStatus)999));

        action.Should().Throw<InvalidOperationException>();
    }

    private static WorkspaceOperationResult<MutationStagingOutcome> CreateMutationResult(WorkspaceOperationStatus status)
    {
        return new WorkspaceOperationResult<MutationStagingOutcome>
        {
            Status = status,
            Data = status == WorkspaceOperationStatus.Succeeded
                ? new MutationStagingOutcome
                {
                    Operation = "Operation",
                    Summary = "Summary",
                }
                : null,
            Error = status is WorkspaceOperationStatus.Rejected or WorkspaceOperationStatus.Conflict or WorkspaceOperationStatus.Faulted
                ? CreateError()
                : null,
        };
    }

    private static WorkspaceOperationError CreateError()
    {
        return new WorkspaceOperationError
        {
            Code = "Code",
            Message = "Message",
            RequiredAction = RequiredAction.Retry,
        };
    }
}
