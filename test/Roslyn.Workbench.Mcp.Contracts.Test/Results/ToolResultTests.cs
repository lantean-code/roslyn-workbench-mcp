using System.Text.Json;

using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Contracts.Validation;

using Xunit;

namespace Roslyn.Workbench.Mcp.Contracts.Test.Results;

public sealed class ToolResultTests
{
    [Fact]
    public void GIVEN_ContractsAssembly_WHEN_LookingUpStageFiveMutationTypes_THEN_ShouldExposeMutationPreviewAndMutationData()
    {
        var assembly = typeof(ToolResult<>).Assembly;

        assembly.GetType("Roslyn.Workbench.Mcp.Contracts.Results.MutationPreview").Should().NotBeNull();
        assembly.GetType("Roslyn.Workbench.Mcp.Contracts.Results.MutationData").Should().NotBeNull();
    }

    [Fact]
    public void GIVEN_SucceededResult_WHEN_SerializedAndDeserialized_THEN_ShouldRoundTripCoreState()
    {
        var result = ToolResult<WorkspaceStatusData>.Succeeded(
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
        var result = ToolResult<WorkspaceStatusData>.Rejected(
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
        var result = ToolResult<WorkspaceStatusData>.NoChange(workspaceEpoch: 42);

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
}
