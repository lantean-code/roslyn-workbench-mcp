namespace Roslyn.Workbench.Mcp.Test.Protocol.Results;

public sealed class RequiredActionContinuationMapperTests
{
    public static TheoryData<RequiredAction, string, string?, string?> Mappings
    {
        get
        {
            return new TheoryData<RequiredAction, string, string?, string?>
            {
                { RequiredAction.OpenWorkspace, "CallTool", "workspace-open", null },
                { RequiredAction.StartTransaction, "CallTool", "transaction-start", null },
                { RequiredAction.RollbackTransaction, "CallTool", "transaction-rollback", null },
                { RequiredAction.ReloadWorkspace, "CallTool", "workspace-reload", null },
                { RequiredAction.ResolveTargetAgain, "ReviseRequest", null, null },
                { RequiredAction.CommitOrRollback, "ChooseTool", null, "transaction-commit,transaction-rollback" },
                { RequiredAction.ReduceTransactionHistory, "CallTool", "transaction-history", null },
                { RequiredAction.Retry, "RetryRequest", null, null },
                { RequiredAction.ResolveRecovery, "ResolveExternally", null, null },
                { RequiredAction.NarrowRequest, "ReviseRequest", null, null },
            };
        }
    }

    [Theory]
    [MemberData(nameof(Mappings))]
    public void GIVEN_RequiredAction_WHEN_Mapping_THEN_ShouldPublishExactContinuation(
        RequiredAction requiredAction,
        string expectedKind,
        string? expectedTool,
        string? expectedTools)
    {
        var result = RequiredActionContinuationMapper.Map(requiredAction);

        result.Should().NotBeNull();
        result.Kind.ToString().Should().Be(expectedKind);
        result.Tool.Should().Be(expectedTool);
        result.Tools.Should().BeEquivalentTo(ParseTools(expectedTools));
        result.Instruction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GIVEN_NoRequiredAction_WHEN_Mapping_THEN_ShouldReturnNoContinuation()
    {
        var result = RequiredActionContinuationMapper.Map(null);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_UnsupportedRequiredAction_WHEN_Mapping_THEN_ShouldRejectMissingPublishedContract()
    {
        var action = () => RequiredActionContinuationMapper.Map((RequiredAction)int.MaxValue);

        action.Should().Throw<InvalidOperationException>();
    }

    private static string[]? ParseTools(string? tools)
    {
        if (tools is null)
        {
            return null;
        }

        return tools.Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
}
