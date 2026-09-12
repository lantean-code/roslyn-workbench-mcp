using Roslyn.Workbench.Mcp.Workspace.Recovery;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Recovery;

public sealed class RecoveryFormatVersionsTests
{
    [Theory]
    [InlineData(RecoveryFormatVersions.V1, true)]
    [InlineData(RecoveryFormatVersions.V1 + 1, false)]
    public void GIVEN_RecoveryFormatVersion_WHEN_CheckingReaderSupport_THEN_ShouldReturnExpectedResult(int version, bool expected)
    {
        var result = RecoveryFormatVersions.IsSupported(version);

        result.Should().Be(expected);
    }
}
