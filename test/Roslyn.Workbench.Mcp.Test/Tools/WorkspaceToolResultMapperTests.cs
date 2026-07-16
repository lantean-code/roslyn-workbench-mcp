namespace Roslyn.Workbench.Mcp.Test.Tools;

public sealed class WorkspaceToolResultMapperTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 1)]
    public void GIVEN_WorkspaceOutcome_WHEN_Mapping_THEN_ShouldPreserveOutcomeAndContext(
        int statusValue,
        int expectedOutcomeValue)
    {
        var status = (WorkspaceOperationStatus)statusValue;
        var expectedOutcome = (ToolOutcome)expectedOutcomeValue;
        var result = CreateResult(status, includeData: status is WorkspaceOperationStatus.Succeeded or WorkspaceOperationStatus.NoChange);

        var mapped = WorkspaceToolResultMapper.Map(result, source => new TestTarget
        {
            Value = source.Value,
        });

        mapped.Outcome.Should().Be(expectedOutcome);
        mapped.WorkspaceId.Should().Be("WorkspaceId");
        mapped.WorkspaceEpoch.Should().Be(2);
        mapped.TransactionRevision.Should().Be(3);
        mapped.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("Id");
        mapped.Warnings.Should().ContainSingle().Which.Code.Should().Be("Code");
        if (expectedOutcome.IsError())
        {
            mapped.Error!.Code.Should().Be("ErrorCode");
            mapped.RequiredAction.Should().Be(RequiredAction.Retry);
        }
        else
        {
            mapped.Data!.Value.Should().Be("Value");
        }
    }

    [Fact]
    public void GIVEN_NoChangeWithoutData_WHEN_Mapping_THEN_ShouldNotInvokeMapper()
    {
        var result = CreateResult(WorkspaceOperationStatus.NoChange, includeData: false);
        var mapper = new Mock<Func<TestSource, TestTarget>>();

        var mapped = WorkspaceToolResultMapper.Map(result, mapper.Object);

        mapped.Outcome.Should().Be(ToolOutcome.NoChange);
        mapped.Data.Should().BeNull();
        mapper.Verify(item => item(It.IsAny<TestSource>()), Times.Never);
    }

    [Fact]
    public void GIVEN_UnsupportedStatus_WHEN_Mapping_THEN_ShouldThrowInvalidOperationException()
    {
        var result = CreateResult((WorkspaceOperationStatus)999, includeData: false);

        var action = () => WorkspaceToolResultMapper.Map(result, source => new TestTarget
        {
            Value = source.Value,
        });

        action.Should().Throw<InvalidOperationException>();
    }

    private static WorkspaceOperationResult<TestSource> CreateResult(
        WorkspaceOperationStatus status,
        bool includeData)
    {
        return new WorkspaceOperationResult<TestSource>
        {
            Status = status,
            Context = new WorkspaceOperationContext
            {
                WorkspaceId = "WorkspaceId",
                WorkspaceEpoch = 2,
                TransactionRevision = 3,
            },
            Data = includeData
                ? new TestSource
                {
                    Value = "Value",
                }
                : null,
            Error = status is WorkspaceOperationStatus.Rejected or WorkspaceOperationStatus.Conflict or WorkspaceOperationStatus.Faulted
                ? new WorkspaceOperationError
                {
                    Code = "ErrorCode",
                    Message = "Message",
                    RequiredAction = RequiredAction.Retry,
                }
                : null,
            Diagnostics =
            [
                new DiagnosticInfo
                {
                    Id = "Id",
                    Message = "Message",
                },
            ],
            Warnings =
            [
                new WarningInfo
                {
                    Code = "Code",
                    Message = "Message",
                },
            ],
        };
    }

    public sealed record TestSource
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed record TestTarget
    {
        public string Value { get; init; } = string.Empty;
    }
}
