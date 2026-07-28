using Roslyn.Workbench.Mcp.Workspace.Selection;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Selection;

public sealed class WorkspaceSelectorServiceTests
{
    private readonly Mock<IWorkspacePathComparison> _workspacePathComparison;
    private readonly WorkspaceSelectorService _target;

    public WorkspaceSelectorServiceTests()
    {
        _workspacePathComparison = new Mock<IWorkspacePathComparison>();
        _workspacePathComparison
            .Setup(item => item.GetComparison(It.IsAny<string>()))
            .Returns(StringComparison.Ordinal);

        _target = new WorkspaceSelectorService(_workspacePathComparison.Object);
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
        var session = CreateSession("WorkspaceId", "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);

        var result = _target.Select(hostSnapshot, selector: null);

        result.HasError.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_MultipleLoadedWorkspacesAndNoSelector_WHEN_SelectingWorkspace_THEN_ShouldRequireSelector()
    {
        var hostSnapshot = CreateHostSnapshot(
            CreateSession("FirstWorkspaceId", "FirstAlias", "FirstPath"),
            CreateSession("SecondWorkspaceId", "SecondAlias", "SecondPath"));

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
        var session = CreateSession("WorkspaceId", "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            WorkspaceId = "WorkspaceId",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_UnknownWorkspaceId_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession("WorkspaceId", "Alias", "WorkspacePath"));
        var selector = new WorkspaceSelector
        {
            WorkspaceId = "UnknownWorkspaceId",
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
        var session = CreateSession("WorkspaceId", "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Alias = "Alias",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_AliasWithDifferentCase_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession("WorkspaceId", "Alias", "WorkspacePath"));
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
    public void GIVEN_MatchingRelativePath_WHEN_SelectingWorkspace_THEN_ShouldReturnMatchingSession()
    {
        var session = CreateSession("WorkspaceId", "Alias", "relative/Workspace.sln");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Path = "relative/Workspace.sln",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_RootedPathContainingRelativeSegments_WHEN_SelectingWorkspace_THEN_ShouldNormalizeAndReturnMatchingSession()
    {
        var workspaceDirectory = Path.Combine(Path.GetTempPath(), "WorkspaceDirectory");
        var loadedPath = Path.Combine(workspaceDirectory, "Workspace.sln");
        var selectorPath = Path.Combine(workspaceDirectory, "NestedDirectory", "..", "Workspace.sln");
        var session = CreateSession("WorkspaceId", "Alias", loadedPath);
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            Path = selectorPath,
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_PathWithDifferentCase_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var loadedPath = Path.Combine(Path.GetTempPath(), "WorkspaceDirectory", "Workspace.sln");
        var selectorPath = Path.Combine(Path.GetTempPath(), "workspaceDirectory", "Workspace.sln");
        var hostSnapshot = CreateHostSnapshot(CreateSession("WorkspaceId", "Alias", loadedPath));
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
        var session = CreateSession("WorkspaceId", "Alias", loadedPath);
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
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_UnknownPath_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession("WorkspaceId", "Alias", "relative/Workspace.sln"));
        var selector = new WorkspaceSelector
        {
            Path = "relative/UnknownWorkspace.sln",
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
        var hostSnapshot = CreateHostSnapshot(CreateSession("WorkspaceId", "Alias", "WorkspacePath"));

        var result = _target.Select(hostSnapshot, new WorkspaceSelector());

        result.HasError.Should().BeTrue();
        result.Error!.Code.Should().Be("WorkspaceSelectorNotFound");
        result.Error.Message.Should().Be("The workspace selector did not match any loaded workspace.");
        result.Error.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public void GIVEN_SelectorWithWhitespaceFields_WHEN_SelectingWorkspace_THEN_ShouldReturnNotFoundError()
    {
        var hostSnapshot = CreateHostSnapshot(CreateSession("WorkspaceId", "Alias", "WorkspacePath"));
        var selector = new WorkspaceSelector
        {
            WorkspaceId = "   ",
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
        var session = CreateSession("WorkspaceId", "Alias", "WorkspacePath");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            WorkspaceId = "WorkspaceId",
            Alias = "Alias",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_AllSelectorFieldsResolveToSameWorkspace_WHEN_SelectingWorkspace_THEN_ShouldReturnMatchingSession()
    {
        var session = CreateSession("WorkspaceId", "Alias", "relative/Workspace.sln");
        var hostSnapshot = CreateHostSnapshot(session);
        var selector = new WorkspaceSelector
        {
            WorkspaceId = "WorkspaceId",
            Alias = "Alias",
            Path = "relative/Workspace.sln",
        };

        var result = _target.Select(hostSnapshot, selector);

        result.HasError.Should().BeFalse();
        result.Selection!.WorkspaceId.Should().Be("WorkspaceId");
        result.Selection.Session.Should().BeSameAs(session);
    }

    [Fact]
    public void GIVEN_IdAndAliasResolveToDifferentWorkspaces_WHEN_SelectingWorkspace_THEN_ShouldReturnMismatchError()
    {
        var hostSnapshot = CreateHostSnapshot(
            CreateSession("FirstWorkspaceId", "FirstAlias", "FirstPath"),
            CreateSession("SecondWorkspaceId", "SecondAlias", "SecondPath"));

        var selector = new WorkspaceSelector
        {
            WorkspaceId = "FirstWorkspaceId",
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
        var hostSnapshot = CreateHostSnapshot(
            CreateSession("FirstWorkspaceId", "FirstAlias", "FirstPath"),
            CreateSession("SecondWorkspaceId", "SecondAlias", "SecondPath"));

        var selector = new WorkspaceSelector
        {
            Alias = "FirstAlias",
            Path = "SecondPath",
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
            Workspaces = sessions.ToDictionary(
                session => session.Workspace.WorkspaceId,
                StringComparer.Ordinal),
        };
    }

    private static WorkspaceSessionSnapshot CreateSession(
        string workspaceId,
        string? alias,
        string loadedPath)
    {
        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = new WorkspaceSnapshotId(1),
            State = WorkspaceLifecycleState.Ready,
            Workspace = new WorkspaceIdentity
            {
                WorkspaceId = workspaceId,
                Alias = alias,
                LoadedPath = loadedPath,
            },
            LoadedWorkspace = null!,
            CurrentSolution = null!,
            InputManifest = null!,
            OperationGate = null!,
        };
    }
}
