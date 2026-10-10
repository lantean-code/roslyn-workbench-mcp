using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class RecoveryFormatVersionsTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CurrentRecoveryFormat_WHEN_ComparingCompatibilityContract_THEN_ShouldRemainV1()
    {
        RecoveryFormatVersions.Current.Should().Be(RecoveryFormatVersions.V1);
        RecoveryFormatVersions.V1.Should().Be(1);
    }

    [Theory]
    [InlineData(RecoveryFormatVersions.V1, true)]
    [InlineData(RecoveryFormatVersions.V1 + 1, false)]
    public void GIVEN_RecoveryFormatVersion_WHEN_CheckingReaderSupport_THEN_ShouldReturnExpectedResult(int version, bool expected)
    {
        var result = RecoveryFormatVersions.IsSupported(version);

        result.Should().Be(expected);
    }
}
