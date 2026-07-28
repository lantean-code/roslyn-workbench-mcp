namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class MefHostExportReadResultTests
{
    [Fact]
    public void GIVEN_SuccessfulExportReadResult_WHEN_ReadingState_THEN_ShouldExposeExportsWithoutError()
    {
        IReadOnlyList<string> exports = ["Export"];

        var result = MefHostExportReadResult.Success(exports);

        result.IsSuccessful.Should().BeTrue();
        result.Exports.Should().BeSameAs(exports);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_FailedExportReadResult_WHEN_ReadingState_THEN_ShouldExposeErrorWithoutExports()
    {
        var result = MefHostExportReadResult.Failure<string>("Error");

        result.IsSuccessful.Should().BeFalse();
        result.Exports.Should().BeEmpty();
        result.Error.Should().Be("Error");
    }
}
