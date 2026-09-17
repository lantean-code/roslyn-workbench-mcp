using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionReviewIdentityServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private readonly TransactionReviewIdentityService _target = new();

    [Fact]
    public void GIVEN_CanonicalDocuments_WHEN_CreatingIdentity_THEN_ShouldProduceStableGoldenDigest()
    {
        var session = CreateSession();
        var documents = CreateDocuments();

        var identity = _target.Create(session, documents);

        identity.Algorithm.Should().Be("sha256-rwcs-v1");
        identity.ChangeSetDigest.Should().Be("cabf93a0dc6cc92c41b7b2b371d091dcca3d48b8cce9eb961668f2d961ce70ef");
        identity.WorkspaceId.Should().Be(session.Workspace.WorkspaceId);
        identity.TransactionId.Should().Be(7);
    }

    [Fact]
    public void GIVEN_DocumentsInDifferentEnumerationOrder_WHEN_CreatingIdentity_THEN_ShouldProduceSameDigest()
    {
        var session = CreateSession();
        var documents = CreateDocuments();

        var forward = _target.Create(session, documents);
        var reverse = _target.Create(session, documents.Reverse().ToArray());

        reverse.ChangeSetDigest.Should().Be(forward.ChangeSetDigest);
    }

    [Fact]
    public void GIVEN_AmbiguousConcatenatedPathText_WHEN_CreatingIdentity_THEN_ShouldRemainLengthDelimited()
    {
        var session = CreateSession();
        var first = CreateDocument("ab", "c");
        var second = CreateDocument("a", "bc");

        var firstIdentity = _target.Create(session, [first]);
        var secondIdentity = _target.Create(session, [second]);

        secondIdentity.ChangeSetDigest.Should().NotBe(firstIdentity.ChangeSetDigest);
    }

    [Fact]
    public void GIVEN_ExactSerializedByteHashChanges_WHEN_CreatingIdentity_THEN_ShouldChangeDigest()
    {
        var session = CreateSession();
        var document = CreateDocument("Sample.cs", "OriginalHash");
        var changedDocument = document with { IntendedHash = "DifferentBytesHash" };

        var original = _target.Create(session, [document]);
        var changed = _target.Create(session, [changedDocument]);

        changed.ChangeSetDigest.Should().NotBe(original.ChangeSetDigest);
    }

    [Fact]
    public void GIVEN_DocumentClassificationChanges_WHEN_CreatingIdentity_THEN_ShouldChangeDigest()
    {
        var session = CreateSession();
        var document = CreateDocument("Sample.cs", "OriginalHash");
        var classifiedDocument = document with { Classification = "generated-looking-source" };

        var original = _target.Create(session, [document]);
        var classified = _target.Create(session, [classifiedDocument]);

        classified.ChangeSetDigest.Should().NotBe(original.ChangeSetDigest);
    }

    [Fact]
    public void GIVEN_NullableAndFalseDocumentValues_WHEN_CreatingIdentity_THEN_ShouldCreateDigest()
    {
        var session = CreateSession();
        var document = new TransactionReviewDocument
        {
            Path = "Added.cs",
            Operation = WorkspaceFileOperation.Create,
            OriginalExists = false,
            OriginalHash = null,
            IntendedHash = null,
            IntendedUnixFileMode = null,
            Projects = [],
            Contained = false,
        };

        var identity = _target.Create(session, [document]);

        identity.ChangeSetDigest.Should().HaveLength(64);
    }

    [Fact]
    public void GIVEN_NoActiveTransaction_WHEN_CreatingIdentity_THEN_ShouldRejectInvalidState()
    {
        var session = CreateSession() with { Transaction = null };

        var action = () => _target.Create(session, []);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GIVEN_IdentityMatchesCurrentTransaction_WHEN_CheckingBinding_THEN_ShouldReturnTrue()
    {
        var session = CreateSession();
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("The test session must contain a transaction.");

        var identity = _target.Create(session, []);

        var result = _target.IsBoundTo(identity, session, transaction);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void GIVEN_IdentityBindingComponentDiffers_WHEN_CheckingBinding_THEN_ShouldReturnFalse(int bindingComponent)
    {
        var session = CreateSession();
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("The test session must contain a transaction.");

        var identity = _target.Create(session, []);
        var differentWorkspaceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var differentSnapshotId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var differentIdentity = bindingComponent switch
        {
            0 => identity with { WorkspaceId = differentWorkspaceId },
            1 => identity with { WorkspaceEpoch = identity.WorkspaceEpoch + 1 },
            2 => identity with { TransactionId = identity.TransactionId + 1 },
            3 => identity with { SnapshotId = differentSnapshotId },
            _ => identity with { TransactionRevision = identity.TransactionRevision + 1 },
        };

        var result = _target.IsBoundTo(differentIdentity, session, transaction);

        result.Should().BeFalse();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceSessionSnapshot CreateSession()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var baseline = project.Solution;
        var snapshotId = WorkspaceSnapshotTestFactory.CreateId(2);
        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(7),
            BaselineSnapshotId = snapshotId,
            BaselineSolution = baseline,
            MaxRevisions = 5,
        };

        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 3,
            LoadedPath = "/workspace/Sample.csproj",
            WorkspaceRoot = "/workspace",
        };

        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();
        var inputManifest = new WorkspaceInputManifest();
        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = snapshotId,
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = transaction.CurrentSolution,
            Transaction = transaction,
            InputManifest = inputManifest,
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(workspaceIdentity, snapshotId, transaction),
        };
    }

    private static TransactionReviewDocument[] CreateDocuments()
    {
        return
        [
            CreateDocument("Zed.cs", "OriginalZed"),
            CreateDocument("Alpha.cs", "OriginalAlpha"),
        ];
    }

    private static TransactionReviewDocument CreateDocument(string path, string originalHash)
    {
        var project = new TransactionReviewProject
        {
            Path = "Sample.csproj",
            TargetFramework = "net10.0",
        };

        return new TransactionReviewDocument
        {
            Path = path,
            Operation = WorkspaceFileOperation.Replace,
            OriginalExists = true,
            OriginalHash = originalHash,
            IntendedHash = "IntendedHash",
            IntendedUnixFileMode = 420,
            Projects = [project],
        };
    }
}
