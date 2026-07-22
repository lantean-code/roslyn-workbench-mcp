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

        result.Outcome.Should().Be(expectedOutcome);
        result.Error.Code.Should().Be("Code");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
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
            result.Data.Summary.Should().Be("Summary");
            result.Data.Transaction.Should().NotBeNull();
            result.Data.Preview.Should().NotBeNull();
        }
        else if (status is WorkspaceOperationStatus.Rejected or WorkspaceOperationStatus.Conflict or WorkspaceOperationStatus.Faulted)
        {
            result.Error!.Code.Should().Be("Code");
        }
    }

    private static WorkspaceOperationResult<MutationStagingOutcome> CreateMutationResult(WorkspaceOperationStatus status)
    {
        if (status == WorkspaceOperationStatus.Succeeded)
        {
            var data = new MutationStagingOutcome
            {
                Operation = "Operation",
                Summary = "Summary",
                Transaction = new TransactionInfo(),
                Preview = new MutationPreview(),
            };

            return WorkspaceOperationResult<MutationStagingOutcome>.Succeeded(data);
        }

        if (status == WorkspaceOperationStatus.NoChange)
        {
            return WorkspaceOperationResult<MutationStagingOutcome>.NoChange();
        }

        var error = CreateError();

        return status switch
        {
            WorkspaceOperationStatus.Rejected => WorkspaceOperationResult<MutationStagingOutcome>.Rejected(error),
            WorkspaceOperationStatus.Conflict => WorkspaceOperationResult<MutationStagingOutcome>.Conflict(error),
            WorkspaceOperationStatus.Faulted => WorkspaceOperationResult<MutationStagingOutcome>.Faulted(error),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "A supported workspace status is required."),
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
