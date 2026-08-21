using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.ChangeDetection;

public sealed class WorkspaceExternalInputMembershipTests
{
    [Fact]
    public void GIVEN_PathOutsideSearchRoot_WHEN_Matching_THEN_ShouldRejectWithoutEvaluatingGlobs()
    {
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, ["/External"]);
        var root = new FileSystemPathKey("/External", isCaseSensitive: true);
        var loadedPaths = new HashSet<FileSystemPathKey>();
        var target = new WorkspaceExternalInputMembership(
            root,
            [glob],
            loadedPaths);

        var result = target.Matches("/Other/Document.cs");

        result.Should().BeFalse();
        matcher.Verify(item => item.Matches(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GIVEN_PathInsideSearchRoot_WHEN_AnyGlobMatches_THEN_ShouldAcceptIt()
    {
        var firstMatcher = new Mock<IWorkspaceItemGlobMatcher>();
        var secondMatcher = new Mock<IWorkspaceItemGlobMatcher>();
        var path = "/External/Document.cs";
        firstMatcher.Setup(item => item.Matches(path)).Returns(false);
        secondMatcher.Setup(item => item.Matches(path)).Returns(true);
        var firstGlob = new WorkspaceEvaluatedItemGlob(firstMatcher.Object, ["/External"]);
        var secondGlob = new WorkspaceEvaluatedItemGlob(secondMatcher.Object, ["/External"]);
        var root = new FileSystemPathKey("/External", isCaseSensitive: true);
        var loadedPaths = new HashSet<FileSystemPathKey>();
        var target = new WorkspaceExternalInputMembership(
            root,
            [firstGlob, secondGlob],
            loadedPaths);

        var result = target.Matches(path);

        result.Should().BeTrue();
        firstMatcher.Verify(item => item.Matches(path), Times.Once);
        secondMatcher.Verify(item => item.Matches(path), Times.Once);
    }

    [Fact]
    public void GIVEN_CaseInsensitiveSearchRoot_WHEN_PathCasingDiffers_THEN_ShouldEvaluateGlob()
    {
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        var path = "/external/Document.cs";
        matcher.Setup(item => item.Matches(path)).Returns(false);
        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, ["/External"]);
        var root = new FileSystemPathKey("/External", isCaseSensitive: false);
        var loadedPaths = new HashSet<FileSystemPathKey>();
        var target = new WorkspaceExternalInputMembership(
            root,
            [glob],
            loadedPaths);

        var result = target.Matches(path);

        result.Should().BeFalse();
        matcher.Verify(item => item.Matches(path), Times.Once);
    }

    [Fact]
    public void GIVEN_SearchRootPath_WHEN_GlobMatchesRoot_THEN_ShouldAcceptIt()
    {
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        matcher.Setup(item => item.Matches("/External")).Returns(true);
        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, ["/External"]);
        var root = new FileSystemPathKey("/External", isCaseSensitive: true);
        var loadedPaths = new HashSet<FileSystemPathKey>();
        var target = new WorkspaceExternalInputMembership(
            root,
            [glob],
            loadedPaths);

        var result = target.Matches("/External");

        result.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_SearchRootWithTrailingSeparator_WHEN_ChildPathMatches_THEN_ShouldAcceptIt()
    {
        var searchRoot = "/External/";
        var path = "/External/Document.cs";
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        matcher.Setup(item => item.Matches(path)).Returns(true);
        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, [searchRoot]);
        var root = new FileSystemPathKey(searchRoot, isCaseSensitive: true);
        var loadedPaths = new HashSet<FileSystemPathKey>();
        var target = new WorkspaceExternalInputMembership(
            root,
            [glob],
            loadedPaths);

        var result = target.Matches(path);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("/External", true)]
    [InlineData("/External/Directory", true)]
    [InlineData("/Other", false)]
    public void GIVEN_SearchRoot_WHEN_CheckingContainment_THEN_ShouldReturnExpectedResult(string path, bool expected)
    {
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, ["/External"]);
        var root = new FileSystemPathKey("/External", isCaseSensitive: true);
        var loadedPaths = new HashSet<FileSystemPathKey>();
        var target = new WorkspaceExternalInputMembership(
            root,
            [glob],
            loadedPaths);

        var result = target.Contains(path);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/External/Directory", true)]
    [InlineData("/External/Document.cs", true)]
    [InlineData("/External/Other", false)]
    public void GIVEN_LoadedPath_WHEN_CheckingAncestor_THEN_ShouldReturnExpectedResult(string path, bool expected)
    {
        var matcher = new Mock<IWorkspaceItemGlobMatcher>();
        var glob = new WorkspaceEvaluatedItemGlob(matcher.Object, ["/External"]);
        var root = new FileSystemPathKey("/External", isCaseSensitive: true);
        var loadedPaths = new HashSet<FileSystemPathKey>
        {
            new FileSystemPathKey("/External/Directory/Document.cs", isCaseSensitive: true),
            new FileSystemPathKey("/External/Document.cs", isCaseSensitive: true),
        };

        var target = new WorkspaceExternalInputMembership(
            root,
            [glob],
            loadedPaths);

        var result = target.ContainsLoadedPathWithin(path);

        result.Should().Be(expected);
    }
}
