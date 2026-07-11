using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Loading;

public sealed class WorkspaceLoaderTests
{
    private readonly WorkspaceLoader _target;

    public WorkspaceLoaderTests()
    {
        _target = new WorkspaceLoader(new WorkspaceHostServicesAccessor(workspaceHostServices: null));
    }

    [Theory]
    [InlineData("Solution.sln")]
    [InlineData("Solution.slnx")]
    [InlineData("Project.csproj")]
    [InlineData("Solution.SLN")]
    [InlineData("Solution.SLNX")]
    [InlineData("Project.CSPROJ")]
    public void GIVEN_RootedSupportedPath_WHEN_NormalisingOpenPath_THEN_ShouldReturnFullPath(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkspaceLoaderTests", fileName);

        var result = _target.NormalizeOpenPath(path);

        result.Should().Be(Path.GetFullPath(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/Project.csproj")]
    public void GIVEN_BlankOrRelativePath_WHEN_NormalisingOpenPath_THEN_ShouldReturnNull(string path)
    {
        var result = _target.NormalizeOpenPath(path);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RootedUnsupportedPath_WHEN_NormalisingOpenPath_THEN_ShouldReturnNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "WorkspaceLoaderTests", "Document.cs");

        var result = _target.NormalizeOpenPath(path);

        result.Should().BeNull();
    }

    [Fact]
    public void GIVEN_RootedMalformedPath_WHEN_NormalisingOpenPath_THEN_ShouldReturnNull()
    {
        var path = Path.GetPathRoot(Path.GetTempPath()) + "\0Project.csproj";

        var result = _target.NormalizeOpenPath(path);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" Alias ", "Alias")]
    public void GIVEN_Alias_WHEN_NormalisingAlias_THEN_ShouldReturnExpectedValue(string? alias, string? expected)
    {
        var result = _target.NormalizeAlias(alias);

        result.Should().Be(expected);
    }
}
