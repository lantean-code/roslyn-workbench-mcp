namespace Roslyn.Workbench.Mcp.Plugins.Test;

public sealed class PluginWorkspaceResultMapperTests
{
    [Theory]
    [InlineData(1, PluginExecutionOutcome.Rejected)]
    [InlineData(2, PluginExecutionOutcome.Conflict)]
    [InlineData(3, PluginExecutionOutcome.Faulted)]
    public void GIVEN_WorkspaceFailure_WHEN_MappingFailure_THEN_ShouldMapOutcomeAndDetails(
        int statusValue,
        PluginExecutionOutcome expectedOutcome)
    {
        var status = (WorkspaceOperationStatus)statusValue;
        var failure = new WorkspaceExecutionFailure
        {
            Status = status,
            Error = CreateError(),
        };

        var result = PluginWorkspaceResultMapper.MapFailure(failure);

        result!.Outcome.Should().Be(expectedOutcome);
        result.Error.Code.Should().Be("Code");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
    }

    [Fact]
    public void GIVEN_NoWorkspaceFailure_WHEN_MappingFailure_THEN_ShouldReturnNull()
    {
        PluginWorkspaceResultMapper.MapFailure(null).Should().BeNull();
    }

    [Theory]
    [InlineData(0, PluginExecutionOutcome.Succeeded)]
    [InlineData(4, PluginExecutionOutcome.NoChange)]
    [InlineData(1, PluginExecutionOutcome.Rejected)]
    [InlineData(2, PluginExecutionOutcome.Conflict)]
    [InlineData(3, PluginExecutionOutcome.Faulted)]
    public void GIVEN_WorkspaceMutationResult_WHEN_Mapping_THEN_ShouldMapEverySupportedOutcome(
        int statusValue,
        PluginExecutionOutcome expectedOutcome)
    {
        var status = (WorkspaceOperationStatus)statusValue;
        var result = PluginWorkspaceResultMapper.MapMutation(CreateMutationResult(status));

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
        var action = () => PluginWorkspaceResultMapper.MapMutation(
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
