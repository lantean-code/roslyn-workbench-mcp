namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class RecoveryFormatEnumContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_WorkspaceFileOperations_WHEN_ReadingNumericValues_THEN_ShouldMatchV1Format()
    {
        ((int)WorkspaceFileOperation.Create).Should().Be(0);
        ((int)WorkspaceFileOperation.Replace).Should().Be(1);
        ((int)WorkspaceFileOperation.Delete).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_RecoveryStates_WHEN_ReadingNumericValues_THEN_ShouldMatchV1Format()
    {
        ((int)RecoveryState.Prepared).Should().Be(0);
        ((int)RecoveryState.Applying).Should().Be(1);
        ((int)RecoveryState.Committed).Should().Be(2);
        ((int)RecoveryState.Restored).Should().Be(3);
        ((int)RecoveryState.RecoveryConflict).Should().Be(4);
        ((int)RecoveryState.RecoveryIncomplete).Should().Be(5);
    }
}
