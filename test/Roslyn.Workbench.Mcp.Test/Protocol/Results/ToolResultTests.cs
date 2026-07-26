using System.Text.Json;
using Roslyn.Workbench.Mcp.Protocol.Validation;

namespace Roslyn.Workbench.Mcp.Test.Protocol.Results;

public sealed class ToolResultTests
{
    [Fact]
    public void GIVEN_SucceededResult_WHEN_SerializedAndDeserialized_THEN_ShouldRoundTripCoreState()
    {
        var result = ToolResult.Succeeded(
            new WorkspaceStatusData
            {
                State = WorkspaceLifecycleState.Ready,
                ProjectCount = 1,
                DocumentCount = 2,
                ReloadRequired = false,
            },
            workspaceId: "workspace-42",
            workspaceEpoch: 42,
            diagnostics:
            [
                new DiagnosticInfo
                {
                    Id = "Id",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "Message",
                },
            ],
            warnings:
            [
                new WarningInfo
                {
                    Code = "Code",
                    Message = "Message",
                },
            ]);

        var json = JsonSerializer.Serialize(result);
        var roundTripped = JsonSerializer.Deserialize<ToolResult<WorkspaceStatusData>>(json);

        roundTripped.Should().NotBeNull();
        roundTripped!.Outcome.Should().Be(ToolOutcome.Succeeded);
        roundTripped.WorkspaceId.Should().Be("workspace-42");
        roundTripped.WorkspaceEpoch.Should().Be(42);
        roundTripped.Data.Should().NotBeNull();
        roundTripped.Data!.State.Should().Be(WorkspaceLifecycleState.Ready);
        roundTripped.Diagnostics.Should().ContainSingle();
        roundTripped.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void GIVEN_RejectedResult_WHEN_CreatedWithWorkspaceIdentity_THEN_ShouldExposeWorkspaceIdentity()
    {
        var result = ToolResult.Rejected<WorkspaceStatusData>(
            new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
            workspaceId: "workspace-42",
            workspaceEpoch: 42);

        result.WorkspaceId.Should().Be("workspace-42");
        result.WorkspaceEpoch.Should().Be(42);
    }

    [Fact]
    public void GIVEN_NoChangeResult_WHEN_Validated_THEN_ShouldHaveNoValidationErrors()
    {
        var result = ToolResult.NoChange<WorkspaceStatusData>(workspaceEpoch: 42);

        var errors = ContractValidator.Validate(result);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_RejectedResultWithoutError_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var result = new ToolResult<WorkspaceStatusData>
        {
            Outcome = ToolOutcome.Rejected,
        };

        var errors = ContractValidator.Validate(result);

        errors.Should().ContainSingle(error => error.Contains("Error"));
    }

    [Fact]
    public void GIVEN_ConflictResultWithData_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var result = new ToolResult<WorkspaceStatusData>
        {
            Outcome = ToolOutcome.Conflict,
            Data = new WorkspaceStatusData
            {
                State = WorkspaceLifecycleState.WorkspaceOutOfDate,
                ReloadRequired = true,
            },
            Error = new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
        };

        var errors = ContractValidator.Validate(result);

        errors.Should().Contain(error => error.Contains("Data"));
    }

    [Fact]
    public void GIVEN_FaultedResultWithChanges_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var result = new ToolResult<WorkspaceStatusData>
        {
            Outcome = ToolOutcome.Faulted,
            Changes = new ChangeSummary(),
            Error = new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
        };

        var errors = ContractValidator.Validate(result);

        errors.Should().Contain(error => error.Contains("Changes"));
    }

    [Fact]
    public void GIVEN_SucceededResultWithoutData_WHEN_Validated_THEN_ShouldReturnValidationError()
    {
        var result = new ToolResult<WorkspaceStatusData>
        {
            Outcome = ToolOutcome.Succeeded,
        };

        var errors = ContractValidator.Validate(result);

        errors.Should().ContainSingle().Which.Should().Contain("Data");
    }

    [Fact]
    public void GIVEN_NoChangeWithErrorAndChanges_WHEN_Validated_THEN_ShouldReturnBothValidationErrors()
    {
        var result = new ToolResult<WorkspaceStatusData>
        {
            Outcome = ToolOutcome.NoChange,
            Changes = new ChangeSummary(),
            Error = new ToolError
            {
                Code = "Code",
                Message = "Message",
            },
        };

        var errors = ContractValidator.Validate(result);

        errors.Should().HaveCount(2);
        errors.Should().Contain(error => error.Contains("Changes"));
        errors.Should().Contain(error => error.Contains("Error"));
    }

    [Theory]
    [InlineData((int)ToolOutcome.Rejected)]
    [InlineData((int)ToolOutcome.Conflict)]
    [InlineData((int)ToolOutcome.Faulted)]
    public void GIVEN_ErrorOutcomeWithDataAndChangesButNoError_WHEN_Validated_THEN_ShouldReturnEveryValidationError(int outcomeValue)
    {
        var outcome = (ToolOutcome)outcomeValue;
        var result = new ToolResult<WorkspaceStatusData>
        {
            Outcome = outcome,
            Data = new WorkspaceStatusData(),
            Changes = new ChangeSummary(),
        };

        var errors = ContractValidator.Validate(result);

        errors.Should().HaveCount(3);
        errors.Should().Contain(error => error.Contains("requires Error"));
        errors.Should().Contain(error => error.Contains("must not include Data"));
        errors.Should().Contain(error => error.Contains("must not include Changes"));
    }

    [Fact]
    public void GIVEN_Error_WHEN_CreatingConflictResult_THEN_ShouldPopulateConflictState()
    {
        var error = new ToolError
        {
            Code = "Code",
            Message = "Message",
        };

        var result = ToolResult.Conflict<WorkspaceStatusData>(error, RequiredAction.Retry);

        result.Outcome.Should().Be(ToolOutcome.Conflict);
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void GIVEN_Error_WHEN_CreatingFaultedResult_THEN_ShouldPopulateFaultedState()
    {
        var error = new ToolError
        {
            Code = "Code",
            Message = "Message",
        };

        var result = ToolResult.Faulted<WorkspaceStatusData>(error, RequiredAction.Retry);

        result.Outcome.Should().Be(ToolOutcome.Faulted);
        result.Error.Should().BeSameAs(error);
    }
}
