using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Selection;

public sealed class WorkspaceSelectorServiceTests
{
    private readonly Mock<IWorkspacePathComparison> _workspacePathComparison;
    private readonly Mock<IWorkspacePathNormalizer> _pathNormalizer;
    private readonly WorkspaceSelectorService _target;

    public WorkspaceSelectorServiceTests()
    {
        _workspacePathComparison = new Mock<IWorkspacePathComparison>();
        _pathNormalizer = new Mock<IWorkspacePathNormalizer>();
        _workspacePathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);
        _pathNormalizer
            .Setup(item => item.TryGetFullPath(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string path, out string fullPath) =>
            {
                try
                {
                    fullPath = Path.GetFullPath(path);
                    return true;
                }
                catch (ArgumentException)
                {
                    fullPath = string.Empty;
                    return false;
                }
            });

        _target = new WorkspaceSelectorService(
            _workspacePathComparison.Object,
            _pathNormalizer.Object);
    }

    [Fact]
    public void GIVEN_NoLoadedWorkspacesAndNoSelector_WHEN_SelectingWorkspace_THEN_ShouldRequireOpenWorkspace()
    {
        var hostSnapshot = CreateHostSnapshot();

        var result = _target.Select(hostSnapshot, selector: null);

        result.HasError.Should().BeTrue();
        result.Selection.Should().BeNull();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("Open a workspace before invoking this tool.");
        result.Error.RequiredAction.Should().Be(RequiredAction.OpenWorkspace);
    }

    [Fact]
    public void GIVEN_OneLoadedWorkspaceAndNoSelector_WHEN_SelectingWorkspace_THEN_ShouldSelectOnlyWorkspace()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);

        var result = _target.Select(hostSnapshot, selector: null);

        result.HasError.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_MultipleLoadedWorkspacesAndNoSelector_WHEN_SelectingWorkspace_THEN_ShouldRequireSelector()
    {
        var hostSnapshot = CreateHostSnapshot(
            CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "FirstAlias", "FirstPath"),
            CreateSession(Guid.Parse("66666666-6666-6666-6666-666666666666"), "SecondAlias", "SecondPath"));

        var result = _target.Select(hostSnapshot, selector: null);

        result.HasError.Should().BeTrue();
        result.Selection.Should().BeNull();
        result.Error!.Code.Should().Be("WorkspaceSelectorRequired");
        result.Error.Message.Should().Be("Select a workspace when more than one workspace is loaded.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MatchingWorkspaceId_WHEN_SelectingWorkspace_THEN_ShouldReturnMatchingSession()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_UnknownWorkspaceId_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath"));
        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MatchingAlias_WHEN_SelectingWorkspace_THEN_ShouldReturnMatchingSession()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Alias = "Alias",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_AliasWithDifferentCase_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath"));
        var selector = new WorkspaceSelector
        {
            Alias = "alias",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_RelativePath_WHEN_SelectingWorkspace_THEN_ShouldReturnInvalidError()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "relative/Workspace.sln");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Path = "relative/Workspace.sln",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Selection.Should().BeNull();
        result.Error!.Code.Should().Be("WorkspaceSelectorInvalid");
        result.Error.Message.Should().Be("The workspace selector path must be a valid absolute path.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_MalformedAbsolutePath_WHEN_SelectingWorkspace_THEN_ShouldReturnInvalidError()
    {
        var loadedPath = Path.Combine(Path.GetTempPath(), "Workspace", "Workspace.sln");
        var malformedPath = Path.GetPathRoot(Path.GetTempPath()) + "\0Workspace.sln";
        var hostSnapshot = CreateHostSnapshot(
            CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", loadedPath));
        var selector = new WorkspaceSelector
        {
            Path = malformedPath,
        };
        _pathNormalizer
            .Setup(item => item.TryGetFullPath(malformedPath, out It.Ref<string>.IsAny))
            .Returns(false);

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Selection.Should().BeNull();
        result.Error!.Code.Should().Be("WorkspaceSelectorInvalid");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_RootedPathContainingRelativeSegments_WHEN_SelectingWorkspace_THEN_ShouldNormalizeAndReturnMatchingSession()
    {
        var workspaceDirectory = Path.Combine(Path.GetTempPath(), "WorkspaceDirectory");
        var loadedPath = Path.Combine(workspaceDirectory, "Workspace.sln");
        var selectorPath = Path.Combine(workspaceDirectory, "NestedDirectory", "..", "Workspace.sln");
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", loadedPath);
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Path = selectorPath,
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_PathWithDifferentCase_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var loadedPath = Path.Combine(Path.GetTempPath(), "WorkspaceDirectory", "Workspace.sln");
        var selectorPath = Path.Combine(Path.GetTempPath(), "workspaceDirectory", "Workspace.sln");
        var hostSnapshot = CreateHostSnapshot(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", loadedPath));
        var selector = new WorkspaceSelector
        {
            Path = selectorPath,
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_CaseInsensitiveWorkspacePath_WHEN_SelectingWithDifferentCase_THEN_ShouldReturnMatchingSession()
    {
        var loadedPath = Path.Combine(Path.GetTempPath(), "WorkspaceDirectory", "Workspace.sln");
        var selectorPath = Path.Combine(Path.GetTempPath(), "workspaceDirectory", "Workspace.sln");
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", loadedPath);
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Path = selectorPath,
        };

        _workspacePathComparison
            .Setup(item => item.GetComparison(loadedPath))
            .Returns(StringComparison.OrdinalIgnoreCase);

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_UnknownPath_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var loadedPath = Path.Combine(Path.GetTempPath(), "Workspace", "Workspace.sln");
        var unknownPath = Path.Combine(Path.GetTempPath(), "Workspace", "UnknownWorkspace.sln");
        var hostSnapshot = CreateHostSnapshot(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", loadedPath));
        var selector = new WorkspaceSelector
        {
            Path = unknownPath,
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_SelectorWithoutPopulatedFields_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath"));

        var result = _target.Select(hostSnapshot, new WorkspaceSelector());

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_SelectorWithWhitespaceFields_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath"));
        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Empty,
            Alias = "   ",
            Path = "   ",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_IdAndAliasResolveToSameWorkspace_WHEN_SelectingWorkspace_THEN_ShouldReturnMatchingSession()
    {
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Alias = "Alias",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_AllSelectorFieldsResolveToSameWorkspace_WHEN_SelectingWorkspace_THEN_ShouldReturnMatchingSession()
    {
        var loadedPath = Path.Combine(Path.GetTempPath(), "Workspace", "Workspace.sln");
        var session = CreateSession(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alias", loadedPath);
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Alias = "Alias",
            Path = loadedPath,
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_IdAndAliasResolveToDifferentWorkspaces_WHEN_SelectingWorkspace_THEN_ShouldReturnMismatchError()
    {
        var hostSnapshot = CreateHostSnapshot(
            CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "FirstAlias", "FirstPath"),
            CreateSession(Guid.Parse("66666666-6666-6666-6666-666666666666"), "SecondAlias", "SecondPath"));

        var selector = new WorkspaceSelector
        {
            WorkspaceId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Alias = "SecondAlias",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorMismatch");
        result.Error.Message.Should().Be("The workspace selector fields must resolve to the same loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_AliasAndPathResolveToDifferentWorkspaces_WHEN_SelectingWorkspace_THEN_ShouldReturnMismatchError()
    {
        var firstPath = Path.Combine(Path.GetTempPath(), "First", "Workspace.sln");
        var secondPath = Path.Combine(Path.GetTempPath(), "Second", "Workspace.sln");
        var hostSnapshot = CreateHostSnapshot(
            CreateSession(Guid.Parse("55555555-5555-5555-5555-555555555555"), "FirstAlias", firstPath),
            CreateSession(Guid.Parse("66666666-6666-6666-6666-666666666666"), "SecondAlias", secondPath));

        var selector = new WorkspaceSelector
        {
            Alias = "FirstAlias",
            Path = secondPath,
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorMismatch");
        result.Error.Message.Should().Be("The workspace selector fields must resolve to the same loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    private static WorkspaceHostSnapshot CreateHostSnapshot(params WorkspaceSessionSnapshot[] sessions)
    {
        return new WorkspaceHostSnapshot
        {
            Workspaces = sessions.ToDictionary(session => session.Workspace.WorkspaceId),
        };
    }

    private static WorkspaceSessionSnapshot CreateSession(
        Guid workspaceId,
        string? alias,
        string loadedPath)
    {
        var committedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = workspaceId,
            Alias = alias,
            LoadedPath = loadedPath,
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspaceIdentity,
            LoadedWorkspace = null!,
            CurrentSolution = null!,
            InputManifest = null!,
            OperationGate = null!,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction: null),
        };
    }
}
