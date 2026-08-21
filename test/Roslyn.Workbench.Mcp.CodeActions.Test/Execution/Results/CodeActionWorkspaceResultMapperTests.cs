namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Results;

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

        result.Outcome.Should().Be(expectedOutcome);
        result.Error.Code.Should().Be("Code");
        result.RequiredAction.Should().Be(RequiredAction.Retry);
    }

    [Fact]
    public void GIVEN_NonFailureWorkspaceStatus_WHEN_MappingFailure_THEN_ShouldThrowInvalidOperationException()
    {
        var failure = new WorkspaceExecutionFailure
        {
            Status = WorkspaceOperationStatus.Succeeded,
            Error = CreateError(),
        };

        var action = () => CodeActionWorkspaceResultMapper.MapFailure(failure);

        action.Should().Throw<InvalidOperationException>();
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
            result.Data.Summary.Should().Be("Summary");
            result.Data.Transaction!.Revision.Should().Be(1);
            result.Data.Preview!.Summary.Should().Be("PreviewSummary");
            result.Changes!.Modified.Should().ContainSingle();
        }
        else if (status is WorkspaceOperationStatus.Rejected or WorkspaceOperationStatus.Conflict or WorkspaceOperationStatus.Faulted)
        {
            result.Error!.Code.Should().Be("Code");
            result.Error.Message.Should().Be("Message");
            result.RequiredAction.Should().Be(RequiredAction.Retry);
        }

        result.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DiagnosticId");
        result.Warnings.Should().ContainSingle().Which.Code.Should().Be("WarningCode");
    }

    private static WorkspaceOperationResult<MutationStagingOutcome> CreateMutationResult(WorkspaceOperationStatus status)
    {
        var diagnostics = new[] { new DiagnosticInfo { Id = "DiagnosticId", Message = "DiagnosticMessage" } };
        var warnings = new[] { new WarningInfo { Code = "WarningCode", Message = "WarningMessage" } };

        if (status == WorkspaceOperationStatus.Succeeded)
        {
            var data = new MutationStagingOutcome
            {
                Operation = "Operation",
                Summary = "Summary",
                Transaction = new TransactionInfo { Revision = 1 },
                Preview = new MutationPreview { Summary = "PreviewSummary" },
                Changes = new ChangeSummary
                {
                    Modified =
                    [
                        new DocumentChange
                        {
                            Document = new DocumentReference { Path = "DocumentPath" },
                        },
                    ],
                },
            };

            var context = WorkspaceSnapshotTestFactory.CreateContext(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                transactionRevision: 1);

            return WorkspaceOperationResult.Succeeded(
                data,
                context,
                diagnostics: diagnostics,
                warnings: warnings);
        }

        if (status == WorkspaceOperationStatus.NoChange)
        {
            return WorkspaceOperationResult.NoChange<MutationStagingOutcome>(
                diagnostics: diagnostics,
                warnings: warnings);
        }

        var error = CreateError();

        return status switch
        {
            WorkspaceOperationStatus.Rejected => WorkspaceOperationResult.Rejected<MutationStagingOutcome>(
                error,
                diagnostics: diagnostics,
                warnings: warnings),
            WorkspaceOperationStatus.Conflict => WorkspaceOperationResult.Conflict<MutationStagingOutcome>(
                error,
                diagnostics: diagnostics,
                warnings: warnings),
            WorkspaceOperationStatus.Faulted => WorkspaceOperationResult.Faulted<MutationStagingOutcome>(
                error,
                diagnostics: diagnostics,
                warnings: warnings),
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
