using Roslyn.Workbench.Mcp.Workspace.ChangeDetection;
using Roslyn.Workbench.Mcp.Workspace.Loading;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionReviewBuilderTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();
    private readonly Mock<IWorkspaceCommitPlanner> _commitPlanner = new();
    private readonly Mock<ITransactionReviewIdentityService> _identityService = new();
    private readonly Mock<ITransactionReviewDocumentFactory> _documentFactory = new();
    private readonly Mock<IWorkspaceDiffBuilder> _diffBuilder = new();
    private readonly Mock<IWorkspaceResolverFactory> _resolverFactory = new();
    private readonly Mock<IWorkspaceResolver> _resolver = new();
    private readonly TransactionReviewBuilder _target;

    public TransactionReviewBuilderTests()
    {
        _resolverFactory
            .Setup(item => item.Create(
                It.IsAny<Solution>(),
                It.IsAny<WorkspaceIdentity>(),
                It.IsAny<WorkspaceProjectTargetFrameworkMap>(),
                It.IsAny<SnapshotPrecondition>()))
            .Returns(_resolver.Object);

        _target = new TransactionReviewBuilder(
            _commitPlanner.Object,
            _identityService.Object,
            _documentFactory.Object,
            _diffBuilder.Object,
            _resolverFactory.Object);
    }

    [Fact]
    public async Task GIVEN_NoActiveTransaction_WHEN_CreatingReview_THEN_ShouldRejectInvalidState()
    {
        var session = CreateSession(transaction: null);

        var action = async () => await _target.CreateAsync(
            session,
            compilerValidation: null,
            diffDocument: null,
            contextLines: 3,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_CommitPlanningFailure_WHEN_CreatingReview_THEN_ShouldReturnFailure()
    {
        var session = CreateSession(CreateTransaction());
        _commitPlanner
            .Setup(item => item.CreateAsync(
                It.IsAny<string>(),
                session.Workspace.LoadedPath,
                session.Workspace.WorkspaceRoot,
                It.IsAny<Solution>(),
                It.IsAny<Solution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitPlanResult.Failed("ErrorMessage"));

        var result = await _target.CreateAsync(
            session,
            compilerValidation: null,
            diffDocument: null,
            contextLines: 3,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("ErrorMessage");
        _resolverFactory.Verify(item => item.Create(
            It.IsAny<Solution>(),
            It.IsAny<WorkspaceIdentity>(),
            It.IsAny<WorkspaceProjectTargetFrameworkMap>(),
            It.IsAny<SnapshotPrecondition>()), Times.Never);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GIVEN_ValidatedCommitPlan_WHEN_CreatingReview_THEN_ShouldProjectExactReview(
        bool includeDiff,
        bool includeCompilerValidation)
    {
        var transaction = CreateTransaction();
        var session = CreateSession(transaction) with
        {
            State = WorkspaceLifecycleState.TransactionConflicted,
        };

        var manifest = new WorkspaceCommitManifest
        {
            CommitId = "CommitId",
            LoadedPath = session.Workspace.LoadedPath,
            WorkspaceRoot = session.Workspace.WorkspaceRoot,
            State = RecoveryState.Prepared,
            Entries = [],
            CreatedDirectories = [],
        };

        var plan = new WorkspaceCommitPlan(manifest, new Dictionary<string, ReadOnlyMemory<byte>>());
        var changes = new ChangeSummary();
        var documents = Array.Empty<TransactionReviewDocument>();
        var identity = CreateIdentity(session, transaction);
        TransactionCompilerValidationOutcome? compilerValidation = null;
        if (includeCompilerValidation)
        {
            compilerValidation = new TransactionCompilerValidationOutcome
            {
                IsComplete = true,
                Succeeded = true,
                Transaction = transaction.ToInfo(conflicted: true),
                BaselineErrorCount = 1,
                StagedErrorCount = 1,
                IntroducedErrorCount = 0,
                DurationMilliseconds = 5,
                Projects =
                [
                    new TransactionCompilerProjectValidation
                    {
                        Project = "Project.csproj",
                        IsComplete = true,
                        BaselineErrorCount = 1,
                        StagedErrorCount = 1,
                        IntroducedErrorCount = 0,
                    },
                ],
            };
        }

        DocumentReference? diffDocument = null;
        DocumentDiff? diff = null;
        if (includeDiff)
        {
            diffDocument = new DocumentReference
            {
                DocumentId = "DocumentId",
                Path = "Document.cs",
                ProjectId = "ProjectId",
            };

            diff = new DocumentDiff { Document = diffDocument };
        }

        _commitPlanner
            .Setup(item => item.CreateAsync(
                It.IsAny<string>(),
                session.Workspace.LoadedPath,
                session.Workspace.WorkspaceRoot,
                transaction.BaselineSolution,
                transaction.CurrentSolution,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceCommitPlanResult.Succeeded(plan));

        _diffBuilder
            .Setup(item => item.CreateChangeSummaryAsync(
                transaction.BaselineSolution,
                transaction.CurrentSolution,
                _resolver.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);

        _documentFactory
            .Setup(item => item.Create(session, plan, changes))
            .Returns(documents);

        _identityService
            .Setup(item => item.Create(session, documents))
            .Returns(identity);

        if (diffDocument is not null)
        {
            _diffBuilder
                .Setup(item => item.CreateDocumentDiffAsync(
                    transaction.BaselineSolution,
                    transaction.CurrentSolution,
                    diffDocument,
                    _resolver.Object,
                    3,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(diff);
        }

        var result = await _target.CreateAsync(
            session,
            compilerValidation,
            diffDocument,
            contextLines: 3,
            TestContext.Current.CancellationToken);

        result.IsSucceeded.Should().BeTrue();
        result.Outcome!.Identity.Should().BeSameAs(identity);
        result.Outcome.Documents.Should().BeSameAs(documents);
        result.Outcome.Diff.Should().BeSameAs(diff);
        result.Outcome.Transaction.CanCommit.Should().BeFalse();
        result.Outcome.Provenance.Should().ContainSingle()
            .Which.Operation.Should().Be("Operation1");
        result.Outcome.Provenance[0].CodeAction.Should().BeSameAs(
            transaction.Revisions[0].CodeActionProvenance);

        var expectedValidationCount = includeCompilerValidation ? 3 : 2;
        result.Outcome.Validations.Should().HaveCount(expectedValidationCount);
        if (includeCompilerValidation)
        {
            result.Outcome.Validations.Should().ContainSingle(
                item => item.Name == "no-new-compiler-errors" && item.Succeeded);
        }

        var expectedDiffCalls = Times.Never();
        if (includeDiff)
        {
            expectedDiffCalls = Times.Once();
        }

        _diffBuilder.Verify(item => item.CreateDocumentDiffAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            It.IsAny<DocumentReference>(),
            _resolver.Object,
            3,
            It.IsAny<CancellationToken>()), expectedDiffCalls);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceTransaction CreateTransaction()
    {
        var project = _workspace.AddProject("Project", LanguageNames.CSharp);
        var baseline = project.Solution;
        var revision = new WorkspaceTransactionRevision
        {
            SnapshotId = WorkspaceSnapshotTestFactory.CreateId(2),
            Solution = baseline,
            Changes = new ChangeSummary(),
            Operation = "Operation1",
            Summary = "Summary1",
            Preview = new MutationPreview { Summary = "Summary1" },
            CodeActionProvenance = CreateCodeActionProvenance(),
        };

        var futureRevision = revision with
        {
            SnapshotId = WorkspaceSnapshotTestFactory.CreateId(3),
            Operation = "Operation2",
            Summary = "Summary2",
        };

        return new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(7),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = baseline,
            Revisions = [revision, futureRevision],
            CurrentRevision = 1,
            MaxRevisions = 5,
        };
    }

    private WorkspaceSessionSnapshot CreateSession(WorkspaceTransaction? transaction)
    {
        var committedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 2,
            LoadedPath = "/workspace/Project.csproj",
            WorkspaceRoot = "/workspace",
        };

        var currentSolution = transaction?.CurrentSolution ?? _workspace.CurrentSolution;
        var loadedWorkspace = new Mock<ILoadedWorkspace>();
        var operationGate = new Mock<IWorkspaceOperationGate>();
        var inputManifest = new WorkspaceInputManifest();

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = transaction is null ? WorkspaceLifecycleState.Ready : WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = loadedWorkspace.Object,
            CurrentSolution = currentSolution,
            Transaction = transaction,
            InputManifest = inputManifest,
            OperationGate = operationGate.Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(workspaceIdentity, committedSnapshotId, transaction),
        };
    }

    private static TransactionReviewIdentity CreateIdentity(
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction)
    {
        return new TransactionReviewIdentity
        {
            Algorithm = "Algorithm",
            ChangeSetDigest = "ChangeSetDigest",
            WorkspaceId = session.Workspace.WorkspaceId,
            WorkspaceEpoch = session.Workspace.WorkspaceEpoch,
            TransactionId = transaction.TransactionId.Value,
            SnapshotId = session.CurrentSnapshotIdentity.SnapshotId.Value,
            TransactionRevision = transaction.CurrentRevision,
        };
    }

    private static CodeActionMutationProvenance CreateCodeActionProvenance()
    {
        var provider = new MutationProviderIdentity
        {
            TypeName = "Provider.Type",
            AssemblyName = "Provider.Assembly",
            AssemblyVersion = "1.0.0.0",
        };

        return new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.CodeFix,
            Provider = provider,
        };
    }
}
