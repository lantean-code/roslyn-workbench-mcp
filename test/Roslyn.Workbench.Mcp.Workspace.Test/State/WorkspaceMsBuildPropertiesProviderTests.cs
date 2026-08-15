using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.State;

public sealed class WorkspaceMsBuildPropertiesProviderTests : IDisposable
{
    private static readonly Guid _workspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly AdhocWorkspace _workspace;

    public WorkspaceMsBuildPropertiesProviderTests()
    {
        _workspace = new AdhocWorkspace();
    }

    [Fact]
    public void GIVEN_WorkspaceSessionHasProperties_WHEN_GettingProperties_THEN_ShouldReturnSessionProperties()
    {
        var properties = new WorkspaceMsBuildProperties
        {
            Configuration = "Release",
        };

        var sessionStore = new Mock<IWorkspaceSessionStore>();
        sessionStore.Setup(item => item.ReadSession(_workspaceId)).Returns(CreateSession(properties));
        var target = new WorkspaceMsBuildPropertiesProvider(sessionStore.Object);

        var result = target.Get(_workspaceId);

        result.Should().BeSameAs(properties);
    }

    [Fact]
    public void GIVEN_WorkspaceSessionDoesNotExist_WHEN_GettingProperties_THEN_ShouldReturnNull()
    {
        var sessionStore = new Mock<IWorkspaceSessionStore>();
        var target = new WorkspaceMsBuildPropertiesProvider(sessionStore.Object);

        var result = target.Get(_workspaceId);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession(WorkspaceMsBuildProperties properties)
    {
        var snapshotId = new WorkspaceSnapshotId(1);
        var workspace = new WorkspaceIdentity
        {
            WorkspaceId = _workspaceId,
            LoadedPath = "Workspace.csproj",
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = snapshotId,
            State = WorkspaceLifecycleState.Ready,
            Workspace = workspace,
            LoadedWorkspace = Mock.Of<ILoadedWorkspace>(),
            CurrentSolution = _workspace.CurrentSolution,
            MsBuildProperties = properties,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = Mock.Of<IWorkspaceOperationGate>(),
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(workspace, snapshotId, transaction: null),
        };
    }
}
