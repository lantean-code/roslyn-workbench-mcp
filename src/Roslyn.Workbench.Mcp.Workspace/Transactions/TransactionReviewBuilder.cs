namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Builds transaction review receipts from the same validated plan used by durable commit.
/// </summary>
internal sealed class TransactionReviewBuilder : ITransactionReviewBuilder
{
    private readonly IWorkspaceCommitPlanner _commitPlanner;
    private readonly ITransactionReviewIdentityService _identityService;
    private readonly ITransactionReviewDocumentFactory _documentFactory;
    private readonly IWorkspaceDiffBuilder _diffBuilder;
    private readonly IWorkspaceResolverFactory _resolverFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReviewBuilder"/> class.
    /// </summary>
    /// <param name="commitPlanner">The planner that supplies exact persistence operations and byte hashes.</param>
    /// <param name="identityService">The service that canonicalises the reviewed persistence set.</param>
    /// <param name="documentFactory">The factory that projects exact persistence entries for review.</param>
    /// <param name="diffBuilder">The builder that calculates bounded source differences.</param>
    /// <param name="resolverFactory">The factory used to create a resolver for the reviewed snapshot.</param>
    public TransactionReviewBuilder(
        IWorkspaceCommitPlanner commitPlanner,
        ITransactionReviewIdentityService identityService,
        ITransactionReviewDocumentFactory documentFactory,
        IWorkspaceDiffBuilder diffBuilder,
        IWorkspaceResolverFactory resolverFactory)
    {
        _commitPlanner = commitPlanner;
        _identityService = identityService;
        _documentFactory = documentFactory;
        _diffBuilder = diffBuilder;
        _resolverFactory = resolverFactory;
    }

    /// <inheritdoc/>
    public async ValueTask<TransactionReviewBuildResult> CreateAsync(
        WorkspaceSessionSnapshot session,
        TransactionCompilerValidationOutcome? compilerValidation,
        DocumentReference? diffDocument,
        int contextLines,
        CancellationToken cancellationToken)
    {
        var transaction = session.Transaction
            ?? throw new InvalidOperationException("A transaction review requires an active transaction.");

        var planningResult = await _commitPlanner.CreateAsync(
            Guid.NewGuid().ToString("n"),
            session.Workspace.LoadedPath,
            session.Workspace.WorkspaceRoot,
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            cancellationToken);

        if (!planningResult.IsSucceeded)
        {
            return TransactionReviewBuildResult.Failed(planningResult.ErrorMessage);
        }

        var snapshot = WorkspaceSnapshotPreconditionFactory.Create(
            session.CurrentSnapshotIdentity,
            transaction.CurrentRevision);

        var resolver = _resolverFactory.Create(
            transaction.CurrentSolution,
            session.Workspace,
            session.ProjectTargetFrameworks,
            snapshot);

        var changes = await _diffBuilder.CreateChangeSummaryAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            resolver,
            cancellationToken);

        var documents = await _documentFactory.CreateAsync(
            session,
            planningResult.Plan,
            changes,
            cancellationToken);

        var identity = _identityService.Create(session, documents);
        var diff = await CreateDiffAsync(
            transaction,
            resolver,
            diffDocument,
            contextLines,
            cancellationToken);

        var outcome = new TransactionReviewOutcome
        {
            Identity = identity,
            Transaction = transaction.ToInfo(session.State == WorkspaceLifecycleState.TransactionConflicted),
            Documents = documents,
            Provenance = TransactionMutationProvenanceFactory.Create(transaction),
            Validations = CreateValidations(compilerValidation),
            Diff = diff,
        };

        return TransactionReviewBuildResult.Succeeded(outcome);
    }

    private async ValueTask<DocumentDiff?> CreateDiffAsync(
        WorkspaceTransaction transaction,
        IWorkspaceResolver resolver,
        DocumentReference? diffDocument,
        int contextLines,
        CancellationToken cancellationToken)
    {
        if (diffDocument is null)
        {
            return null;
        }

        return await _diffBuilder.CreateDocumentDiffAsync(
            transaction.BaselineSolution,
            transaction.CurrentSolution,
            diffDocument,
            resolver,
            contextLines,
            cancellationToken);
    }

    private static List<TransactionReviewValidation> CreateValidations(
        TransactionCompilerValidationOutcome? compilerValidation)
    {
        var validations = new List<TransactionReviewValidation>
        {
            new TransactionReviewValidation
            {
                Name = "commit-planning",
                Succeeded = true,
                Message = "The staged change produced a valid in-memory persistence plan.",
            },
            new TransactionReviewValidation
            {
                Name = "workspace-containment",
                Succeeded = true,
                Message = "Every persistence target is physically contained by current Workspace policy.",
            },
        };

        if (compilerValidation is not null)
        {
            validations.Add(new TransactionReviewValidation
            {
                Name = "no-new-compiler-errors",
                Succeeded = true,
                Message = $"No new compiler errors were introduced across {compilerValidation.Projects.Count} affected loaded project evaluation(s).",
            });
        }

        return validations;
    }
}
