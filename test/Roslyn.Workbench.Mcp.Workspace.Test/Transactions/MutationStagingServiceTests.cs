using Roslyn.Workbench.Mcp.Workspace.Coordination;

namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class MutationStagingServiceTests : IDisposable
{
    private readonly AdhocWorkspace _workspace;
    private readonly Mock<IWorkspaceOperationResultFactory> _resultFactory;
    private readonly Mock<IWorkspaceSessionStore> _sessionStore;
    private readonly Mock<IWorkspaceDiffBuilder> _diffBuilder;
    private readonly Mock<IWorkspaceResolverFactory> _resolverFactory;
    private readonly Mock<IWorkspaceInstanceStatusPublisher> _instanceStatusPublisher;
    private readonly Mock<IWorkspaceMutationCandidateProcessor> _candidateProcessor;
    private readonly Mock<IWorkspaceMutationCandidateIdentityService> _candidateIdentityService;
    private readonly MutationStagingService _target;

    public MutationStagingServiceTests()
    {
        _workspace = new AdhocWorkspace();
        _resultFactory = new Mock<IWorkspaceOperationResultFactory>();
        _sessionStore = new Mock<IWorkspaceSessionStore>();
        _diffBuilder = new Mock<IWorkspaceDiffBuilder>();
        _resolverFactory = new Mock<IWorkspaceResolverFactory>();
        _instanceStatusPublisher = new Mock<IWorkspaceInstanceStatusPublisher>();
        _candidateProcessor = new Mock<IWorkspaceMutationCandidateProcessor>();
        _candidateIdentityService = new Mock<IWorkspaceMutationCandidateIdentityService>();
        _sessionStore
            .Setup(item => item.AllocateWorkspaceSnapshotId())
            .Returns(new WorkspaceSnapshotId(3));

        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                It.IsAny<Solution>(),
                It.IsAny<Solution>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Solution _,
                Solution candidateSolution,
                string _,
                CancellationToken _) => ValueTask.FromResult(
                    WorkspaceMutationCandidateProcessingResult.Succeeded(candidateSolution)));

        _target = new MutationStagingService(
            _resultFactory.Object,
            _sessionStore.Object,
            _diffBuilder.Object,
            _resolverFactory.Object,
            _instanceStatusPublisher.Object,
            _candidateProcessor.Object,
            _candidateIdentityService.Object);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_Staging_THEN_ShouldPropagateCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationCandidate { CandidateSolution = _workspace.CurrentSolution },
            [],
            [],
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GIVEN_NoTransactionOwner_WHEN_Staging_THEN_ShouldRequireTransaction()
    {
        var expected = CreateRejectedResult("TransactionRequired");
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot());
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction,
                null,
                null,
                null))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationCandidate { CandidateSolution = _workspace.CurrentSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReadSession(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_OwnerWithoutTransaction_WHEN_Staging_THEN_ShouldRequireTransaction()
    {
        var expected = CreateRejectedResult("TransactionRequired");
        var session = CreateSession(transaction: null);
        SetupOwner(session);
        SetupTransactionRequiredResult(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationCandidate { CandidateSolution = _workspace.CurrentSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _candidateProcessor.Verify(item => item.ProcessAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LinkedDocumentMergeFails_WHEN_Staging_THEN_ShouldReturnValidationFailure()
    {
        var currentSolution = CreateSolution();
        var candidate = new WorkspaceMutationCandidate { CandidateSolution = currentSolution };
        var transaction = CreateTransaction(currentSolution);
        var session = CreateSession(transaction);
        var error = new WorkspaceOperationError
        {
            Code = WorkspaceErrorCodes.LinkedDocumentConflict,
            Message = "Message",
        };

        var expected = CreateRejectedResult(error.Code);
        SetupOwner(session);
        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                currentSolution,
                candidate.CandidateSolution,
                session.Workspace.WorkspaceRoot,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(error));

        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                error,
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            candidate,
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(
            item => item.ReplaceSession(It.IsAny<WorkspaceSessionSnapshot>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_MergedCandidateValidationFails_WHEN_Staging_THEN_ShouldReturnValidationFailure()
    {
        var currentSolution = CreateSolution();
        var candidate = new WorkspaceMutationCandidate { CandidateSolution = currentSolution };
        var transaction = CreateTransaction(currentSolution);
        var session = CreateSession(transaction);
        var error = new WorkspaceOperationError
        {
            Code = "UnsupportedChange",
            Message = "Message",
        };

        var expected = CreateRejectedResult(error.Code);
        SetupOwner(session);
        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                currentSolution,
                candidate.CandidateSolution,
                session.Workspace.WorkspaceRoot,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(error));

        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                error,
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            candidate,
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(
            item => item.ReplaceSession(It.IsAny<WorkspaceSessionSnapshot>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreparedCandidateNormalizationFails_WHEN_Staging_THEN_ShouldRejectAsChangedWithoutAppendingRevision()
    {
        var currentSolution = CreateSolution();
        var identity = new WorkspaceMutationCandidateIdentity { Documents = [] };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = identity,
            MaximumChangedDocuments = 1,
        };

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = currentSolution,
            Precondition = precondition,
        };

        var transaction = CreateTransaction(currentSolution);
        var session = CreateSession(transaction);
        var processingError = new WorkspaceOperationError
        {
            Code = "UnsupportedChange",
            Message = "Message",
        };

        var expected = CreateRejectedResult(WorkspaceErrorCodes.MutationCandidateChanged);
        SetupOwner(session);
        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                currentSolution,
                candidate.CandidateSolution,
                session.Workspace.WorkspaceRoot,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(processingError));
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.MutationCandidateChanged,
                "The mutation candidate no longer matches the previously prepared operation.",
                RequiredAction.ResolveTargetAgain,
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            candidate,
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _candidateIdentityService.Verify(item => item.CreateAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _diffBuilder.Verify(item => item.CreateChangeSummaryAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<IWorkspaceResolver>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _sessionStore.Verify(item => item.ReplaceSessionAfterStaging(
            It.IsAny<WorkspaceSessionSnapshot>(),
            It.IsAny<IReadOnlyList<WorkspaceSnapshotId>>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_MissingOwnerSession_WHEN_Staging_THEN_ShouldRequireTransaction()
    {
        var expected = CreateRejectedResult("TransactionRequired");
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot
        {
            TransactionOwnerWorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        });

        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns((WorkspaceSessionSnapshot?)null);
        SetupTransactionRequiredResult(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationCandidate { CandidateSolution = _workspace.CurrentSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_CandidateValidationFails_WHEN_Staging_THEN_ShouldReturnValidationFailure()
    {
        var currentSolution = CreateSolution();
        var expected = CreateRejectedResult("InvalidMutationProposal");
        var session = CreateSession(CreateTransaction(currentSolution));
        SetupOwner(session);
        SetupValidationFailureResult(expected, "InvalidMutationProposal", "Mutation proposals must belong to the current workspace.");

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationCandidate { CandidateSolution = currentSolution },
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_PreparedCandidateNoLongerMatches_WHEN_Staging_THEN_ShouldRejectWithoutAppendingRevision()
    {
        var currentSolution = CreateSolution();
        var identity = new WorkspaceMutationCandidateIdentity
        {
            Documents = [],
        };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = identity,
            MaximumChangedDocuments = 1,
        };

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = currentSolution,
            Precondition = precondition,
        };

        var transaction = CreateTransaction(currentSolution);
        var session = CreateSession(transaction);
        var expected = CreateRejectedResult(WorkspaceErrorCodes.MutationCandidateChanged);
        SetupOwner(session);
        _candidateIdentityService
            .Setup(item => item.CreateAsync(
                currentSolution,
                currentSolution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(identity);
        _candidateIdentityService
            .Setup(item => item.MatchesPrecondition(precondition, identity))
            .Returns(false);
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.MutationCandidateChanged,
                "The mutation candidate no longer matches the previously prepared operation.",
                RequiredAction.ResolveTargetAgain,
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            candidate,
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _diffBuilder.Verify(item => item.CreateChangeSummaryAsync(
            It.IsAny<Solution>(),
            It.IsAny<Solution>(),
            It.IsAny<IWorkspaceResolver>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _sessionStore.Verify(item => item.ReplaceSessionAfterStaging(
            It.IsAny<WorkspaceSessionSnapshot>(),
            It.IsAny<IReadOnlyList<WorkspaceSnapshotId>>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreparedCandidateStillMatches_WHEN_Staging_THEN_ShouldAppendRevision()
    {
        var currentSolution = CreateSolution();
        var identity = new WorkspaceMutationCandidateIdentity
        {
            Documents = [],
        };
        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = identity,
            MaximumChangedDocuments = 1,
        };

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = currentSolution,
            Precondition = precondition,
        };

        var transaction = CreateTransaction(currentSolution);
        var session = CreateSession(transaction);
        var changes = new ChangeSummary();
        var expected = WorkspaceOperationResult.NoChange<MutationStagingOutcome>();
        SetupOwner(session);
        _candidateIdentityService
            .Setup(item => item.CreateAsync(
                currentSolution,
                currentSolution,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(identity);
        _candidateIdentityService
            .Setup(item => item.MatchesPrecondition(precondition, identity))
            .Returns(true);
        _diffBuilder
            .Setup(item => item.CreateChangeSummaryAsync(
                currentSolution,
                currentSolution,
                It.IsAny<IWorkspaceResolver>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(changes);
        _resultFactory
            .Setup(item => item.Succeeded(
                It.IsAny<MutationStagingOutcome>(),
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            candidate,
            [],
            [],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSessionAfterStaging(
            It.Is<WorkspaceSessionSnapshot>(replacement => replacement.Transaction != null && replacement.Transaction.CurrentRevision == 1),
            It.IsAny<IReadOnlyList<WorkspaceSnapshotId>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ValidCandidate_WHEN_Staging_THEN_ShouldReplaceSessionAndReturnMappedSuccess()
    {
        var currentSolution = CreateSolution(documentPathDiffersByCase: true);
        var document = currentSolution.Projects.Single().Documents.Single();
        var candidateSolution = document.WithText(SourceText.From("class Updated { }")).Project.Solution;
        var transaction = CreateTransaction(currentSolution) with
        {
            Revisions =
            [
                new WorkspaceTransactionRevision
                {
                    SnapshotId = new WorkspaceSnapshotId(2),
                    Solution = currentSolution,
                    Changes = new ChangeSummary(),
                    Operation = "Operation",
                    Summary = "Summary",
                    Preview = new MutationPreview(),
                },
            ],
            CurrentRevision = 0,
            MaxRevisions = 3,
        };

        var session = CreateSession(transaction);
        SetupOwner(session);
        var changes = new ChangeSummary();
        var handlerWarning = new WarningInfo { Code = "HandlerWarning", Message = "Message" };
        var proposalWarning = new WarningInfo { Code = "ProposalWarning", Message = "Message" };
        var expected = WorkspaceOperationResult.NoChange<MutationStagingOutcome>();
        _diffBuilder
            .Setup(item => item.CreateChangeSummaryAsync(
                currentSolution,
                candidateSolution,
                It.IsAny<IWorkspaceResolver>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(changes);

        _resultFactory
            .Setup(item => item.Succeeded(
                It.Is<MutationStagingOutcome>(outcome =>
                    outcome.Operation == "OperationName"
                    && outcome.Summary == "Summary"
                    && outcome.Changes == changes
                    && outcome.Transaction.Revision == 1),
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.Is<IReadOnlyList<WarningInfo>>(items => items.SequenceEqual(new[] { handlerWarning, proposalWarning }))))
            .Returns(expected);

        var result = await _target.StageAsync(
            "OperationName",
            new WorkspaceMutationCandidate
            {
                CandidateSolution = candidateSolution,
                Summary = "Summary",
                Warnings = [proposalWarning],
            },
            [],
            [handlerWarning],
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        _sessionStore.Verify(item => item.ReplaceSessionAfterStaging(
            It.Is<WorkspaceSessionSnapshot>(replacement =>
                replacement.CurrentSolution == candidateSolution
                && replacement.Transaction != null
                && replacement.Transaction.CurrentRevision == 1
                && replacement.Transaction.Revisions.Count == 1
                && replacement.Transaction.CurrentSnapshotId == new WorkspaceSnapshotId(3)
                && replacement.CurrentSnapshotIdentity.TransactionId == replacement.Transaction.TransactionId
                && replacement.CurrentSnapshotIdentity.SnapshotId == new WorkspaceSnapshotId(3)),
            It.Is<IReadOnlyList<WorkspaceSnapshotId>>(snapshotIds =>
                snapshotIds.SequenceEqual(new[] { new WorkspaceSnapshotId(2) }))), Times.Once);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private void SetupOwner(WorkspaceSessionSnapshot session)
    {
        _sessionStore.Setup(item => item.ReadSnapshot()).Returns(new WorkspaceHostSnapshot
        {
            TransactionOwnerWorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        });

        _sessionStore.Setup(item => item.ReadSession(Guid.Parse("11111111-1111-1111-1111-111111111111"))).Returns(session);
    }

    private void SetupTransactionRequiredResult(WorkspaceOperationResult<MutationStagingOutcome> result)
    {
        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                WorkspaceErrorCodes.TransactionRequired,
                "Start a transaction before invoking mutation tools.",
                RequiredAction.StartTransaction,
                null,
                null,
                null))
            .Returns(result);
    }

    private void SetupValidationFailureResult(
        WorkspaceOperationResult<MutationStagingOutcome> result,
        string code,
        string message)
    {
        var error = new WorkspaceOperationError
        {
            Code = code,
            Message = message,
        };

        _candidateProcessor
            .Setup(item => item.ProcessAsync(
                It.IsAny<Solution>(),
                It.IsAny<Solution>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkspaceMutationCandidateProcessingResult.Failed(error));

        _resultFactory
            .Setup(item => item.Rejected<MutationStagingOutcome>(
                error,
                null,
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>()))
            .Returns(result);
    }

    private Solution CreateSolution(bool documentPathDiffersByCase = false)
    {
        var project = _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Project",
            "Project",
            LanguageNames.CSharp,
            filePath: Path.Combine(Path.GetTempPath(), "Project", "Project.csproj")));

        _workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "Document.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("class C { }"), VersionStamp.Default)),
            filePath: Path.Combine(Path.GetTempPath(), documentPathDiffersByCase ? "project" : "Project", "Document.cs")));

        return _workspace.CurrentSolution;
    }

    private WorkspaceSessionSnapshot CreateSession(WorkspaceTransaction? transaction)
    {
        var committedSnapshotId = new WorkspaceSnapshotId(1);
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            LoadedPath = "LoadedPath",
        };

        return new WorkspaceSessionSnapshot
        {
            CommittedSnapshotId = committedSnapshotId,
            State = WorkspaceLifecycleState.TransactionActive,
            Workspace = workspaceIdentity,
            LoadedWorkspace = null!,
            CurrentSolution = transaction?.CurrentSolution ?? _workspace.CurrentSolution,
            Transaction = transaction,
            InputManifest = null!,
            OperationGate = new Mock<IWorkspaceOperationGate>().Object,
            CurrentSnapshotIdentity = WorkspaceSnapshotIdentity.Create(
                workspaceIdentity,
                committedSnapshotId,
                transaction),
        };
    }

    private static WorkspaceTransaction CreateTransaction(Solution solution)
    {
        return new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(1),
            BaselineSnapshotId = new WorkspaceSnapshotId(1),
            BaselineSolution = solution,
            CurrentRevision = 0,
            MaxRevisions = 3,
        };
    }

    private static WorkspaceOperationResult<MutationStagingOutcome> CreateRejectedResult(string code)
    {
        var error = new WorkspaceOperationError
        {
            Code = code,
            Message = "Message",
        };

        return WorkspaceOperationResult.Rejected<MutationStagingOutcome>(error);
    }
}
