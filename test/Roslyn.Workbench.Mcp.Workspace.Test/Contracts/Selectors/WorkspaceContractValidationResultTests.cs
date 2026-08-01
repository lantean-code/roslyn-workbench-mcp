namespace Roslyn.Workbench.Mcp.Workspace.Test.Contracts.Selectors;

public sealed class WorkspaceContractValidationResultTests
{
    [Fact]
    public void GIVEN_ValidResult_WHEN_InspectingState_THEN_ShouldExposeSuccessAndNoFailures()
    {
        var result = WorkspaceContractValidationResult.Valid();

        result.IsValid.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_Failures_WHEN_CreatingInvalidResult_THEN_ShouldExposeFailureState()
    {
        var failure = new WorkspaceContractValidationFailure("Failure", ["Member"]);

        var result = WorkspaceContractValidationResult.Invalid([failure]);

        result.IsValid.Should().BeFalse();
        result.Failures.Should().ContainSingle().Which.Should().Be(failure);
    }

    [Fact]
    public void GIVEN_NoFailures_WHEN_CreatingInvalidResult_THEN_ShouldRejectContradictoryState()
    {
        var action = static () => WorkspaceContractValidationResult.Invalid([]);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("failures");
    }
}
