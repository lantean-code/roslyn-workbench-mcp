using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionReviewDocumentFactoryTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private readonly Mock<IWorkspacePathComparison> _pathComparison = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IPath> _path = new();
    private readonly TransactionReviewDocumentFactory _target;

    public TransactionReviewDocumentFactoryTests()
    {
        _pathComparison
            .Setup(item => item.CreateKey(It.IsAny<string>()))
            .Returns((string value) => new FileSystemPathKey(value, isCaseSensitive: true));

        _fileSystem.SetupGet(item => item.Path).Returns(_path.Object);
        _path
            .Setup(item => item.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string left, string right) => $"{left}/{right}");

        _path
            .Setup(item => item.GetRelativePath("/workspace", It.IsAny<string>()))
            .Returns((string _, string value) => value["/workspace/".Length..].Replace('/', '\\'));

        _target = new TransactionReviewDocumentFactory(_pathComparison.Object, _fileSystem.Object);
    }

    [Fact]
    public void GIVEN_ExactPlanAndChangeSummary_WHEN_ProjectingDocuments_THEN_ShouldReturnCanonicalEntries()
    {
        var session = CreateSession();
        var entries = new[]
        {
            CreateEntry("/workspace/Z.cs", UnixFileMode.UserRead | UnixFileMode.UserWrite),
            CreateEntry("/workspace/Alpha.cs", intendedUnixFileMode: null),
        };

        var plan = CreatePlan(entries);
        var lineSummary = new DiffSummary { AddedLines = 1, RemovedLines = 2, ChangedLines = 3 };
        var changes = new ChangeSummary
        {
            Added =
            [
                new DocumentChange
                {
                    Document = new DocumentReference { DocumentId = "DocumentId", Path = "Z.cs", ProjectId = "ProjectId" },
                    Preview = lineSummary,
                },
                new DocumentChange { Document = null, Preview = lineSummary },
            ],
            Modified = [new DocumentChange { Preview = null }],
            Deleted = [new DocumentChange { Document = null, Preview = null }],
        };

        var result = _target.Create(session, plan, changes);

        result.Select(item => item.Path).Should().Equal("Alpha.cs", "Z.cs");
        result[0].IntendedUnixFileMode.Should().BeNull();
        result[1].IntendedUnixFileMode.Should().Be((int)(UnixFileMode.UserRead | UnixFileMode.UserWrite));
        result[1].LineSummary.Should().BeSameAs(lineSummary);
        result[1].Projects.Should().ContainSingle();
        result[1].Projects[0].Path.Should().Be("Project.csproj");
        result[1].Projects[0].TargetFramework.Should().Be("net10.0");
    }

    [Fact]
    public void GIVEN_NoChangeSummary_WHEN_ProjectingDocuments_THEN_ShouldOmitLineSummary()
    {
        var session = CreateSession();
        var plan = CreatePlan([CreateEntry("/workspace/Z.cs", intendedUnixFileMode: null)]);

        var result = _target.Create(session, plan, changes: null);

        result.Should().ContainSingle();
        result[0].LineSummary.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoActiveTransaction_WHEN_ProjectingDocumentOwners_THEN_ShouldRejectInvalidState()
    {
        var session = CreateSession() with { Transaction = null };
        var plan = CreatePlan([CreateEntry("/workspace/Z.cs", intendedUnixFileMode: null)]);

        var action = () => _target.Create(session, plan, changes: null);

        action.Should().Throw<InvalidOperationException>();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession()
    {
        var firstProjectId = ProjectId.CreateNewId();
        var secondProjectId = ProjectId.CreateNewId();
        var solution = _workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(firstProjectId, VersionStamp.Default, "Project", "Project", LanguageNames.CSharp, filePath: "/workspace/Project.csproj"))
            .AddProject(ProjectInfo.Create(secondProjectId, VersionStamp.Default, "Linked", "Linked", LanguageNames.CSharp))
            .AddDocument(DocumentId.CreateNewId(firstProjectId), "Z.cs", SourceText.From("class Z { }"), filePath: "/workspace/Z.cs")
            .AddDocument(DocumentId.CreateNewId(secondProjectId), "Z.cs", SourceText.From("class Z { }"), filePath: "/workspace/Z.cs");

        var snapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(7),
            BaselineSnapshotId = snapshotId,
            BaselineSolution = solution,
            MaxRevisions = 5,
        };

        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 2,
            LoadedPath = "/workspace/Project.csproj",
            WorkspaceRoot = "/workspace",
        };

        var targetFrameworks = new WorkspaceProjectTargetFrameworkMap(
            new Dictionary<ProjectId, string> { [firstProjectId] = "net10.0" });

        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = snapshotId,
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = solution,
            ProjectTargetFrameworks = targetFrameworks,
            Transaction = transaction,
            InputManifest = new WorkspaceInputManifest(),
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(workspaceIdentity, snapshotId, transaction),
        };
    }

    private static WorkspaceCommitPlan CreatePlan(IReadOnlyList<WorkspaceCommitEntry> entries)
    {
        var manifest = new WorkspaceCommitManifest
        {
            CommitId = "CommitId",
            LoadedPath = "/workspace/Project.csproj",
            WorkspaceRoot = "/workspace",
            State = RecoveryState.Prepared,
            Entries = entries,
            CreatedDirectories = [],
        };

        return new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
    }

    private static WorkspaceCommitEntry CreateEntry(string targetPath, UnixFileMode? intendedUnixFileMode)
    {
        return new WorkspaceCommitEntry
        {
            TargetPath = targetPath,
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
            OriginalHash = "OriginalHash",
            IntendedHash = "IntendedHash",
            IntendedUnixFileMode = intendedUnixFileMode,
        };
    }
}
