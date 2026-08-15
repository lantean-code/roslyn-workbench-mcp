namespace Roslyn.Workbench.Mcp.Workspace.Test.IO;

public sealed class FileSystemPathKeyTests
{
    [Theory]
    [InlineData(true, "Path", "Path", true)]
    [InlineData(true, "Path", "path", false)]
    [InlineData(false, "Path", "path", true)]
    [InlineData(false, "Path", "Other", false)]
    public void GIVEN_PathKeys_WHEN_Comparing_THEN_ShouldUseCapturedFileSystemSemantics(
        bool isCaseSensitive,
        string firstPath,
        string secondPath,
        bool expected)
    {
        var first = new FileSystemPathKey(firstPath, isCaseSensitive);
        var second = new FileSystemPathKey(secondPath, isCaseSensitive);

        first.Equals(second).Should().Be(expected);
        first.Equals((object)second).Should().Be(expected);
        (first == second).Should().Be(expected);
        (first != second).Should().Be(!expected);
        first.Path.Should().Be(firstPath);
        first.Comparison.Should().Be(isCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase);

        if (expected)
        {
            first.GetHashCode().Should().Be(second.GetHashCode());
        }
    }

    [Fact]
    public void GIVEN_DifferentFileSystemSemantics_WHEN_Comparing_THEN_ShouldNotBeEqual()
    {
        var caseSensitive = new FileSystemPathKey("Path", isCaseSensitive: true);
        var caseInsensitive = new FileSystemPathKey("Path", isCaseSensitive: false);

        caseSensitive.Equals(caseInsensitive).Should().BeFalse();
        caseSensitive.Equals("Path").Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DefaultKey_WHEN_ReadingPath_THEN_ShouldReturnEmptyPath()
    {
        var target = default(FileSystemPathKey);

        target.Path.Should().BeEmpty();
        target.GetHashCode().Should().Be(default(FileSystemPathKey).GetHashCode());
    }
}
