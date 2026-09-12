using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Workspace.Authority;
using Roslyn.Workbench.Mcp.Workspace.Configuration;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Authority;

public sealed class WorkspaceAuthorityTests
{
    [Fact]
    public void GIVEN_NoConfiguredRoots_WHEN_CheckingPath_THEN_ShouldPermitUnrestrictedAdmission()
    {
        var target = CreateTarget([]);

        var result = target.TryGetAllowedRoot("/any/workspace/Sample.csproj", out var allowedRoot);

        result.Should().BeTrue();
        allowedRoot.Should().BeNull();
        target.IsRestricted.Should().BeFalse();
        target.AllowedRootCount.Should().Be(0);
        target.ExternalDocumentPolicy.Should().Be(ExternalDocumentPolicy.AllowReadOnly);
        target.IsWorkspaceAllowed("/any/workspace/Sample.csproj", "/any/workspace").Should().BeTrue();
    }

    [Fact]
    public void GIVEN_AncestorAndNestedConfiguredRoots_WHEN_CheckingPath_THEN_ShouldUseOnlyAncestorAuthority()
    {
        var repositoryRoot = Directory.GetCurrentDirectory();
        var nestedRoot = Path.Combine(repositoryRoot, "src");
        var target = CreateTarget([nestedRoot, repositoryRoot, nestedRoot]);
        var path = Path.Combine(nestedRoot, "Sample.csproj");

        var result = target.TryGetAllowedRoot(path, out var allowedRoot);

        result.Should().BeTrue();
        allowedRoot.Should().Be(Path.GetFullPath(repositoryRoot));
        target.IsRestricted.Should().BeTrue();
        target.AllowedRootCount.Should().Be(1);
    }

    [Fact]
    public void GIVEN_RestrictedAuthority_WHEN_PathIsOutsideRoots_THEN_ShouldRejectIt()
    {
        var allowedRoot = Directory.GetCurrentDirectory();
        var target = CreateTarget([allowedRoot]);

        var result = target.TryGetAllowedRoot(
            Path.Combine(Path.GetTempPath(), "Sample.csproj"),
            out var matchedRoot);

        result.Should().BeFalse();
        matchedRoot.Should().BeNull();
    }

    [Fact]
    public void GIVEN_LoadedPathOutsideAuthority_WHEN_CheckingWorkspace_THEN_ShouldRejectIt()
    {
        var allowedRoot = Directory.GetCurrentDirectory();
        var target = CreateTarget([allowedRoot]);

        var result = target.IsWorkspaceAllowed(
            Path.Combine(Path.GetTempPath(), "Sample.csproj"),
            allowedRoot);

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_EffectiveRootOutsideAuthority_WHEN_CheckingWorkspace_THEN_ShouldRejectIt()
    {
        var allowedRoot = Directory.GetCurrentDirectory();
        var target = CreateTarget([allowedRoot]);

        var result = target.IsWorkspaceAllowed(
            Path.Combine(allowedRoot, "Sample.csproj"),
            Path.GetTempPath());

        result.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_DisjointConfiguredRoots_WHEN_PathIsInSecondRoot_THEN_ShouldReturnMatchingRoot()
    {
        var firstRoot = Path.Combine(Directory.GetCurrentDirectory(), "src");
        var secondRoot = Path.Combine(Directory.GetCurrentDirectory(), "test");
        var target = CreateTarget([firstRoot, secondRoot]);
        var path = Path.Combine(secondRoot, "Sample.csproj");

        var result = target.IsWorkspaceRootAllowed(path);
        target.TryGetAllowedRoot(path, out var allowedRoot).Should().BeTrue();

        result.Should().BeTrue();
        allowedRoot.Should().Be(Path.GetFullPath(secondRoot));
        target.AllowedRootCount.Should().Be(2);
    }

    [Fact]
    public void GIVEN_UncanonicalisableConfiguredRoot_WHEN_Constructing_THEN_ShouldRejectInvalidAuthorityConfiguration()
    {
        var action = () => CreateTarget(["\0"]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Validated Workspace authority roots must be canonicalisable.");
    }

    private static WorkspaceAuthority CreateTarget(IReadOnlyList<string> roots)
    {
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison(fileSystem);
        var pathContainment = new PhysicalPathContainment(fileSystem, pathComparison);
        var pathNormalizer = new WorkspacePathNormalizer(fileSystem);
        return new WorkspaceAuthority(
            Options.Create(new WorkspaceAuthorityOptions { AllowedRoots = roots }),
            pathContainment,
            pathNormalizer);
    }
}
